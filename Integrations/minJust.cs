using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using data_registry_public.Models.local_models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace data_registry_public.Integrations
{
    /// <summary>
    /// Ответ сервиса Минюста getSubjectByQueryRequest.
    /// Запрос: POST { "query": "&lt;ИНН&gt;", "type": "TIN" }.
    /// Если субъект не найден, сервис отвечает HTTP 200 и subject с пустыми строками.
    /// </summary>
    public class MinJustResponce
    {
        public MinJustSubject? subject { get; set; }
    }

    public class MinJustSubject
    {
        public string? fullNameGl { get; set; }
        public string? shortNameGl { get; set; }
        public string? fullNameOl { get; set; }
        public string? shortNameOl { get; set; }
        public string? foreignPart { get; set; }
        public string? registrCode { get; set; }
        public string? statSubCode { get; set; }
        public string? tin { get; set; }
        public string? region { get; set; }
        public string? district { get; set; }
        public string? city { get; set; }
        public string? microdistrict { get; set; }
        public string? street { get; set; }
        public string? house { get; set; }
        public string? room { get; set; }
        public string? phones { get; set; }
        public string? email1 { get; set; }
        public string? orderDate { get; set; }
        public string? category { get; set; }
        /// <summary>Форма собственности: 1 — частная, 2 — государственная, 3 — муниципальная.</summary>
        public string? ownership { get; set; }
        public string? chief { get; set; }
        /// <summary>Основной вид деятельности.</summary>
        public string? baseBus { get; set; }
        public string? baseBusCode { get; set; }
        public string? description { get; set; }
        public List<MinJustFounder>? founders { get; set; }
    }

    public class MinJustFounder
    {
        public string? fullName { get; set; }
        public string? citizenship { get; set; }
        public string? tin { get; set; }
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
        /// <summary>Форма собственности (как в Минюсте): Частная / Государственная / Муниципальная.</summary>
        public string Ownership { get; set; } = string.Empty;
        /// <summary>Основной вид деятельности (baseBus).</summary>
        public string MainActivity { get; set; } = string.Empty;
        public string Phones { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = "Активный";
        public string Source { get; set; } = "MinJust";

        public const string SourceDb = "БД (последняя синхронизация с Минюстом)";

        /// <summary>Реквизиты, сохранённые при последней синхронизации, — запасной вариант, когда Минюст не ответил.</summary>
        public static MinJustOrganizationInfo FromOrganization(data_registry_public.Models.Organization org) => new()
        {
            Tin = org.Id,
            FullName = org.Fullnamegl ?? org.Id,
            ShortName = org.ShortName ?? org.Fullnamegl ?? "",
            LegalForm = org.LegalForm ?? "",
            RegistrationNumber = org.RegistrationNumber ?? "",
            RegistrationDate = org.RegistrationDate ?? "",
            Address = org.Address ?? "",
            DirectorName = org.DirectorName ?? "",
            DirectorPosition = org.DirectorPosition ?? "",
            BusinessSector = org.Businesssector ?? "",
            BusinessSectorName = org.BusinesssectorNavigation?.SectorRu ?? "",
            Status = org.Status ?? "Активный",
            Source = SourceDb
        };
    }

    /// <summary>
    /// Настройки интеграции (секция MinJust).
    /// Mode: Real — запросы в API Минюста (сервер: тест / прод);
    ///       Emulator — данные генерируются по ИНН (локальный запуск, appsettings.Development.json).
    /// </summary>
    public class MinJustOptions
    {
        public const string Real = "Real";
        public const string Emulator = "Emulator";

        public string Mode { get; set; } = Real;
        public int TimeoutSeconds { get; set; } = 10;
        public int CacheMinutes { get; set; } = 10;

        public bool UseEmulator => string.Equals(Mode, Emulator, StringComparison.OrdinalIgnoreCase);
    }

    public interface IMinJustService
    {
        /// <summary>Режим эмулятора (локальный запуск). В реальном режиме данные только из API.</summary>
        bool IsEmulator { get; }

        /// <summary>
        /// Получить данные организации по ИНН. null — не найдено или сервис недоступен (подробности в логе).
        /// </summary>
        /// <param name="tin">ИНН организации</param>
        /// <param name="forceEmulator">принудительно использовать эмулятор (тестовый вход)</param>
        Task<MinJustOrganizationInfo?> GetOrganizationByTinAsync(string tin, bool forceEmulator = false);
    }

    /// <summary>
    /// Клиент к API Министерства юстиции. В режиме Real НЕ подменяет данные эмулятором при ошибке —
    /// иначе в БД попали бы выдуманные реквизиты организаций. Успешные ответы кэшируются на CacheMinutes.
    /// </summary>
    public class MinJustService : IMinJustService
    {
        public const string HttpClientName = "minjust";

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly ILogger<MinJustService> _logger;
        private readonly IHttpClientFactory _httpFactory;
        private readonly AppSettings _appSettings;
        private readonly MinJustOptions _options;
        private readonly IMemoryCache _cache;

        public MinJustService(
            ILogger<MinJustService> logger,
            IHttpClientFactory httpFactory,
            IOptions<AppSettings> appSettings,
            IOptions<MinJustOptions> options,
            IMemoryCache cache)
        {
            _logger = logger;
            _httpFactory = httpFactory;
            _appSettings = appSettings.Value;
            _options = options.Value;
            _cache = cache;
        }

        public bool IsEmulator => _options.UseEmulator;

        public async Task<MinJustOrganizationInfo?> GetOrganizationByTinAsync(string tin, bool forceEmulator = false)
        {
            if (string.IsNullOrWhiteSpace(tin)) return null;
            tin = tin.Trim();

            if (forceEmulator || _options.UseEmulator)
            {
                return Emulate(tin);
            }

            var cacheKey = "minjust:" + tin;
            if (_cache.TryGetValue(cacheKey, out CachedLookup? cached) && cached is not null)
            {
                return cached.Info;
            }

            var (info, definitive) = await QueryApiAsync(tin);
            // Кэшируем найденное и «не найдено». Сбои связи не кэшируем — следующий запрос повторит попытку.
            if (definitive && _options.CacheMinutes > 0)
            {
                _cache.Set(cacheKey, new CachedLookup(info), TimeSpan.FromMinutes(_options.CacheMinutes));
            }
            return info;
        }

        private sealed record CachedLookup(MinJustOrganizationInfo? Info);

        /// <summary>definitive = true, если Минюст ответил (нашёл или не нашёл); false — сбой связи / ошибка.</summary>
        private async Task<(MinJustOrganizationInfo? Info, bool Definitive)> QueryApiAsync(string tin)
        {
            if (string.IsNullOrWhiteSpace(_appSettings.minJustApi))
            {
                _logger.LogError("Интеграция с Минюстом: не задан AppSettings:minJustApi");
                return (null, false);
            }

            try
            {
                var http = _httpFactory.CreateClient(HttpClientName);
                http.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds));

                var response = await http.PostAsJsonAsync(_appSettings.minJustApi, new { query = tin, type = "TIN" });
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Минюст вернул HTTP {Status} по ИНН {Tin}", (int)response.StatusCode, tin);
                    return (null, false);
                }

                var json = await response.Content.ReadAsStringAsync();
                var parsed = JsonSerializer.Deserialize<MinJustResponce>(json, JsonOptions);
                var info = Map(parsed?.subject, tin);
                if (info is null)
                {
                    _logger.LogInformation("Минюст: субъект с ИНН {Tin} не найден", tin);
                    return (null, true);
                }

                _logger.LogInformation("Минюст: получены данные по ИНН {Tin} ({Name})", tin, info.FullName);
                return (info, true);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Минюст не ответил за {Timeout} с по ИНН {Tin}", _options.TimeoutSeconds, tin);
                return (null, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка запроса в Минюст по ИНН {Tin}", tin);
                return (null, false);
            }
        }

        /// <summary>Ответ API → нормализованный DTO. Пустой subject (все поля "") — «не найдено».</summary>
        private static MinJustOrganizationInfo? Map(MinJustSubject? s, string requestedTin)
        {
            if (s is null) return null;

            var fullName = Clean(s.fullNameGl) ?? Clean(s.fullNameOl);
            var subjectTin = Clean(s.tin);
            if (fullName is null && subjectTin is null) return null;

            var ownership = Clean(s.ownership) switch
            {
                "1" => "Частная",
                "2" => "Государственная",
                "3" => "Муниципальная",
                var other => other ?? ""
            };

            return new MinJustOrganizationInfo
            {
                Tin = subjectTin ?? requestedTin,
                FullName = fullName ?? subjectTin ?? requestedTin,
                ShortName = Clean(s.shortNameGl) ?? Clean(s.shortNameOl) ?? fullName ?? "",
                LegalForm = Clean(s.category) ?? "",
                RegistrationNumber = Clean(s.registrCode) ?? Clean(s.statSubCode) ?? "",
                RegistrationDate = Clean(s.orderDate) ?? "",
                Address = BuildAddress(s),
                DirectorName = Clean(s.chief) ?? "",
                DirectorPosition = Clean(s.chief) is null ? "" : "Руководитель",
                // Код сектора из нашего справочника по данным Минюста определить нельзя, кроме гос. / муниципальных.
                BusinessSector = Clean(s.ownership) is "2" or "3" ? "GOV" : "",
                Ownership = ownership,
                MainActivity = Clean(s.baseBus) ?? "",
                Phones = Clean(s.phones) ?? "",
                Email = Clean(s.email1) ?? "",
                Status = "Активный",
                Source = "MinJust API"
            };
        }

        private static string BuildAddress(MinJustSubject s)
        {
            var parts = new List<string>();
            void Add(string? value, string? prefix = null)
            {
                var v = Clean(value);
                if (v is null) return;
                parts.Add(prefix is null ? v : $"{prefix} {v}");
            }

            Add(s.region);
            Add(s.district);
            Add(s.city);
            Add(s.microdistrict);
            Add(s.street);
            Add(s.house, "д.");
            Add(s.room, "кв./оф.");
            return string.Join(", ", parts);
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        // === Эмулятор (детерминированный, только для локального запуска / тестового входа) ===

        private static MinJustOrganizationInfo? Emulate(string tin)
        {
            if (tin.Length < 8 || !tin.All(char.IsDigit))
            {
                return null;
            }
            var emu = BuildEmulatedInfo(tin);
            emu.Source = "MinJust Emulator (dev)";
            return emu;
        }

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
                    Ownership = "Частная",
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
