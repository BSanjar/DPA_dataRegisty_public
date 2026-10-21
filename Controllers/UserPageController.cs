using data_registry_public.Integrations;
using data_registry_public.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace data_registry_public.Controllers
{
    [Authorize]
    public class UserPageController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IMinJustService _minJust;
        private readonly ILogger<UserPageController> _logger;

        public UserPageController(AppDbContext db, IMinJustService minJust, ILogger<UserPageController> logger)
        {
            _db = db;
            _minJust = minJust;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst("id")?.Value;
            var user = string.IsNullOrEmpty(userId)
                ? null
                : await _db.PublicUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

            var orgTin = User.FindFirst("orgId")?.Value ?? User.FindFirst("orgPin")?.Value ?? user?.Organization;

            Organization? org = null;
            MinJustOrganizationInfo? mj = null;
            if (!string.IsNullOrEmpty(orgTin))
            {
                org = await _db.Organizations.AsNoTracking()
                    .Include(o => o.BusinesssectorNavigation)
                    .FirstOrDefaultAsync(o => o.Id == orgTin);
                mj = await _minJust.GetOrganizationByTinAsync(orgTin);
                // Минюст не ответил / не нашёл — показываем реквизиты из последней синхронизации,
                // а если их нет — хотя бы ИНН организации (название в карточке заменится на «ИНН …»).
                if (mj == null && org != null)
                {
                    mj = MinJustOrganizationInfo.FromOrganization(org);
                }
                mj ??= new MinJustOrganizationInfo { Tin = orgTin };
            }

            int totalIncidents = 0;
            if (!string.IsNullOrEmpty(orgTin))
            {
                totalIncidents = await _db.Incidents.AsNoTracking().CountAsync(i => i.Organization == orgTin);
            }

            ViewBag.MinJustOrg = mj;
            ViewBag.OrganizationDb = org;
            ViewBag.TotalIncidents = totalIncidents;
            return View(user);
        }
    }
}
