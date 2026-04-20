using System.ComponentModel.DataAnnotations;

namespace data_registry_public.Models.local_models
{
    /// <summary>
    /// Модель данных для многошагового мастера регистрации инцидента.
    /// Шаг 1 — организация (подтягивается из Минюста).
    /// Шаг 2 — общие сведения об инциденте.
    /// Шаг 3 — затронутые данные и принятые меры.
    /// </summary>
    public class IncidentCreateViewModel
    {
        // === Шаг 1. Организация ===
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

        [Display(Name = "Дата обнаружения")]
        public DateTime? DateDetection { get; set; }

        [Display(Name = "Дата возникновения")]
        public DateTime? DateOccurrence { get; set; }

        [Display(Name = "Место возникновения")]
        public string? IncidentLocation { get; set; }

        [Display(Name = "Краткое описание")]
        public string? IncidentDescription { get; set; }

        [Display(Name = "Тип инцидента")]
        public string? IncidentType { get; set; }

        [Display(Name = "Критичность")]
        public string? IncidentSeverity { get; set; }

        [Display(Name = "Затронутая информационная система")]
        public string? AffectedInformationSystem { get; set; }

        [Display(Name = "Тип системы")]
        public string? SystemType { get; set; }

        [Display(Name = "Предполагаемая причина")]
        public string? SuspectedCause { get; set; }

        // === Шаг 3. Затронутые данные и меры ===

        [Display(Name = "Количество записей")]
        public decimal? AffectedRecordsCount { get; set; }

        [Display(Name = "Данные были скопированы/похищены")]
        public bool IsDataCopiedOrStolen { get; set; }

        [Display(Name = "Данные были опубликованы")]
        public bool IsDataPublished { get; set; }

        [Display(Name = "Есть риск вреда субъектам")]
        public bool HasRiskOfHarmToSubjects { get; set; }

        [Display(Name = "Субъекты уведомлены")]
        public bool AreSubjectsNotified { get; set; }

        [Display(Name = "Категории персональных данных")]
        public string? PersonalDataCategories { get; set; }

        [Display(Name = "Тип персональных данных")]
        public string? PersonalDataType { get; set; }

        [Display(Name = "Количество субъектов")]
        public string? EstimatedDataSubjectsCount { get; set; }

        [Display(Name = "Категория субъектов")]
        public string? DataSubjectCategory { get; set; }

        [Display(Name = "Принятые меры по устранению")]
        public string? ActionsTaken { get; set; }

        [Display(Name = "Меры по предотвращению")]
        public string? PreventionMeasures { get; set; }

        [Display(Name = "Информация о расследовании")]
        public string? InvestigationInfo { get; set; }

        [Display(Name = "Дата отправки уведомления")]
        public DateTime? NotificationSentAt { get; set; }
    }
}
