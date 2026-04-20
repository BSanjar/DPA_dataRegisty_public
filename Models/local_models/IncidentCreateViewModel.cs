using System.ComponentModel.DataAnnotations;

namespace data_registry_public.Models.local_models
{
    /// <summary>
    /// Модель данных для многошагового мастера регистрации инцидента.
    /// Шаг 1 — организация (подтягивается из Минюста, read-only).
    /// Шаг 2 — общие сведения об инциденте.
    /// Шаг 3 — затронутые данные и принятые меры.
    /// </summary>
    public class IncidentCreateViewModel
    {
        // === Шаг 1. Организация (только чтение) ===
        [Display(Name = "ИНН организации")]
        public string? OrganizationTin { get; set; }

        [Display(Name = "Наименование")]
        public string? OrganizationName { get; set; }

        [Display(Name = "Краткое наименование")]
        public string? OrganizationShortName { get; set; }

        [Display(Name = "Организационно-правовая форма")]
        public string? OrganizationLegalForm { get; set; }

        [Display(Name = "Регистрационный номер")]
        public string? OrganizationRegNumber { get; set; }

        [Display(Name = "Дата регистрации")]
        public string? OrganizationRegDate { get; set; }

        [Display(Name = "Адрес")]
        public string? OrganizationAddress { get; set; }

        [Display(Name = "Руководитель")]
        public string? OrganizationDirector { get; set; }

        [Display(Name = "Должность руководителя")]
        public string? OrganizationDirectorPosition { get; set; }

        [Display(Name = "Сфера деятельности")]
        public string? OrganizationSector { get; set; }

        [Display(Name = "Сфера деятельности (название)")]
        public string? OrganizationSectorName { get; set; }

        // === Шаг 2. Общие сведения об инциденте ===

        [Required(ErrorMessage = "Укажите дату возникновения")]
        [Display(Name = "Дата возникновения")]
        public DateTime? DateOccurrence { get; set; }

        [Required(ErrorMessage = "Укажите дату обнаружения")]
        [Display(Name = "Дата обнаружения")]
        public DateTime? DateDetection { get; set; }

        [Required(ErrorMessage = "Выберите тип инцидента")]
        [Display(Name = "Тип инцидента")]
        public string? IncidentType { get; set; }

        [Required(ErrorMessage = "Выберите уровень критичности")]
        [Display(Name = "Критичность")]
        public string? IncidentSeverity { get; set; }

        [Required(ErrorMessage = "Укажите место возникновения")]
        [StringLength(255, MinimumLength = 2, ErrorMessage = "От 2 до 255 символов")]
        [Display(Name = "Место возникновения")]
        public string? IncidentLocation { get; set; }

        [Required(ErrorMessage = "Укажите затронутую систему")]
        [StringLength(255, MinimumLength = 2, ErrorMessage = "От 2 до 255 символов")]
        [Display(Name = "Затронутая информационная система")]
        public string? AffectedInformationSystem { get; set; }

        [Required(ErrorMessage = "Выберите тип системы")]
        [Display(Name = "Тип системы")]
        public string? SystemType { get; set; }

        [Required(ErrorMessage = "Опишите инцидент")]
        [StringLength(4000, MinimumLength = 10, ErrorMessage = "Описание должно содержать от 10 до 4000 символов")]
        [Display(Name = "Краткое описание")]
        public string? IncidentDescription { get; set; }

        [Display(Name = "Предполагаемая причина")]
        [StringLength(2000, ErrorMessage = "Не более 2000 символов")]
        public string? SuspectedCause { get; set; }

        // === Шаг 3. Затронутые данные и меры ===

        [Required(ErrorMessage = "Укажите количество записей (можно 0)")]
        [Range(0, 2_000_000_000, ErrorMessage = "Введите число от 0 до 2 000 000 000")]
        [Display(Name = "Количество записей")]
        public decimal? AffectedRecordsCount { get; set; }

        [Required(ErrorMessage = "Укажите число субъектов (можно 0)")]
        [RegularExpression(@"^\d+$", ErrorMessage = "Только цифры")]
        [Display(Name = "Количество субъектов")]
        public string? EstimatedDataSubjectsCount { get; set; }

        [Required(ErrorMessage = "Выберите тип персональных данных")]
        [Display(Name = "Тип персональных данных")]
        public string? PersonalDataType { get; set; }

        [Required(ErrorMessage = "Выберите категорию субъектов")]
        [Display(Name = "Категория субъектов")]
        public string? DataSubjectCategory { get; set; }

        [Required(ErrorMessage = "Укажите категории персональных данных")]
        [StringLength(500, MinimumLength = 2, ErrorMessage = "От 2 до 500 символов")]
        [Display(Name = "Категории персональных данных")]
        public string? PersonalDataCategories { get; set; }

        [Display(Name = "Данные были скопированы/похищены")]
        public bool IsDataCopiedOrStolen { get; set; }

        [Display(Name = "Данные были опубликованы")]
        public bool IsDataPublished { get; set; }

        [Display(Name = "Есть риск вреда субъектам")]
        public bool HasRiskOfHarmToSubjects { get; set; }

        [Display(Name = "Субъекты уведомлены")]
        public bool AreSubjectsNotified { get; set; }

        [Required(ErrorMessage = "Опишите принятые меры")]
        [StringLength(2000, MinimumLength = 5, ErrorMessage = "От 5 до 2000 символов")]
        [Display(Name = "Принятые меры по устранению")]
        public string? ActionsTaken { get; set; }

        [Display(Name = "Меры по предотвращению")]
        [StringLength(2000, ErrorMessage = "Не более 2000 символов")]
        public string? PreventionMeasures { get; set; }

        [Display(Name = "Информация о расследовании")]
        [StringLength(2000, ErrorMessage = "Не более 2000 символов")]
        public string? InvestigationInfo { get; set; }

        [Display(Name = "Дата отправки уведомления")]
        public DateTime? NotificationSentAt { get; set; }
    }
}
