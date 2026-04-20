using System.Text.Json;
using data_registry_public.Integrations;
using data_registry_public.Models;
using data_registry_public.Models.local_models;
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
    public class IncedentsController : Controller
    {
        /// <summary>
        /// DEV-заглушка ИНН для неавторизованных сценариев.
        /// </summary>
        private const string DevCurrentOrgTin = "01001202210023";

        private const string OwnIncidentsCookie = "dp_own_incidents";
        private const string CurrentOrgCookie = "dp_current_org";

        private readonly AppDbContext _db;
        private readonly IMinJustService _minJust;
        private readonly ILogger<IncedentsController> _logger;

        public IncedentsController(AppDbContext db, IMinJustService minJust, ILogger<IncedentsController> logger)
        {
            _db = db;
            _minJust = minJust;
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

            ViewBag.CurrentOrg = await ResolveCurrentOrganizationAsync();
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

            ViewBag.CurrentOrg = await ResolveCurrentOrganizationAsync();
            return View(incident);
        }

        // === МАСТЕР СОЗДАНИЯ ===============================================

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Данные организации автоматически подтягиваем из Минюста при входе,
            // никаких кнопок. ИНН берётся:
            //   — после авторизации: из claims (orgPin)
            //   — сейчас (dev): из cookie или фиксированная заглушка
            var org = await ResolveCurrentOrganizationAsync();

            var model = new IncidentCreateViewModel
            {
                DateDetection = DateTime.Now,
                DateOccurrence = DateTime.Now.AddHours(-1),
            };

            if (org != null)
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
            else
            {
                TempData["ErrorMessage"] = "Не удалось получить данные организации из Минюста. Обратитесь к администратору.";
            }

            ViewBag.CurrentOrg = org;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(IncidentCreateViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            // организация всегда берётся с сервера (не из формы),
            // чтобы её нельзя было подделать со страницы.
            var org = await ResolveCurrentOrganizationAsync();
            if (org == null)
            {
                TempData["ErrorMessage"] = "Невозможно зарегистрировать инцидент: не удалось получить данные организации.";
                return RedirectToAction(nameof(Create));
            }

            // Поля организации read-only — убираем их из ModelState, чтоб не мешали валидации
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Organization")).ToList())
            {
                ModelState.Remove(key);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CurrentOrg = org;
                // подставим данные организации, т.к. на форме они read-only и могут не прийти
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
            };

            // Сохраняем тип инцидента до появления справочника в UI — в префикс описания.
            if (!string.IsNullOrWhiteSpace(model.IncidentType))
            {
                incident.IncidentDescription = $"[{model.IncidentType}] " + incident.IncidentDescription;
            }

            try
            {
                await _db.Incidents.AddAsync(incident);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось сохранить инцидент");
                TempData["ErrorMessage"] = "Не удалось сохранить инцидент: " + ex.Message;
                ViewBag.CurrentOrg = org;
                return View("Create", model);
            }

            AppendOwnIncidentId(id);
            StoreCurrentOrgCookie(org);

            TempData["SuccessMessage"] =
                $"Инцидент успешно зарегистрирован. Номер: {regNumber}";
            TempData["HighlightIncidentId"] = id;

            return RedirectToAction(nameof(Index));
        }

        // === Получение организации (dev + будущий auth) ====================

        private async Task<MinJustOrganizationInfo?> ResolveCurrentOrganizationAsync()
        {
            var tin = GetCurrentOrgTin();

            // dev: cookie от предыдущего сеанса
            if (string.IsNullOrWhiteSpace(tin))
            {
                tin = ReadCurrentOrgFromCookie()?.Tin;
            }

            // dev: фиксированная заглушка — чтобы при первом заходе организация
            // тоже автоматически подтянулась
            if (string.IsNullOrWhiteSpace(tin))
            {
                tin = DevCurrentOrgTin;
            }

            return await _minJust.GetOrganizationByTinAsync(tin);
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

        private void StoreCurrentOrgCookie(MinJustOrganizationInfo org)
        {
            var json = JsonSerializer.Serialize(org);
            Response.Cookies.Append(
                CurrentOrgCookie,
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)),
                new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.Now.AddYears(1),
                    IsEssential = true
                });
        }

        private MinJustOrganizationInfo? ReadCurrentOrgFromCookie()
        {
            var raw = Request.Cookies[CurrentOrgCookie];
            if (string.IsNullOrWhiteSpace(raw)) return null;
            try
            {
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(raw));
                return JsonSerializer.Deserialize<MinJustOrganizationInfo>(json);
            }
            catch
            {
                return null;
            }
        }

        private static string GenerateRegNumber()
            => "INC-" + DateTime.Now.ToString("yyyyMMdd") + "-" + Random.Shared.Next(1000, 9999);
    }
}
