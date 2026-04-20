using data_registry_local.Models;
using data_registry_public.Integrations;
using data_registry_public.Models;
using data_registry_public.Models.local_models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace data_registry_public.Controllers
{
    [Route("[controller]/[action]")]
    public class esiController : Controller
    {

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppSettings _appSettings;
        private readonly ILogger<esiController> _logger;
        private readonly AppDbContext _db;
        private readonly IMinJustService _minJust;

        public esiController(
            IHttpClientFactory httpClientFactory,
            IOptions<AppSettings> appSettings,
            ILogger<esiController> logger,
            AppDbContext db,
            IMinJustService minJust)
        {
            _httpClientFactory = httpClientFactory;
            _appSettings = appSettings.Value;
            _logger = logger;
            _db = db;
            _minJust = minJust;
        }




        [HttpGet]
        public IActionResult Login()
        {

            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "UserPage");
            }

            // Generate state, code verifier, and code challenge
            string state = Guid.NewGuid().ToString();
            string codeVerifier = GenerateCodeVerifier();
            string codeChallenge = GenerateCodeChallenge(codeVerifier);

            // Store code verifier and state in TempData
            TempData["code_verifier"] = codeVerifier;
            TempData["state"] = state;

            // Construct the authorization URL
            string authorizeUrl = $"{_appSettings.authorizeUrl}?response_type=code&client_id={_appSettings.clientId}&redirect_uri={_appSettings.redirectUrl}&scope=openid profile email phone&code_challenge={codeChallenge}&code_challenge_method=S256";

            return Redirect(authorizeUrl);
        }



        /// <summary>
        /// Тестовый (эмуляционный) вход без реального ЕСИ.
        /// Создаёт тестового пользователя и подтягивает организацию
        /// через эмулятор Минюста.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> LoginTest()
        {
            // Фиксированный тестовый пользователь с известным ИНН.
            // Данные организации будут детерминированно сгенерированы эмулятором Минюста.
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

            string userId = await registrateNewUser(userModel);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Не удалось зарегистрировать тестового пользователя.";
                return RedirectToAction("Index", "Home");
            }

            userModel.Id = userId;

            // useEmulator = true: обращаемся только к эмулятору Минюста
            var link = await UpsertOrganizationAndLinkUserAsync(userModel, useEmulator: true);
            if (!link.Success || string.IsNullOrEmpty(link.OrgId))
            {
                TempData["ErrorMessage"] = "Вход не выполнен: " + (link.ErrorMessage ?? "не удалось сохранить организацию.");
                _logger.LogWarning("Тестовый вход отменён: {Err}", link.ErrorMessage);
                return RedirectToAction("Index", "Home");
            }

            await Authenticate(userModel, link.OrgId);
            _logger.LogInformation("Тестовый вход: {Name} авторизован, org: {Org}", userModel.Name, link.OrgId);
            TempData["SuccessMessage"] = "Вы вошли в тестовом режиме. Данные организации — из эмулятора Минюста.";
            return RedirectToAction("Index", "UserPage");
        }


        private string GenerateCodeVerifier()
        {
            byte[] randomBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Base64UrlEncode(randomBytes);
        }

        private string GenerateCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
                return Base64UrlEncode(challengeBytes);
            }
        }

        private string Base64UrlEncode(byte[] bytes)
        {
            var base64 = Convert.ToBase64String(bytes);
            return base64.Replace("+", "-").Replace("/", "_").Replace("=", "");
        }


        [HttpGet]
        //[Route("signin-esi")]
        public async Task<IActionResult> auth(string code, string state)
        {
            try
            {
                // Verify state and retrieve code verifier from TempData
                if (state != TempData["state"]?.ToString())
                {
                    //return RedirectToAction("Login");
                }

                string codeVerifier = TempData["code_verifier"]?.ToString();

                // Exchange authorization code for access token
                string accessToken = await ExchangeCodeForAccessToken(code, codeVerifier, state);

                //User userModel = new User();

                if (accessToken != null)
                {
                    var userInfo = await GetUserInfoAsync(accessToken);
                    if (userInfo != null)
                    {
                        var userModel = parseUserInfo(userInfo);
                        if (userModel != null && userModel.EsiPin != "")
                        {
                            string userId = await registrateNewUser(userModel);
                            if (string.IsNullOrEmpty(userId))
                            {
                                TempData["ErrorMessage"] = "Не удалось зарегистрировать пользователя после ЕСИ.";
                                return RedirectToAction("Index", "Home");
                            }

                            userModel.Id = userId;

                            // Подтягиваем данные организации из Минюста и привязываем пользователя
                            var link = await UpsertOrganizationAndLinkUserAsync(userModel);
                            if (!link.Success || string.IsNullOrEmpty(link.OrgId))
                            {
                                // Явно выходим — логин не состоялся
                                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                                TempData["ErrorMessage"] =
                                    "Вход не выполнен: " + (link.ErrorMessage ?? "не удалось получить и сохранить данные организации из Минюста.");
                                _logger.LogWarning("ЕСИ-вход отменён для {Name}: {Err}", userModel.Name, link.ErrorMessage);
                                return RedirectToAction("Index", "Home");
                            }

                            await Authenticate(userModel, link.OrgId);
                            _logger.LogInformation("Пользователь {Name} авторизовался через ЕСИ, org: {Org}", userModel.Name, link.OrgId);
                            return RedirectToAction("Index", "UserPage");
                        }
                    }
                }



                TempData["ErrorMessage"] = "Авторизация через ЕСИ не завершена.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в методе auth");
                ViewBag.errorText = "Не удалось авторизоваться, возникла ошибка: " + ex.Message + ", попробуйте еще раз";
                return View("ErrorPage");
            }
        }

        private PublicUser parseUserInfo(string userInfo)
        {
            try
            {
                PublicUser user = new PublicUser();
                dynamic data = JObject.Parse(userInfo);

                user.EsiSub = data.sub;
                user.EsiEmail = data.email;
                user.EsiPin = data.pin;
                user.EsiPhoneNumber = data.phone_number;
                user.EsiCitizenship = data.citizenship;
                user.EsiFamilyName = data.family_name;
                user.EsiGivenName = data.given_name;
                user.EsiName = data.name;
                user.EsiGender = data.gender;
                user.EsiBirthdate = data.birthdate;
                user.EsiEmailVerified = data.email_verified;
                user.EsiPhoneNumberVerified = data.phone_number_verified;

                user.OrganizationTin = data.organization_tin;
                user.OrganizationName = data.organization_name;

                user.Name = data.name;
                user.Email = data.email;

                return user;
            }
            catch (Exception ex)
            {
                throw new Exception("Не удалось парсить ответ ЕСИ на запрос userInfo");
            }
        }

        private async Task Authenticate(PublicUser user, string? orgId = null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.EsiName ?? user.Name ?? ""),
                new Claim(ClaimTypes.Email, user.EsiEmail ?? user.Email ?? ""),
                new Claim(ClaimTypes.DateOfBirth, user.EsiBirthdate ?? ""),
                new Claim("pin", user.EsiPin ?? ""),
                new Claim("orgPin", user.OrganizationTin ?? ""),
                new Claim("orgName", user.OrganizationName ?? ""),
                new Claim("id", user.Id ?? ""),
            };
            if (!string.IsNullOrEmpty(orgId))
            {
                claims.Add(new Claim("orgId", orgId));
            }

            ClaimsIdentity id = new ClaimsIdentity(claims, "ApplicationCookie",
                ClaimsIdentity.DefaultNameClaimType, ClaimsIdentity.DefaultRoleClaimType);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));
        }

        /// <summary>
        /// Результат попытки привязки организации к пользователю.
        /// </summary>
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
                result.ErrorMessage = "У пользователя не указан ИНН организации (ЕСИ не передал поле organization_tin).";
                _logger.LogWarning("Вход без OrganizationTin для {Name}", user.Name);
                return result;
            }

            try
            {
                // 1. Минюст (реальный API или эмулятор)
                var info = await _minJust.GetOrganizationByTinAsync(user.OrganizationTin, useEmulator);
                if (info == null)
                {
                    result.ErrorMessage = $"Не удалось получить сведения об организации по ИНН {user.OrganizationTin} из Минюста.";
                    _logger.LogWarning("Минюст не вернул данные по ИНН {Tin}", user.OrganizationTin);
                    return result;
                }

                // 2. Валидируем код сектора по справочнику (FK organizations_fk).
                //    Если код не найден — используем 'OTHER'. Если и OTHER отсутствует — NULL.
                string? safeSector = null;
                if (!string.IsNullOrWhiteSpace(info.BusinessSector))
                {
                    var exists = await _db.OrganizationBuisnesSectors
                        .AsNoTracking()
                        .AnyAsync(s => s.Id == info.BusinessSector);
                    if (exists)
                    {
                        safeSector = info.BusinessSector;
                    }
                }
                if (safeSector == null)
                {
                    var otherExists = await _db.OrganizationBuisnesSectors
                        .AsNoTracking()
                        .AnyAsync(s => s.Id == "OTHER");
                    if (otherExists) safeSector = "OTHER";
                    _logger.LogInformation(
                        "Сектор '{Code}' не найден в справочнике, использую '{Fallback}'",
                        info.BusinessSector, safeSector ?? "NULL");
                }

                // 3. Upsert организации (Id = ИНН). При повторном входе обновляются ВСЕ поля.
                var now = DateTime.Now;
                var existing = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == info.Tin);
                if (existing == null)
                {
                    await _db.Organizations.AddAsync(new Organization
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
                    // Обновляем ВСЕ реквизиты организации (актуализация из Минюста)
                    if (!string.IsNullOrEmpty(info.FullName))           existing.Fullnamegl         = info.FullName;
                    if (!string.IsNullOrEmpty(info.ShortName))          existing.ShortName          = info.ShortName;
                    if (!string.IsNullOrEmpty(info.LegalForm))          existing.LegalForm          = info.LegalForm;
                    if (!string.IsNullOrEmpty(info.RegistrationNumber)) existing.RegistrationNumber = info.RegistrationNumber;
                    if (!string.IsNullOrEmpty(info.RegistrationDate))   existing.RegistrationDate   = info.RegistrationDate;
                    if (!string.IsNullOrEmpty(info.Address))            existing.Address            = info.Address;
                    if (!string.IsNullOrEmpty(info.DirectorName))       existing.DirectorName       = info.DirectorName;
                    if (!string.IsNullOrEmpty(info.DirectorPosition))   existing.DirectorPosition   = info.DirectorPosition;
                    if (!string.IsNullOrEmpty(info.Status))             existing.Status             = info.Status;
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
                    dbUser.UpdatedAt = DateTime.Now;
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("Организация {Tin} ({Name}) привязана к пользователю {User}",
                    info.Tin, info.FullName, user.Name);

                // обновляем модель пользователя в памяти — чтобы корректно лёг claim orgName
                user.OrganizationName = info.FullName;

                result.Success = true;
                result.OrgId = info.Tin;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при upsert организации для пользователя {Name}", user.Name);
                result.ErrorMessage = "Ошибка при сохранении организации: " + (ex.InnerException?.Message ?? ex.Message);
                return result;
            }
        }


        private async Task<string> registrateNewUser(PublicUser user)
        {
            try
            {
                if (_db.PublicUsers.Where(a => a.EsiPin == user.EsiPin) != null && _db.PublicUsers.Where(a => a.EsiPin == user.EsiPin).Count() > 0)
                {
                    return _db.PublicUsers.FirstOrDefault(a => a.EsiPin == user.EsiPin).Id;
                }
                else
                {
                    //новый пользователь 
                    user.Id = Guid.NewGuid().ToString();
                    user.CreatedAt = DateTime.Now;
                    user.UpdatedAt = DateTime.Now;
                    user.Isdeleted = "0";

                    await _db.PublicUsers.AddAsync(user);
                    await _db.SaveChangesAsync();

                    _logger.LogInformation("Пользователь " + user.Name + ", ИНН: " + user.EsiPin + " успешно зарегистрировался в системе");

                    return user.Id;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в методе registrateNewUser");
                return null;
            }
        }


        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }


        private async Task<string> logoutEsi()
        {
            using (var client = new HttpClient())
            {

                // Генерация строкового представления Basic Authentication
                string authInfo = _appSettings.clientId + ":" + _appSettings.secretKey;
                authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
                string authHeader = "Basic " + authInfo;

                // Добавление заголовка Authorization с Basic Authentication к запросу
                client.DefaultRequestHeaders.Add("Authorization", authHeader);

                var tokenResponse = await client.PostAsync("https://dev-esia.tunduk.kg/connect/token", new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                }));

                var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
                var tokenData = JsonConvert.DeserializeObject<Dictionary<string, string>>(tokenContent);



                if (tokenData != null && tokenData.ContainsKey("access_token"))
                {
                    return tokenData["access_token"];
                }
                else
                {
                    throw new Exception("Не удалось получить токен от ЕСИ");
                }
            }
        }


        private async Task<string> ExchangeCodeForAccessToken(string code, string codeVerifier, string state)
        {
            try
            {
                using (var client = new HttpClient())
                {

                    // Генерация строкового представления Basic Authentication
                    string authInfo = _appSettings.clientId + ":" + _appSettings.secretKey;
                    authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
                    string authHeader = "Basic " + authInfo;

                    // Добавление заголовка Authorization с Basic Authentication к запросу
                    client.DefaultRequestHeaders.Add("Authorization", authHeader);

                    var tokenResponse = await client.PostAsync(_appSettings.tokenUrl, new FormUrlEncodedContent(new[]
                    {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("redirect_uri", _appSettings.redirectUrl),
                    new KeyValuePair<string, string>("code_verifier", codeVerifier),
                }));

                    var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
                    var tokenData = JsonConvert.DeserializeObject<Dictionary<string, string>>(tokenContent);

                    if (tokenData != null && tokenData.ContainsKey("access_token"))
                    {
                        return tokenData["access_token"];
                    }
                    else
                    {
                        throw new Exception("Не удалось получить токен от ЕСИ");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в методе ExchangeCodeForAccessToken");
                return null;
            }
        }


        private async Task<string> GetUserInfoAsync(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync(_appSettings.userInfoUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                // Обработка ошибки получения данных пользователя
                return null;
            }
        }


    }
}
