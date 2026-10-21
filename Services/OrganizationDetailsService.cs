using System.Globalization;
using data_registry_public.Integrations;
using data_registry_public.Models;
using data_registry_public.Models.local_models;
using Microsoft.EntityFrameworkCore;

namespace data_registry_public.Services
{
    /// <summary>Откуда взяты реквизиты организации на экране.</summary>
    public enum OrganizationDetailsKind
    {
        /// <summary>Ответ Минюста (или эмулятора в Development) — только просмотр.</summary>
        MinJust,
        /// <summary>Минюст сейчас не ответил, показаны его данные из прошлой синхронизации — только просмотр.</summary>
        MinJustSaved,
        /// <summary>Минюст не знает ИНН, реквизиты указал представитель организации — можно изменить.</summary>
        Manual,
        /// <summary>Минюст не знает ИНН, реквизиты ещё не указаны — нужно заполнить.</summary>
        Missing
    }

    public sealed record OrganizationDetails(MinJustOrganizationInfo Info, OrganizationDetailsKind Kind)
    {
        /// <summary>Реквизиты указывает сам пользователь (Минюст их не дал).</summary>
        public bool IsEditable => Kind is OrganizationDetailsKind.Manual or OrganizationDetailsKind.Missing;
        public bool IsMissing => Kind == OrganizationDetailsKind.Missing;
    }

    public sealed record SectorOption(string Id, string Name);

    /// <summary>
    /// Реквизиты организации вошедшего пользователя: из Минюста, а если Минюст не знает ИНН (переданный ЕСИ) —
    /// указанные вручную. ИНН всегда из сессии, со страницы не принимается.
    /// </summary>
    public sealed class OrganizationDetailsService
    {
        private readonly AppDbContext _db;
        private readonly IMinJustService _minJust;
        private readonly ILogger<OrganizationDetailsService> _logger;

        public OrganizationDetailsService(AppDbContext db, IMinJustService minJust, ILogger<OrganizationDetailsService> logger)
        {
            _db = db;
            _minJust = minJust;
            _logger = logger;
        }

        public const string SourceManual = "Указано представителем организации";

        public async Task<OrganizationDetails> GetAsync(string tin)
        {
            var info = await _minJust.GetOrganizationByTinAsync(tin);
            if (info != null) return new OrganizationDetails(info, OrganizationDetailsKind.MinJust);

            var org = await _db.Organizations.AsNoTracking()
                .Include(o => o.BusinesssectorNavigation)
                .FirstOrDefaultAsync(o => o.Id == tin);
            if (org == null)
            {
                return new OrganizationDetails(new MinJustOrganizationInfo { Tin = tin, Status = "" }, OrganizationDetailsKind.Missing);
            }

            var saved = MinJustOrganizationInfo.FromOrganization(org);
            saved.BusinessSectorName = SectorName(org.BusinesssectorNavigation) ?? saved.BusinessSectorName;
            if (org.DetailsSource == OrganizationDetailsSources.MinJust)
            {
                _logger.LogWarning("Минюст не вернул данные по ИНН {Tin}; показаны реквизиты из БД (синхронизация {Synced})", tin, org.LastSyncedAt);
                return new OrganizationDetails(saved, OrganizationDetailsKind.MinJustSaved);
            }

            // Без названия (только ИНН из ЕСИ) FromOrganization подставляет ИНН — здесь это не нужно.
            saved.FullName = org.Fullnamegl ?? "";
            saved.ShortName = org.ShortName ?? "";
            saved.Status = "";
            if (org.DetailsSource == OrganizationDetailsSources.Manual)
            {
                saved.Source = SourceManual;
                return new OrganizationDetails(saved, OrganizationDetailsKind.Manual);
            }
            return new OrganizationDetails(saved, OrganizationDetailsKind.Missing);
        }

        /// <summary>Значения для формы ручного ввода (то, что уже сохранено; для новой — название из ЕСИ).</summary>
        public static OrganizationDetailsInput ToInput(MinJustOrganizationInfo info) => new()
        {
            FullName = NullIfEmpty(info.FullName),
            ShortName = NullIfEmpty(info.ShortName),
            LegalForm = NullIfEmpty(info.LegalForm),
            BusinessSector = NullIfEmpty(info.BusinessSector),
            RegistrationNumber = NullIfEmpty(info.RegistrationNumber),
            RegistrationDate = OrganizationDetailsInput.ParseDate(info.RegistrationDate),
            Address = NullIfEmpty(info.Address),
            DirectorName = NullIfEmpty(info.DirectorName),
            DirectorPosition = NullIfEmpty(info.DirectorPosition),
        };

        public async Task<IReadOnlyList<SectorOption>> GetSectorsAsync()
        {
            var sectors = await _db.OrganizationBuisnesSectors.AsNoTracking().ToListAsync();
            return sectors
                .Select(s => new SectorOption(s.Id, SectorName(s) ?? s.Id))
                .OrderBy(s => s.Id == "OTHER" ? 1 : 0).ThenBy(s => s.Name, StringComparer.CurrentCulture)
                .ToList();
        }

        public Task<bool> SectorExistsAsync(string? id) =>
            string.IsNullOrWhiteSpace(id)
                ? Task.FromResult(false)
                : _db.OrganizationBuisnesSectors.AsNoTracking().AnyAsync(s => s.Id == id);

        /// <summary>
        /// Сохраняет реквизиты, указанные вручную. Вызывать только для редактируемых (<see cref="OrganizationDetails.IsEditable"/>)
        /// и уже проверенных (ModelState) данных. Возвращает сохранённые реквизиты.
        /// </summary>
        public async Task<MinJustOrganizationInfo> SaveManualAsync(string tin, OrganizationDetailsInput input, string? userId)
        {
            input.Normalize();
            var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == tin);
            if (org == null)
            {
                org = new Organization { Id = tin, Risklevel = "Low" };
                _db.Organizations.Add(org);
            }
            else if (org.DetailsSource == OrganizationDetailsSources.MinJust)
            {
                throw new InvalidOperationException($"Реквизиты организации {tin} получены из Минюста и вручную не меняются");
            }

            org.Fullnamegl = input.FullName;
            org.ShortName = input.ShortName;
            org.LegalForm = input.LegalForm;
            org.Businesssector = input.BusinessSector;
            org.RegistrationNumber = input.RegistrationNumber;
            org.RegistrationDate = input.RegistrationDateText;
            org.Address = input.Address;
            org.DirectorName = input.DirectorName;
            org.DirectorPosition = input.DirectorPosition;
            org.Status = null;
            org.DetailsSource = OrganizationDetailsSources.Manual;
            org.DetailsUpdatedAt = DateTime.Now;
            org.DetailsUpdatedBy = userId;

            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _db.PublicUsers.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null)
                {
                    user.OrganizationName = input.FullName;
                    user.UpdatedAt = DateTime.Now;
                }
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Реквизиты организации {Tin} указаны вручную пользователем {UserId}", tin, userId ?? "-");

            var sector = await _db.OrganizationBuisnesSectors.AsNoTracking().FirstOrDefaultAsync(s => s.Id == org.Businesssector);
            var info = MinJustOrganizationInfo.FromOrganization(org);
            info.BusinessSectorName = SectorName(sector) ?? "";
            info.Status = "";
            info.Source = SourceManual;
            return info;
        }

        private static string? SectorName(OrganizationBuisnesSector? s)
        {
            if (s == null) return null;
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var name = lang switch
            {
                "ky" => s.SectorKg,
                "en" => s.SectorEn,
                _ => s.SectorRu
            };
            return string.IsNullOrWhiteSpace(name) ? s.SectorRu : name;
        }

        private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}
