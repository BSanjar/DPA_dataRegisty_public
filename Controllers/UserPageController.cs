using data_registry_public.Infrastructure.Hosting;
using data_registry_public.Models;
using data_registry_public.Models.local_models;
using data_registry_public.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace data_registry_public.Controllers
{
    [Authorize]
    public class UserPageController : Controller
    {
        private readonly AppDbContext _db;
        private readonly OrganizationDetailsService _orgDetails;
        private readonly ILogger<UserPageController> _logger;

        public UserPageController(AppDbContext db, OrganizationDetailsService orgDetails, ILogger<UserPageController> logger)
        {
            _db = db;
            _orgDetails = orgDetails;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var user = await CurrentUserAsync();
            var orgTin = CurrentOrgTin(user);

            Organization? org = null;
            OrganizationDetails? details = null;
            int totalIncidents = 0;
            if (!string.IsNullOrEmpty(orgTin))
            {
                org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgTin);
                details = await _orgDetails.GetAsync(orgTin);
                totalIncidents = await _db.Incidents.AsNoTracking().CountAsync(i => i.Organization == orgTin);
            }

            ViewBag.MinJustOrg = details?.Info;
            ViewBag.OrgDetails = details;
            ViewBag.OrganizationDb = org;
            ViewBag.TotalIncidents = totalIncidents;
            return View(user);
        }

        // === Реквизиты организации, указываемые вручную (Минюст не знает ИНН) ===

        [HttpGet]
        public async Task<IActionResult> Organization()
        {
            var details = await EditableDetailsAsync();
            if (details == null) return RedirectToAction(nameof(Index));

            ViewBag.OrgDetails = details;
            ViewBag.OrgSectors = await _orgDetails.GetSectorsAsync();
            return View(OrganizationDetailsService.ToInput(details.Info));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Organization(OrganizationDetailsInput model)
        {
            var details = await EditableDetailsAsync();
            if (details == null) return RedirectToAction(nameof(Index));

            if (!string.IsNullOrWhiteSpace(model.BusinessSector) && !await _orgDetails.SectorExistsAsync(model.BusinessSector.Trim()))
            {
                ModelState.AddModelError(nameof(model.BusinessSector), "Выберите сферу деятельности из списка");
            }
            if (!ModelState.IsValid)
            {
                ViewBag.OrgDetails = details;
                ViewBag.OrgSectors = await _orgDetails.GetSectorsAsync();
                TempData["ErrorMessage"] = "Форма заполнена с ошибками. Проверьте подсвеченные поля.";
                return View(model);
            }

            var saved = await _orgDetails.SaveManualAsync(details.Info.Tin, model, User.FindFirst("id")?.Value);
            await SessionClaims.UpdateOrgNameAsync(HttpContext, saved.FullName);
            TempData["SuccessMessage"] = "Сведения об организации сохранены.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Реквизиты организации пользователя, если их можно указать вручную; иначе null (с сообщением).</summary>
        private async Task<OrganizationDetails?> EditableDetailsAsync()
        {
            var tin = CurrentOrgTin(await CurrentUserAsync());
            if (string.IsNullOrEmpty(tin))
            {
                TempData["ErrorMessage"] = "К вашей учётной записи не привязана организация.";
                return null;
            }

            var details = await _orgDetails.GetAsync(tin);
            if (!details.IsEditable)
            {
                _logger.LogInformation("Попытка изменить реквизиты организации {Tin}, полученные из Минюста", tin);
                TempData["ErrorMessage"] = "Сведения об организации получены из Минюста и изменяются только там.";
                return null;
            }
            return details;
        }

        private async Task<PublicUser?> CurrentUserAsync()
        {
            var userId = User.FindFirst("id")?.Value;
            return string.IsNullOrEmpty(userId)
                ? null
                : await _db.PublicUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        }

        private string? CurrentOrgTin(PublicUser? user)
        {
            var tin = User.FindFirst("orgId")?.Value ?? User.FindFirst("orgPin")?.Value ?? user?.Organization;
            return string.IsNullOrWhiteSpace(tin) ? null : tin.Trim();
        }
    }
}
