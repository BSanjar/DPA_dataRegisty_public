using data_registry_public.Infrastructure.Hosting;
using data_registry_public.Integrations;
using data_registry_public.Models;
using data_registry_public.Models.local_models;
using data_registry_public.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace data_registry_public.Controllers
{
    /// <summary>
    /// Инциденты с персональными данными.
    /// Доступ только для авторизованных пользователей.
    /// Фильтрация "свои" — по ИНН организации из claim orgId.
    /// </summary>
    [Authorize]
    public class IncidentsController : Controller
    {
        /// <summary>
        /// DEV-заглушка ИНН для неавторизованных сценариев.
        /// </summary>

        private const string OwnIncidentsCookie = "dp_own_incidents";

        private readonly AppDbContext _db;
        private readonly OrganizationDetailsService _orgDetails;
        private readonly ILogger<IncidentsController> _logger;

        public IncidentsController(AppDbContext db, OrganizationDetailsService orgDetails, ILogger<IncidentsController> logger)
        {
            _db = db;
            _orgDetails = orgDetails;
            _logger = logger;
        }

        // === СПИСОК (только свои) =========================================

        public async Task<IActionResult> Index(string? q, string? severity, string? period)
        {
            var query = _db.Incidents.AsNoTracking().AsQueryable();

            // Приоритет: авторизованный пользователь → фильтр по его организации.
            // Без авторизации — фильтр по cookie (dev-режим).
            var orgTin = GetCurrentOrgTin();
            if (!string.IsNullOrEmpty(orgTin))
            {
                query = query.Where(i => i.Organization == orgTin);
            }
            else
            {
                var ownIds = GetOwnIncidentIds();
                if (ownIds.Count == 0)
                {
                    query = query.Where(i => false);
                }
                else
                {
                    query = query.Where(i => ownIds.Contains(i.Id));
                }
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(i =>
                    (i.IncidentRegnumber != null && i.IncidentRegnumber.Contains(term)) ||
                    (i.IncidentDescription != null && i.IncidentDescription.Contains(term)) ||
                    (i.IncidetLocation != null && i.IncidetLocation.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(severity))
            {
                query = query.Where(i => i.Incidentseverity == severity);
            }

            if (!string.IsNullOrWhiteSpace(period))
            {
                DateTime since = DateTime.Now;
                switch (period)
                {
                    case "7": since = DateTime.Now.AddDays(-7); break;
                    case "30": since = DateTime.Now.AddDays(-30); break;
                    case "90": since = DateTime.Now.AddDays(-90); break;
                    case "year": since = DateTime.Now.AddYears(-1); break;
                    default: since = DateTime.MinValue; break;
                }
                if (since > DateTime.MinValue)
                {
                    query = query.Where(i => i.DateCreate >= since);
                }
            }

            var list = await query.OrderByDescending(i => i.DateCreate).ToListAsync();

            ViewBag.CurrentOrg = (await ResolveCurrentOrganizationAsync())?.Info;
            ViewBag.FilterQuery = q;
            ViewBag.FilterSeverity = severity;
            ViewBag.FilterPeriod = period;

            // "всего моих" считаем по той же логике
            if (!string.IsNullOrEmpty(orgTin))
            {
                ViewBag.TotalOwn = await _db.Incidents.AsNoTracking().CountAsync(i => i.Organization == orgTin);
            }
            else
            {
                ViewBag.TotalOwn = GetOwnIncidentIds().Count;
            }

            return View(list);
        }

        // === ПРОСМОТР КАРТОЧКИ =============================================

        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var incident = await _db.Incidents
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);

            if (incident == null) return NotFound();

            // Доступ только к своим:
            //  — авторизованный пользователь: инцидент должен принадлежать его организации
            //  — dev-режим: id должен быть в cookie "мои"
            var orgTin = GetCurrentOrgTin();
            if (!string.IsNullOrEmpty(orgTin))
            {
                if (incident.Organization != orgTin)
                {
                    return Forbid();
                }
            }
            else
            {
                if (!GetOwnIncidentIds().Contains(id))
                {
                    return Forbid();
                }
            }

            ViewBag.CurrentOrg = (await ResolveCurrentOrganizationAsync())?.Info;
            return View(incident);
        }

        // === МАСТЕР СОЗДАНИЯ ===============================================

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Реквизиты организации — из Минюста по ИНН из ЕСИ. Если Минюст ИНН не знает,
            // представитель указывает их на шаге 1 (ИНН не меняется).
            var details = await ResolveCurrentOrganizationAsync();

            var model = new IncidentCreateViewModel
            {
                DateDetection = DateTime.Now,
                DateOccurrence = DateTime.Now.AddHours(-1),
            };
            var me = await GetCurrentUserContactAsync();
            model.ResponsibleName = me.Name;
            model.ResponsiblePosition = me.Position;
            model.ResponsiblePhone = me.Phone;
            model.ResponsibleEmail = me.Email;

            if (details != null)
            {
                FillOrganizationFields(model, details.Info);
                if (details.IsEditable)
                {
                    model.OrgManual = OrganizationDetailsService.ToInput(details.Info);
                }
            }
            else
            {
                TempData["ErrorMessage"] = "К вашей учётной записи не привязана организация. Войдите через ЕСИ как представитель организации.";
            }

            await PrepareCreateViewAsync(details, me);
            return View(model);
        }

        /// <summary>Данные для страницы мастера: организация, справочник секторов (для ручного ввода), ответственный.</summary>
        private async Task PrepareCreateViewAsync(OrganizationDetails? details, ContactInfo? me = null)
        {
            ViewBag.CurrentOrg = details?.Info;
            ViewBag.OrgDetails = details;
            if (details?.IsEditable == true)
            {
                ViewBag.OrgSectors = await _orgDetails.GetSectorsAsync();
            }
            ViewBag.ResponsibleMe = me ?? await GetCurrentUserContactAsync();
        }

        private static void FillOrganizationFields(IncidentCreateViewModel model, MinJustOrganizationInfo org)
        {
            model.OrganizationTin = org.Tin;
            model.OrganizationName = org.FullName;
            model.OrganizationShortName = org.ShortName;
            model.OrganizationLegalForm = org.LegalForm;
            model.OrganizationRegNumber = org.RegistrationNumber;
            model.OrganizationRegDate = org.RegistrationDate;
            model.OrganizationAddress = org.Address;
            model.OrganizationDirector = org.DirectorName;
            model.OrganizationDirectorPosition = org.DirectorPosition;
            model.OrganizationSector = org.BusinessSector;
            model.OrganizationSectorName = org.BusinessSectorName;
        }

        /// <summary>Контактные данные вошедшего пользователя — для блока «Ответственное лицо».</summary>
        public sealed record ContactInfo(string? Name, string? Position, string? Phone, string? Email);

        /// <summary>
        /// Ответственный по умолчанию — вошедший пользователь: ФИО и email из claims ЕСИ,
        /// телефон и должность — из его записи public_users. Поля на форме можно изменить.
        /// </summary>
        private async Task<ContactInfo> GetCurrentUserContactAsync()
        {
            var name = User.Identity?.Name;
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

            var userId = User.FindFirst("id")?.Value;
            var user = string.IsNullOrEmpty(userId) ? null : await _db.PublicUsers.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.EsiName, u.Name, u.EsiEmail, u.Email, u.EsiPhoneNumber, u.PositionName })
                .FirstOrDefaultAsync();

            return new ContactInfo(
                FirstNonEmpty(name, user?.EsiName, user?.Name),
                FirstNonEmpty(user?.PositionName),
                FirstNonEmpty(user?.EsiPhoneNumber),
                FirstNonEmpty(email, user?.EsiEmail, user?.Email));
        }

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(IncidentCreateViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            // Организация (ИНН и реквизиты Минюста) всегда берётся с сервера, а не из формы,
            // чтобы её нельзя было подделать со страницы. С формы — только ручные реквизиты, если Минюст ИНН не знает.
            var details = await ResolveCurrentOrganizationAsync();
            if (details == null)
            {
                TempData["ErrorMessage"] = "Невозможно зарегистрировать инцидент: к учётной записи не привязана организация.";
                return RedirectToAction(nameof(Create));
            }
            var org = details.Info;

            // Поля организации read-only — убираем их из ModelState, чтоб не мешали валидации
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Organization")).ToList())
            {
                ModelState.Remove(key);
            }

            if (!details.IsEditable)
            {
                foreach (var key in ModelState.Keys.Where(k => k.StartsWith(nameof(IncidentCreateViewModel.OrgManual))).ToList())
                {
                    ModelState.Remove(key);
                }
                model.OrgManual = null;
            }
            else if (model.OrgManual == null)
            {
                model.OrgManual = OrganizationDetailsService.ToInput(org);
                ModelState.AddModelError("OrgManual.FullName", "Заполните сведения об организации");
            }
            else if (!string.IsNullOrWhiteSpace(model.OrgManual.BusinessSector)
                     && !await _orgDetails.SectorExistsAsync(model.OrgManual.BusinessSector.Trim()))
            {
                ModelState.AddModelError("OrgManual.BusinessSector", "Выберите сферу деятельности из списка");
            }

            if (!ModelState.IsValid)
            {
                // подставим данные организации, т.к. на форме они read-only и могут не прийти
                FillOrganizationFields(model, org);
                await PrepareCreateViewAsync(details);
                TempData["ErrorMessage"] = "Форма заполнена с ошибками. Проверьте подсвеченные поля.";
                return View("Create", model);
            }

            var id = Guid.NewGuid().ToString();
            var regNumber = GenerateRegNumber();

            var incident = new Incident
            {
                Id = id,
                DateCreate = DateTime.Now,
                IncidentRegnumber = regNumber,
                // Привязка к организации (для авторизованных — orgId из claim, иначе dev-ИНН)
                Organization = org.Tin,
                DateDetection = model.DateDetection,
                DateOccurrence = model.DateOccurrence,
                IncidetLocation = model.IncidentLocation,
                IncidentDescription = model.IncidentDescription,
                IncidentType = null, // FK на справочник — пока не используем
                Affectedrecordscount = model.AffectedRecordsCount,
                Isdatacopiedorstolen = model.IsDataCopiedOrStolen,
                Isdatapublished = model.IsDataPublished,
                Hasriskofharmtosubjects = model.HasRiskOfHarmToSubjects,
                Aresubjectsnotified = model.AreSubjectsNotified,
                Affectedinformationsystem = model.AffectedInformationSystem,
                Systemtype = model.SystemType,
                Suspectedincidentcause = model.SuspectedCause,
                Actionstaken = model.ActionsTaken,
                Preventionmeasures = model.PreventionMeasures,
                Investigationinfo = model.InvestigationInfo,
                Incidentseverity = model.IncidentSeverity,
                Notificationsentat = model.NotificationSentAt,
                Personaldatacategories = model.PersonalDataCategories,
                Personaldatatype = model.PersonalDataType,
                Estimateddatasubjectscount = model.EstimatedDataSubjectsCount,
                Datasubjectcategory = model.DataSubjectCategory,
                ResponsibleName = Clean(model.ResponsibleName),
                ResponsiblePosition = Clean(model.ResponsiblePosition),
                ResponsiblePhone = Clean(model.ResponsiblePhone),
                ResponsibleEmail = Clean(model.ResponsibleEmail),
            };

            // Сохраняем тип инцидента до появления справочника в UI — в префикс описания.
            if (!string.IsNullOrWhiteSpace(model.IncidentType))
            {
                incident.IncidentDescription = $"[{model.IncidentType}] " + incident.IncidentDescription;
            }

            try
            {
                // Ручные реквизиты и инцидент сохраняются вместе: либо оба, либо ничего.
                await using var tx = await _db.Database.BeginTransactionAsync();
                if (details.IsEditable && model.OrgManual != null)
                {
                    org = await _orgDetails.SaveManualAsync(org.Tin, model.OrgManual, User.FindFirst("id")?.Value);
                }

                await _db.Incidents.AddAsync(incident);

                // Контакт организации по инцидентам = последний указанный ответственный (его видит админка).
                var orgEntity = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == org.Tin);
                if (orgEntity is not null)
                {
                    orgEntity.IncidentContactPerson = incident.ResponsibleName;
                    orgEntity.IncidentContactPosition = incident.ResponsiblePosition;
                    orgEntity.IncidentContactPhone = incident.ResponsiblePhone;
                    orgEntity.IncidentContactEmail = incident.ResponsibleEmail;
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                _logger.LogInformation("Инцидент {Id} зарегистрирован: организация {Org}, тяжесть {Severity}, субъектов ~{Subjects}",
                    incident.Id, org.Tin, model.IncidentSeverity ?? "-", model.EstimatedDataSubjectsCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось сохранить инцидент");
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = "Не удалось сохранить инцидент. Попробуйте ещё раз.";
                FillOrganizationFields(model, details.Info);
                await PrepareCreateViewAsync(details);
                return View("Create", model);
            }

            if (details.IsEditable)
            {
                await SessionClaims.UpdateOrgNameAsync(HttpContext, org.FullName);
            }

            AppendOwnIncidentId(id);

            TempData["SuccessMessage"] =
                $"Инцидент успешно зарегистрирован. Номер: {regNumber}";
            TempData["HighlightIncidentId"] = id;

            return RedirectToAction(nameof(Index));
        }

        // === Организация текущего пользователя ===========================

        /// <summary>
        /// Организация вошедшего пользователя: ИНН из сессии (claims orgId/orgPin), иначе из его профиля
        /// (public_users.organization). Никаких заглушек и cookie — иначе на проде можно было бы
        /// зарегистрировать инцидент от имени чужой (тестовой) организации.
        /// </summary>
        private async Task<OrganizationDetails?> ResolveCurrentOrganizationAsync()
        {
            var tin = GetCurrentOrgTin();
            if (string.IsNullOrWhiteSpace(tin))
            {
                var userId = User.FindFirst("id")?.Value;
                tin = string.IsNullOrEmpty(userId) ? null : await _db.PublicUsers.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.Organization ?? u.OrganizationTin)
                    .FirstOrDefaultAsync();
            }
            if (string.IsNullOrWhiteSpace(tin))
            {
                _logger.LogWarning("Пользователь {UserId} без организации — регистрация инцидента недоступна", User.FindFirst("id")?.Value);
                return null;
            }

            return await _orgDetails.GetAsync(tin.Trim());
        }

        /// <summary>
        /// ИНН организации текущего пользователя из claims (orgId или orgPin).
        /// Возвращает null, если пользователь не авторизован.
        /// </summary>
        private string? GetCurrentOrgTin()
        {
            if (User?.Identity?.IsAuthenticated != true) return null;
            var tin = User.FindFirst("orgId")?.Value;
            if (string.IsNullOrWhiteSpace(tin))
                tin = User.FindFirst("orgPin")?.Value;
            return string.IsNullOrWhiteSpace(tin) ? null : tin;
        }

        // === Cookie-helpers ===============================================

        private List<string> GetOwnIncidentIds()
        {
            var raw = Request.Cookies[OwnIncidentsCookie];
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        private void AppendOwnIncidentId(string id)
        {
            var list = GetOwnIncidentIds();
            if (!list.Contains(id)) list.Insert(0, id);
            if (list.Count > 500) list = list.Take(500).ToList();

            Response.Cookies.Append(OwnIncidentsCookie, string.Join(',', list), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.Now.AddYears(1),
                IsEssential = true
            });
        }

        private static string GenerateRegNumber()
            => "INC-" + DateTime.Now.ToString("yyyyMMdd") + "-" + Random.Shared.Next(1000, 9999);
    }
}
