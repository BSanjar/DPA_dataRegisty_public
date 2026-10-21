using System.Security.Claims;
using data_registry_public.Infrastructure.Hosting;
using data_registry_public.Infrastructure.Logging;
using data_registry_public.Integrations;
using data_registry_public.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace data_registry_public.Controllers
{
    /// <summary>
    /// Вход пользователей портала через ЕСИ (Тундук): /esi/Login → ЕСИ → /esi/auth.
    /// После входа организация пользователя подтягивается из Минюста и привязывается к нему.
    /// Тестовый вход без ЕСИ — /esi/LoginTest (флаг Features:EsiTestLogin, на проде закрыт).
    /// </summary>
    [Route("[controller]/[action]")]
    public class esiController : Controller
    {
        // Ключи TempData между уходом в ЕСИ и возвратом (cookie TempData, защищена DataProtection).
        private const string StateKey = "esi_state";
        private const string VerifierKey = "esi_code_verifier";
        private const string ReturnUrlKey = "esi_return_url";

        private readonly EsiClient _esi;
        private readonly ILogger<esiController> _logger;
        private readonly AppDbContext _db;
        private readonly IMinJustService _minJust;
        private readonly AppFeatures _features;

        public esiController(EsiClient esi, ILogger<esiController> logger, AppDbContext db, IMinJustService minJust, AppFeatures features)
        {
            _esi = esi;
            _logger = logger;
            _db = db;
            _minJust = minJust;
            _features = features;
        }

        /// <summary>Уход в ЕСИ: новые state и PKCE, сохранение их до возврата.</summary>
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return SafeRedirect(returnUrl);
            }

            if (!_esi.IsConfigured)
            {
                _logger.LogError("ЕСИ: не заданы настройки секции Esi (адреса, ClientId, ClientSecret, RedirectUrl)");
                TempData["ErrorMessage"] = "Вход через ЕСИ временно недоступен. Попробуйте позже.";
                return RedirectToAction("Index", "Home");
            }

            var request = _esi.CreateAuthorizeRequest();
            TempData[StateKey] = request.State;
            TempData[VerifierKey] = request.CodeVerifier;
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                TempData[ReturnUrlKey] = returnUrl;
            }

            return Redirect(request.Url);
        }

        /// <summary>
        /// Возврат из ЕСИ (redirect_uri). Проверяем state, меняем code на токен, получаем данные пользователя,
        /// регистрируем/обновляем его, привязываем организацию и входим.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> auth(string? code, string? state, string? error, string? error_description)
        {
            var expectedState = TempData[StateKey] as string;
            var codeVerifier = TempData[VerifierKey] as string;
            var returnUrl = TempData[ReturnUrlKey] as string;

            // Пользователь отменил вход или ЕСИ вернула ошибку (параметры error/error_description по стандарту).
            if (!string.IsNullOrEmpty(error))
            {
                using (LogCategories.Push(LogCategories.Integration))
                {
                    _logger.LogWarning("ЕСИ вернула ошибку авторизации: {Error} {Description}", error, error_description ?? "");
                }
                return Fail(error == "access_denied" ? "Вход через ЕСИ отменён." : "ЕСИ отклонила запрос на вход. Попробуйте ещё раз.");
            }

            // state защищает от подмены ответа (CSRF): должен совпасть с выданным в Login.
            if (string.IsNullOrEmpty(expectedState) || string.IsNullOrEmpty(codeVerifier))
            {
                _logger.LogWarning("ЕСИ: возврат без начатого входа (нет state/verifier — истёк срок или вход начат в другом браузере)");
                return Fail("Сеанс входа истёк. Нажмите «Войти через ЕСИ» ещё раз.");
            }
            if (!string.Equals(state, expectedState, StringComparison.Ordinal))
            {
                _logger.LogWarning("ЕСИ: state в ответе не совпал с выданным при входе — вход отклонён");
                return Fail("Не удалось подтвердить вход. Нажмите «Войти через ЕСИ» ещё раз.");
            }
            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("ЕСИ: возврат без кода авторизации");
                return Fail("Авторизация через ЕСИ не завершена.");
            }

            try
            {
                var ct = HttpContext.RequestAborted;
                var accessToken = await _esi.ExchangeCodeAsync(code, codeVerifier, ct);
                if (accessToken is null)
                {
                    return Fail("Не удалось получить подтверждение от ЕСИ. Попробуйте ещё раз.");
                }

                var info = await _esi.GetUserInfoAsync(accessToken, ct);
                if (info is null || string.IsNullOrWhiteSpace(info.Pin))
                {
                    return Fail("ЕСИ не передала данные пользователя. Попробуйте ещё раз.");
                }

                var userModel = MapUser(info);
                var userId = await RegisterOrUpdateUserAsync(userModel);
                if (string.IsNullOrEmpty(userId))
                {
                    return Fail("Не удалось зарегистрировать пользователя после ЕСИ.");
                }
                userModel.Id = userId;

                // Организация из Минюста и привязка пользователя к ней.
                var link = await UpsertOrganizationAndLinkUserAsync(userModel);
                if (!link.Success || string.IsNullOrEmpty(link.OrgId))
                {
                    _logger.LogWarning("ЕСИ-вход отменён для {UserId}: {Err}", userId, link.ErrorMessage);
                    return Fail("Вход не выполнен: " + (link.ErrorMessage ?? "не удалось получить и сохранить данные организации из Минюста."));
                }

                await Authenticate(userModel, link.OrgId);
                _logger.LogInformation("Вход через ЕСИ: пользователь {UserId} ({Name}), организация {Org}", userId, userModel.Name, link.OrgId);
                return SafeRedirect(returnUrl);
            }
            catch (Exception ex)
            {
                // Текст исключения пользователю не показываем — подробности в логах (errors).
                _logger.LogError(ex, "ЕСИ: ошибка при завершении входа");
                return Fail("Не удалось авторизоваться через ЕСИ. Попробуйте ещё раз.");
            }
        }

        /// <summary>
        /// Тестовый (эмуляционный) вход без реального ЕСИ: тестовый пользователь,
        /// организация — из эмулятора Минюста.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> LoginTest(string? returnUrl = null)
        {
            // Features:EsiTestLogin — только локально и на тестовом сервере.
            if (!_features.EsiTestLogin)
            {
                return NotFound();
            }

            var userModel = new PublicUser
            {
                Email = "test.user@dpa.local",
                EsiEmail = "test.user@dpa.local",
                Name = "Тестовый Пользователь",
                EsiName = "Тестовый Пользователь",
                EsiFamilyName = "Тестовый",
                EsiGivenName = "Пользователь",
                EsiPin = "01001202210023",
                EsiBirthdate = "01.01.1990",
                EsiPhoneNumber = "+996 700 000 000",
                OrganizationTin = "01001202210023",
                PositionName = "Ответственный за ПДн",
            };

            var userId = await RegisterOrUpdateUserAsync(userModel);
            if (string.IsNullOrEmpty(userId))
            {
                return Fail("Не удалось зарегистрировать тестового пользователя.");
            }
            userModel.Id = userId;

            // useEmulator = true: обращаемся только к эмулятору Минюста
            var link = await UpsertOrganizationAndLinkUserAsync(userModel, useEmulator: true);
            if (!link.Success || string.IsNullOrEmpty(link.OrgId))
            {
                _logger.LogWarning("Тестовый вход отменён: {Err}", link.ErrorMessage);
                return Fail("Вход не выполнен: " + (link.ErrorMessage ?? "не удалось сохранить организацию."));
            }

            await Authenticate(userModel, link.OrgId);
            _logger.LogInformation("Тестовый вход: {Name} авторизован, org: {Org}", userModel.Name, link.OrgId);
            TempData["SuccessMessage"] = "Вы вошли в тестовом режиме. Данные организации — из эмулятора Минюста.";
            return SafeRedirect(returnUrl);
        }

        public async Task<IActionResult> Logout()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                _logger.LogInformation("Выход пользователя {UserId}", User.FindFirst("id")?.Value);
            }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // === Вспомогательное ===============================================

        /// <summary>Безопасный редирект на returnUrl, иначе на UserPage.</summary>
        private IActionResult SafeRedirect(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "UserPage");
        }

        /// <summary>Вход не состоялся: сообщение на главной (через баннер ErrorMessage в шапке).</summary>
        private IActionResult Fail(string message)
        {
            TempData["ErrorMessage"] = message;
            return RedirectToAction("Index", "Home");
        }

        private static PublicUser MapUser(EsiUserInfo info) => new()
        {
            EsiSub = info.Sub,
            EsiPin = info.Pin?.Trim(),
            EsiName = info.Name,
            EsiFamilyName = info.FamilyName,
            EsiGivenName = info.GivenName,
            EsiGender = info.Gender,
            EsiBirthdate = info.Birthdate,
            EsiCitizenship = info.Citizenship,
            EsiEmail = info.Email,
            EsiEmailVerified = info.EmailVerified,
            EsiPhoneNumber = info.PhoneNumber,
            EsiPhoneNumberVerified = info.PhoneNumberVerified,
            OrganizationTin = info.OrganizationTin?.Trim(),
            OrganizationName = info.OrganizationName,
            Name = info.Name,
            Email = info.Email
        };

        private async Task Authenticate(PublicUser user, string? orgId = null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, user.EsiName ?? user.Name ?? ""),
                new(ClaimTypes.Email, user.EsiEmail ?? user.Email ?? ""),
                new(ClaimTypes.DateOfBirth, user.EsiBirthdate ?? ""),
                new("pin", user.EsiPin ?? ""),
                new("orgPin", user.OrganizationTin ?? ""),
                new("orgName", user.OrganizationName ?? ""),
                new("id", user.Id ?? ""),
            };
            if (!string.IsNullOrEmpty(orgId))
            {
                claims.Add(new Claim("orgId", orgId));
            }

            var identity = new ClaimsIdentity(claims, "ApplicationCookie",
                ClaimsIdentity.DefaultNameClaimType, ClaimsIdentity.DefaultRoleClaimType);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }

        /// <summary>
        /// Пользователь по ПИН: новый — создаём, существующий — обновляем данные из ЕСИ
        /// (ЕСИ — первоисточник ФИО, email, телефона). Возвращает public_users.id или null.
        /// </summary>
        private async Task<string?> RegisterOrUpdateUserAsync(PublicUser user)
        {
            try
            {
                var existing = await _db.PublicUsers.FirstOrDefaultAsync(a => a.EsiPin == user.EsiPin);
                var now = DateTime.Now;
                if (existing is not null)
                {
                    existing.EsiSub = user.EsiSub ?? existing.EsiSub;
                    existing.EsiName = user.EsiName ?? existing.EsiName;
                    existing.EsiFamilyName = user.EsiFamilyName ?? existing.EsiFamilyName;
                    existing.EsiGivenName = user.EsiGivenName ?? existing.EsiGivenName;
                    existing.EsiGender = user.EsiGender ?? existing.EsiGender;
                    existing.EsiBirthdate = user.EsiBirthdate ?? existing.EsiBirthdate;
                    existing.EsiCitizenship = user.EsiCitizenship ?? existing.EsiCitizenship;
                    existing.EsiEmail = user.EsiEmail ?? existing.EsiEmail;
                    existing.EsiEmailVerified = user.EsiEmailVerified ?? existing.EsiEmailVerified;
                    existing.EsiPhoneNumber = user.EsiPhoneNumber ?? existing.EsiPhoneNumber;
                    existing.EsiPhoneNumberVerified = user.EsiPhoneNumberVerified ?? existing.EsiPhoneNumberVerified;
                    existing.Name = user.Name ?? existing.Name;
                    existing.Email = user.Email ?? existing.Email;
                    existing.UpdatedAt = now;
                    await _db.SaveChangesAsync();
                    return existing.Id;
                }

                user.Id = Guid.NewGuid().ToString();
                user.CreatedAt = now;
                user.UpdatedAt = now;
                user.Isdeleted = "0";
                _db.PublicUsers.Add(user);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Зарегистрирован новый пользователь {UserId} ({Name}, ПИН {Pin})",
                    user.Id, user.Name, AppLogging.Mask(user.EsiPin));
                return user.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось зарегистрировать/обновить пользователя (ПИН {Pin})", AppLogging.Mask(user.EsiPin));
                return null;
            }
        }

        /// <summary>Результат попытки привязки организации к пользователю.</summary>
        private sealed class OrgLinkResult
        {
            public bool Success { get; set; }
            public string? OrgId { get; set; }
            public string? ErrorMessage { get; set; }
        }

        /// <summary>
        /// Подтягивает данные организации из Минюста, upsert в таблицу organizations
        /// и связывает PublicUser с организацией.
        /// </summary>
        /// <param name="user">Пользователь с заполненным OrganizationTin</param>
        /// <param name="useEmulator">При true — используется эмулятор Минюста (для тестового входа)</param>
        private async Task<OrgLinkResult> UpsertOrganizationAndLinkUserAsync(PublicUser user, bool useEmulator = false)
        {
            var result = new OrgLinkResult();

            if (string.IsNullOrWhiteSpace(user.OrganizationTin))
            {
                result.ErrorMessage = "У пользователя не указан ИНН организации (ЕСИ не передала поле organization_tin).";
                _logger.LogWarning("Вход без OrganizationTin для {UserId}", user.Id);
                return result;
            }

            try
            {
                // 1. Минюст (реальный API или эмулятор)
                var info = await _minJust.GetOrganizationByTinAsync(user.OrganizationTin, useEmulator);
                if (info == null)
                {
                    // Минюст недоступен или не нашёл субъект. Если организация уже синхронизирована ранее —
                    // пускаем с сохранёнными реквизитами (без подмены данных), иначе вход невозможен.
                    var tin = user.OrganizationTin.Trim();
                    var known = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == tin);
                    if (known != null)
                    {
                        _logger.LogWarning("Минюст не вернул данные по ИНН {Tin}; вход с ранее сохранёнными реквизитами (синхронизация {Synced})",
                            user.OrganizationTin, known.LastSyncedAt);
                        var dbUserFallback = await _db.PublicUsers.FirstOrDefaultAsync(u => u.Id == user.Id);
                        if (dbUserFallback != null)
                        {
                            dbUserFallback.Organization = known.Id;
                            dbUserFallback.OrganizationTin = known.Id;
                            dbUserFallback.OrganizationName = known.Fullnamegl;
                            dbUserFallback.UpdatedAt = DateTime.Now;
                            await _db.SaveChangesAsync();
                        }
                        user.OrganizationName = known.Fullnamegl;
                        result.Success = true;
                        result.OrgId = known.Id;
                        return result;
                    }

                    result.ErrorMessage = $"Не удалось получить сведения об организации по ИНН {user.OrganizationTin} из Минюста. Попробуйте позже или обратитесь в Агентство.";
                    _logger.LogWarning("Минюст не вернул данные по ИНН {Tin}", user.OrganizationTin);
                    return result;
                }

                // 2. Код сектора — только из справочника (FK organizations_fk); неизвестный → 'OTHER' (или NULL).
                string? safeSector = null;
                if (!string.IsNullOrWhiteSpace(info.BusinessSector)
                    && await _db.OrganizationBuisnesSectors.AsNoTracking().AnyAsync(s => s.Id == info.BusinessSector))
                {
                    safeSector = info.BusinessSector;
                }
                if (safeSector == null)
                {
                    if (await _db.OrganizationBuisnesSectors.AsNoTracking().AnyAsync(s => s.Id == "OTHER")) safeSector = "OTHER";
                    _logger.LogInformation("Сектор '{Code}' не найден в справочнике, использую '{Fallback}'",
                        info.BusinessSector, safeSector ?? "NULL");
                }

                // 3. Upsert организации (Id = ИНН). При повторном входе обновляются все реквизиты.
                var now = DateTime.Now;
                var existing = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == info.Tin);
                if (existing == null)
                {
                    _db.Organizations.Add(new Organization
                    {
                        Id = info.Tin,
                        Fullnamegl = info.FullName,
                        ShortName = info.ShortName,
                        LegalForm = info.LegalForm,
                        RegistrationNumber = info.RegistrationNumber,
                        RegistrationDate = info.RegistrationDate,
                        Address = info.Address,
                        DirectorName = info.DirectorName,
                        DirectorPosition = info.DirectorPosition,
                        Status = string.IsNullOrEmpty(info.Status) ? "Активный" : info.Status,
                        Businesssector = safeSector,
                        Risklevel = "Low",
                        LastSyncedAt = now,
                    });
                }
                else
                {
                    if (!string.IsNullOrEmpty(info.FullName)) existing.Fullnamegl = info.FullName;
                    if (!string.IsNullOrEmpty(info.ShortName)) existing.ShortName = info.ShortName;
                    if (!string.IsNullOrEmpty(info.LegalForm)) existing.LegalForm = info.LegalForm;
                    if (!string.IsNullOrEmpty(info.RegistrationNumber)) existing.RegistrationNumber = info.RegistrationNumber;
                    if (!string.IsNullOrEmpty(info.RegistrationDate)) existing.RegistrationDate = info.RegistrationDate;
                    if (!string.IsNullOrEmpty(info.Address)) existing.Address = info.Address;
                    if (!string.IsNullOrEmpty(info.DirectorName)) existing.DirectorName = info.DirectorName;
                    if (!string.IsNullOrEmpty(info.DirectorPosition)) existing.DirectorPosition = info.DirectorPosition;
                    if (!string.IsNullOrEmpty(info.Status)) existing.Status = info.Status;
                    // сектор обновляем только если новый код валиден (не обнуляем имеющийся)
                    if (safeSector != null) existing.Businesssector = safeSector;
                    existing.LastSyncedAt = now;
                }

                // 4. Привязываем пользователя
                var dbUser = await _db.PublicUsers.FirstOrDefaultAsync(u => u.Id == user.Id);
                if (dbUser != null)
                {
                    dbUser.Organization = info.Tin;
                    dbUser.OrganizationTin = info.Tin;
                    dbUser.OrganizationName = info.FullName;
                    dbUser.UpdatedAt = now;
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("Организация {Tin} ({Name}) привязана к пользователю {UserId} ({UserName})",
                    info.Tin, info.FullName, user.Id, user.Name);

                // Модель в памяти — чтобы корректно лёг claim orgName.
                user.OrganizationName = info.FullName;
                result.Success = true;
                result.OrgId = info.Tin;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении организации для пользователя {UserId}", user.Id);
                result.ErrorMessage = "Ошибка при сохранении организации. Попробуйте позже или обратитесь в Агентство.";
                return result;
            }
        }
    }
}
