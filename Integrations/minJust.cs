using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using data_registry_public.Models.local_models;
using Microsoft.Extensions.Options;

namespace data_registry_public.Integrations
{
    /// <summary>
    /// Ответ сервиса Минюста по ИНН (упрощённая модель).
    /// Маппится на JSON с API getSubjectByQueryRequest.
    /// </summary>
    public class MinJustResponce
    {
        public int status { get; set; } = 1;
        public string? message { get; set; }

        [JsonPropertyName("subject")]
        public MinJustSubject? Subject { get; set; }

        // На некоторых контурах API возвращает поля в корне. Оставляем универсальность.
        public string? tin { get; set; }
        public string? name { get; set; }
        public string? shortname { get; set; }
        public string? address { get; set; }
        public string? director { get; set; }
        public string? directorposition { get; set; }
        public string? legalform { get; set; }
        public string? registrationnumber { get; set; }
        public string? registrationdate { get; set; }
    }

    public class MinJustSubject
    {
        public string? tin { get; set; }
        public string? name { get; set; }
        public string? shortname { get; set; }
        public string? address { get; set; }
        public string? director { get; set; }
        public string? directorposition { get; set; }
        public string? legalform { get; set; }
        public string? registrationnumber { get; set; }
        public string? registrationdate { get; set; }
        public string? status { get; set; }
    }

    /// <summary>
    /// DTO с данными организации (нормализованный формат).
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
        public string Source { get; set; } = "MinJust";
    }

    public interface IMinJustService
    {
        /// <summary>
        /// Получить данные организации по ИНН.
        /// </summary>
        /// <param name="tin">ИНН организации</param>
        /// <param name="forceEmulator">принудительно использовать эмулятор (для тестовых сценариев)</param>
        Task<MinJustOrganizationInfo?> GetOrganizationByTinAsync(string tin, bool forceEmulator = false);
    }

    /// <summary>
    /// Клиент к API Министерства юстиции (POST JSON).
    /// Если API недоступен или вернул ошибку — автоматически используется
    /// локальный эмулятор, детерминированно генерирующий данные по ИНН.
    /// </summary>
    public class MinJustService : IMinJustService
    {
        private readonly ILogger<MinJustService> _logger;
        private readonly IHttpClientFactory _httpFactory;
        private readonly AppSettings _appSettings;

        public MinJustService(
            ILogger<MinJustService> logger,
            IHttpClientFactory httpFactory,
            IOptions<AppSettings> appSettings)
        {
            _logger = logger;
            _httpFactory = httpFactory;
            _appSettings = appSettings.Value;
        }

        public async Task<MinJustOrganizationInfo?> GetOrganizationByTinAsync(string tin, bool forceEmulator = false)
        {
            if (string.IsNullOrWhiteSpace(tin)) return null;
            tin = tin.Trim();

            // === 1. Реальный вызов API Минюста (если не запрошен эмулятор) ===
            if (!forceEmulator && !string.IsNullOrWhiteSpace(_appSettings.minJustApi))
            {
                try
                {
                    using var http = _httpFactory.CreateClient();
                    http.Timeout = TimeSpan.FromSeconds(6);

                    var request = new { query = tin, type = "TIN" };
                    var response = await http.PostAsJsonAsync(_appSettings.minJustApi, request);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("Ответ Минюста по ИНН {Tin}: {Len} байт", tin, json.Length);

                        var parsed = TryMapFromApi(json, tin);
                        if (parsed != null)
                        {
                            parsed.Source = "MinJust API";
                            return parsed;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Минюст вернул статус {Status} по ИНН {Tin}", response.StatusCode, tin);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Сбой запроса в Минюст по ИНН {Tin}, переключаюсь на эмулятор", tin);
                }
            }

            // === 2. Fallback — эмулятор (dev) ===
            if (tin.Length < 8 || !tin.All(char.IsDigit))
            {
                return null;
            }
            var emu = BuildEmulatedInfo(tin);
            emu.Source = "MinJust Emulator (dev)";
            return emu;
        }

        /// <summary>Пытается распарсить разные форматы ответа API.</summary>
        private static MinJustOrganizationInfo? TryMapFromApi(string json, string tin)
        {
            try
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<MinJustResponce>(json, opts);
                if (parsed == null) return null;

                var subj = parsed.Subject;
                string? name = subj?.name ?? parsed.name;
                if (string.IsNullOrWhiteSpace(name)) return null;

                return new MinJustOrganizationInfo
                {
                    Tin = subj?.tin ?? parsed.tin ?? tin,
                    FullName = name,
                    ShortName = subj?.shortname ?? parsed.shortname ?? name,
                    LegalForm = subj?.legalform ?? parsed.legalform ?? "",
                    RegistrationNumber = subj?.registrationnumber ?? parsed.registrationnumber ?? "",
                    RegistrationDate = subj?.registrationdate ?? parsed.registrationdate ?? "",
                    Address = subj?.address ?? parsed.address ?? "",
                    DirectorName = subj?.director ?? parsed.director ?? "",
                    DirectorPosition = subj?.directorposition ?? parsed.directorposition ?? "",
                    Status = subj?.status ?? "Активный",
                };
            }
            catch
            {
                return null;
            }
        }

        // === Эмулятор (детерминированный) ============================

        private static MinJustOrganizationInfo BuildEmulatedInfo(string tin)
        {
            // Для известного тестового ИНН возвращаем статический, всегда одинаковый
            // набор данных — чтобы тестовый вход не «менял» организацию между запусками.
            if (tin == "01001202210023")
            {
                return new MinJustOrganizationInfo
                {
                    Tin = tin,
                    FullName = "ОсОО «ТестТех Системс»",
                    ShortName = "ТестТех Системс",
                    LegalForm = "ОсОО",
                    RegistrationNumber = "123456",
                    RegistrationDate = "15.03.2015",
                    Address = "г. Бишкек, пр. Манаса, д. 42",
                    DirectorName = "Абдыкадыров Нурлан",
                    DirectorPosition = "Генеральный директор",
                    BusinessSector = "IT",
                    BusinessSectorName = "Информационные технологии",
                    Status = "Активный",
                };
            }

            // Для прочих ИНН — детерминированная генерация (seed по ИНН,
            // одинаковый ИНН даёт одинаковые данные).
            int seed = 0;
            foreach (var ch in tin) seed = unchecked(seed * 31 + ch);
            var rnd = new Random(seed);

            // Официальный справочник секторов (organization_buisnes_sectors)
            var sectors = new (string code, string name)[]
            {
                ("IT",    "Информационные технологии"),
                ("FIN",   "Финансы"),
                ("EDU",   "Образование"),
                ("HLTH",  "Здравоохранение"),
                ("CONS",  "Строительство"),
                ("TRD",   "Торговля"),
                ("LOG",   "Логистика"),
                ("AGRO",  "Сельское хозяйство"),
                ("MFG",   "Производство"),
                ("TEL",   "Телекоммуникации"),
                ("ENER",  "Энергетика"),
                ("TOUR",  "Туризм"),
                ("REAL",  "Недвижимость"),
                ("SERV",  "Услуги"),
                ("GOV",   "Государственный сектор"),
                ("MEDIA", "Медиа и развлечения"),
                ("INS",   "Страхование"),
                ("BANK",  "Банковская деятельность"),
                ("AUTO",  "Автомобильная отрасль"),
                ("FOOD",  "Пищевая промышленность"),
                ("OTHER", "Другое / Не указано"),
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
            var regDate = new DateTime(regYear, rnd.Next(1, 13), rnd.Next(1, 28));

            return new MinJustOrganizationInfo
            {
                Tin = tin,
                FullName = name,
                ShortName = name,
                LegalForm = form,
                RegistrationNumber = rnd.Next(100000, 999999).ToString(CultureInfo.InvariantCulture),
                RegistrationDate = regDate.ToString("dd.MM.yyyy"),
                Address = $"{cities[rnd.Next(cities.Length)]}, {streets[rnd.Next(streets.Length)]}, д. {rnd.Next(1, 250)}",
                DirectorName = $"{lastNames[rnd.Next(lastNames.Length)]} {firstNames[rnd.Next(firstNames.Length)]}",
                DirectorPosition = positions[rnd.Next(positions.Length)],
                BusinessSector = sector.code,
                BusinessSectorName = sector.name,
                Status = "Активный",
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
