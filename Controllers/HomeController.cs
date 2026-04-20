using System.Diagnostics;
using System.Globalization;
using data_registry_local.Models;
using data_registry_public.Models;
using data_registry_public.Models.local_models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace data_registry_local.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _db;

        public HomeController(ILogger<HomeController> logger, AppDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        /// <summary>
        /// Главная — дашборд по всем инцидентам с фильтрами.
        /// </summary>
        public async Task<IActionResult> Index(string? period, string? severity, string? sector)
        {
            var all = _db.Incidents.AsNoTracking();
            var query = all.AsQueryable();

            // === фильтры ===
            DateTime since = DateTime.MinValue;
            switch (period)
            {
                case "7":    since = DateTime.Now.AddDays(-7);   break;
                case "30":   since = DateTime.Now.AddDays(-30);  break;
                case "90":   since = DateTime.Now.AddDays(-90);  break;
                case "year": since = DateTime.Now.AddYears(-1);  break;
                default:     since = DateTime.MinValue;          break;
            }
            if (since > DateTime.MinValue)
            {
                query = query.Where(i => i.DateCreate >= since);
            }

            if (!string.IsNullOrWhiteSpace(severity))
            {
                query = query.Where(i => i.Incidentseverity == severity);
            }

            // Сектор — пока инцидент не связан напрямую с Organization, фильтр "про запас":
            // ищем по подстроке в описании (тип инцидента хранится там). На будущее, когда
            // появится FK organization → использовать JOIN.
            if (!string.IsNullOrWhiteSpace(sector))
            {
                query = query.Where(i => i.IncidentDescription != null && i.IncidentDescription.Contains(sector));
            }

            var incidents = await query.ToListAsync();

            var vm = new DashboardViewModel
            {
                Period = period,
                Severity = severity,
                Sector = sector,
                TotalAll = await all.CountAsync(),
                TotalInPeriod = incidents.Count,
                CriticalCount = incidents.Count(i => i.Incidentseverity == "Critical" || i.Incidentseverity == "High"),
                PublishedCount = incidents.Count(i => i.Isdatapublished == true),
                NotifiedCount = incidents.Count(i => i.Aresubjectsnotified == true),
                AffectedRecordsSum = (int)incidents.Sum(i => (decimal?)i.Affectedrecordscount ?? 0m),
                Last30DaysCount = incidents.Count(i => i.DateCreate >= DateTime.Now.AddDays(-30)),
            };

            vm.CriticalPct = vm.TotalInPeriod == 0 ? 0 : Math.Round(100.0 * vm.CriticalCount / vm.TotalInPeriod, 1);
            vm.NotifiedPct = vm.TotalInPeriod == 0 ? 0 : Math.Round(100.0 * vm.NotifiedCount / vm.TotalInPeriod, 1);

            // Распределения
            vm.BySeverity = new Dictionary<string, int>
            {
                ["Critical"] = incidents.Count(i => i.Incidentseverity == "Critical"),
                ["High"]     = incidents.Count(i => i.Incidentseverity == "High"),
                ["Medium"]   = incidents.Count(i => i.Incidentseverity == "Medium"),
                ["Low"]      = incidents.Count(i => i.Incidentseverity == "Low"),
            };

            vm.BySystemType = incidents
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Systemtype) ? "unknown" : i.Systemtype!)
                .ToDictionary(g => g.Key, g => g.Count());

            vm.ByPersonalDataType = new Dictionary<string, int>
            {
                ["Regular"]   = incidents.Count(i => i.Personaldatatype == "Regular"),
                ["Special"]   = incidents.Count(i => i.Personaldatatype == "Special"),
                ["Biometric"] = incidents.Count(i => i.Personaldatatype == "Biometric"),
            };

            vm.BySubjectCategory = incidents
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Datasubjectcategory) ? "Other" : i.Datasubjectcategory!)
                .ToDictionary(g => g.Key, g => g.Count());

            // Временной ряд: последние 12 месяцев
            var cultureRu = CultureInfo.GetCultureInfo("ru-RU");
            var now = DateTime.Now;
            for (int i = 11; i >= 0; i--)
            {
                var month = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                var label = month.ToString("LLL yy", cultureRu);
                vm.MonthlyLabels.Add(char.ToUpper(label[0]) + label[1..]);

                var count = incidents.Count(x => x.DateCreate.HasValue
                    && x.DateCreate.Value.Year == month.Year
                    && x.DateCreate.Value.Month == month.Month);

                var critical = incidents.Count(x => x.DateCreate.HasValue
                    && x.DateCreate.Value.Year == month.Year
                    && x.DateCreate.Value.Month == month.Month
                    && (x.Incidentseverity == "Critical" || x.Incidentseverity == "High"));

                vm.MonthlySeries.Add(count);
                vm.MonthlyCriticalSeries.Add(critical);
            }

            vm.RecentIncidents = incidents
                .OrderByDescending(i => i.DateCreate)
                .Take(8)
                .ToList();

            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
