using System.Collections.Generic;

namespace data_registry_public.Models.local_models
{
    /// <summary>
    /// Модель данных для главной страницы — дашборд по всем инцидентам.
    /// </summary>
    public class DashboardViewModel
    {
        // === фильтры (прокидываются в форму обратно) ===
        public string? Period { get; set; }
        public string? Severity { get; set; }
        public string? Sector { get; set; }

        // === KPI ===
        public int TotalAll { get; set; }
        public int TotalInPeriod { get; set; }
        public int CriticalCount { get; set; }
        public int PublishedCount { get; set; }
        public int NotifiedCount { get; set; }
        public int AffectedRecordsSum { get; set; }
        public int Last30DaysCount { get; set; }
        public double CriticalPct { get; set; }
        public double NotifiedPct { get; set; }

        // === распределения для графиков ===
        public Dictionary<string, int> BySeverity { get; set; } = new();
        public Dictionary<string, int> BySystemType { get; set; } = new();
        public Dictionary<string, int> ByPersonalDataType { get; set; } = new();
        public Dictionary<string, int> BySubjectCategory { get; set; } = new();

        /// <summary>Месяцы за последние 12 месяцев (включая текущий), в формате "yyyy-MM".</summary>
        public List<string> MonthlyLabels { get; set; } = new();
        public List<int> MonthlySeries { get; set; } = new();
        public List<int> MonthlyCriticalSeries { get; set; } = new();

        // === последние инциденты (обезличенные, для ленты) ===
        public List<Incident> RecentIncidents { get; set; } = new();
    }
}
