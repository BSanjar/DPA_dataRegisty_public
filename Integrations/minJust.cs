using System.Globalization;

namespace data_registry_public.Integrations
{
    /// <summary>
    /// DTO с данными организации, получаемыми из Министерства юстиции
    /// </summary>
    public class MinJustOrganizationInfo
    {
        public string Tin { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string LegalForm { get; set; } = string.Empty;
        public string RegistrationNumber { get; set; } = string.Empty;
        public string RegistrationDate { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string DirectorName { get; set; } = string.Empty;
        public string DirectorPosition { get; set; } = string.Empty;
        public string BusinessSector { get; set; } = string.Empty;
        public string BusinessSectorName { get; set; } = string.Empty;
        public string Status { get; set; } = "Активный";
        public string Source { get; set; } = "MinJust Emulator";
    }

    public interface IMinJustService
    {
        Task<MinJustOrganizationInfo?> GetOrganizationByTinAsync(string tin);
    }

    /// <summary>
    /// Клиент к API Министерства юстиции.
    /// Пока API недоступно — используется эмулятор, который
    /// генерирует детерминированные данные по ИНН.
    /// </summary>
    public class MinJustService : IMinJustService
    {
        private readonly ILogger<MinJustService> _logger;

        public MinJustService(ILogger<MinJustService> logger)
        {
            _logger = logger;
        }

        public Task<MinJustOrganizationInfo?> GetOrganizationByTinAsync(string tin)
        {
            if (string.IsNullOrWhiteSpace(tin))
            {
                return Task.FromResult<MinJustOrganizationInfo?>(null);
            }

            tin = tin.Trim();

            // минимальная проверка: ИНН — 14 цифр (форма KG)
            if (tin.Length < 8 || !tin.All(char.IsDigit))
            {
                _logger.LogWarning("MinJust эмулятор: некорректный ИНН '{Tin}'", tin);
                return Task.FromResult<MinJustOrganizationInfo?>(null);
            }

            var info = BuildEmulatedInfo(tin);
            _logger.LogInformation("MinJust эмулятор вернул данные по ИНН {Tin}: {Name}", tin, info.FullName);
            return Task.FromResult<MinJustOrganizationInfo?>(info);
        }

        private static MinJustOrganizationInfo BuildEmulatedInfo(string tin)
        {
            // детерминированный генератор на базе ИНН
            int seed = 0;
            foreach (var ch in tin) seed = unchecked(seed * 31 + ch);
            var rnd = new Random(seed);

            var sectors = new (string code, string name)[]
            {
                ("Government",       "Государственные органы"),
                ("Telecommunications","Телекоммуникации"),
                ("Healthcare",       "Медицина"),
                ("Banking",          "Банки и финансы"),
                ("Education",        "Образование"),
                ("Insurance",        "Страхование"),
                ("Retail",           "Торговля"),
                ("IT",               "IT / технологии"),
                ("Manufacturing",    "Производство"),
                ("Energy",           "Энергетика"),
                ("Transportation",   "Транспорт / логистика"),
                ("Agriculture",      "Сельское хозяйство"),
                ("Media",            "СМИ"),
                ("Hospitality",      "Туризм / гостиницы"),
                ("Other",            "Другое"),
            };

            var forms = new[] { "ОсОО", "ОАО", "ЗАО", "ГП", "ЧП", "ИП" };
            var cities = new[] { "г. Бишкек", "г. Ош", "г. Токмок", "г. Каракол", "г. Жалал-Абад", "г. Баткен" };
            var streets = new[] { "ул. Чуй", "пр. Манаса", "ул. Ибраимова", "ул. Киевская", "ул. Токтогула", "ул. Байтик Баатыра" };
            var firstNames = new[] { "Асан", "Бакыт", "Нурлан", "Эркин", "Азамат", "Улан", "Данияр", "Марат" };
            var lastNames = new[] { "Абдыкадыров", "Осмонов", "Турсунов", "Бекмуратов", "Жумагулов", "Токтосунов" };
            var positions = new[] { "Генеральный директор", "Директор", "Руководитель", "Председатель" };

            var sector = sectors[rnd.Next(sectors.Length)];
            var form = forms[rnd.Next(forms.Length)];
            var name = form switch
            {
                "ИП" => $"ИП {lastNames[rnd.Next(lastNames.Length)]} {firstNames[rnd.Next(firstNames.Length)][0]}.",
                _ => $"{form} «{GenerateCompanyName(rnd)}»"
            };

            var regYear = 1995 + rnd.Next(0, 29);
            var regMonth = rnd.Next(1, 13);
            var regDay = rnd.Next(1, 28);
            var regDate = new DateTime(regYear, regMonth, regDay);

            return new MinJustOrganizationInfo
            {
                Tin = tin,
                FullName = name,
                ShortName = form == "ИП" ? name : name.Replace(form, form).Trim(),
                LegalForm = form,
                RegistrationNumber = rnd.Next(100000, 999999).ToString(CultureInfo.InvariantCulture),
                RegistrationDate = regDate.ToString("dd.MM.yyyy"),
                Address = $"{cities[rnd.Next(cities.Length)]}, {streets[rnd.Next(streets.Length)]}, д. {rnd.Next(1, 250)}",
                DirectorName = $"{lastNames[rnd.Next(lastNames.Length)]} {firstNames[rnd.Next(firstNames.Length)]}",
                DirectorPosition = positions[rnd.Next(positions.Length)],
                BusinessSector = sector.code,
                BusinessSectorName = sector.name,
                Status = "Активный",
                Source = "MinJust Emulator (dev)"
            };
        }

        private static string GenerateCompanyName(Random rnd)
        {
            var prefixes = new[] { "Ала", "Ак", "Кыргыз", "Манас", "Иссык", "Бишкек", "Тянь", "Алтын", "Эл", "Эне" };
            var suffixes = new[] { "Строй", "Тех", "Софт", "Сервис", "Трейд", "Инвест", "Групп", "Лайн", "Плюс", "Проект" };
            return $"{prefixes[rnd.Next(prefixes.Length)]}{suffixes[rnd.Next(suffixes.Length)]}";
        }
    }
}
