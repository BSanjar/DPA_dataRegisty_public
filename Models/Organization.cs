using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

/// <summary>
/// субьект, организация
/// </summary>
public partial class Organization
{
    /// <summary>
    /// Идентификатор (ИНН организации).
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>Полное наименование (как в Минюсте).</summary>
    public string? Fullnamegl { get; set; }

    /// <summary>Краткое наименование.</summary>
    public string? ShortName { get; set; }

    /// <summary>Организационно-правовая форма (ОсОО, ОАО и т.п.).</summary>
    public string? LegalForm { get; set; }

    /// <summary>Регистрационный номер из Минюста.</summary>
    public string? RegistrationNumber { get; set; }

    /// <summary>Дата регистрации (строкой, как отдаёт Минюст).</summary>
    public string? RegistrationDate { get; set; }

    /// <summary>Юридический адрес.</summary>
    public string? Address { get; set; }

    /// <summary>ФИО руководителя.</summary>
    public string? DirectorName { get; set; }

    /// <summary>Должность руководителя.</summary>
    public string? DirectorPosition { get; set; }

    /// <summary>Текущий статус (Активный, Ликвидирована и т.п.).</summary>
    public string? Status { get; set; }

    /// <summary>Время последнего обновления данных из Минюста.</summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// Откуда реквизиты: minjust — из Минюста; manual — указаны представителем организации вручную
    /// (Минюст не нашёл ИНН, переданный ЕСИ). См. <see cref="OrganizationDetailsSources"/>.
    /// </summary>
    public string? DetailsSource { get; set; }

    /// <summary>Когда реквизиты последний раз указаны вручную.</summary>
    public DateTime? DetailsUpdatedAt { get; set; }

    /// <summary>Кто указал реквизиты вручную (public_users.id).</summary>
    public string? DetailsUpdatedBy { get; set; }

    /// <summary>
    /// сфера деятельности субъекта:
    /// Government — государственные органы
    /// Telecommunications — телеком
    /// Healthcare — медицина
    /// Banking — банки / финансы
    /// Education — образование
    /// Insurance — страхование
    /// Retail — торговля
    /// IT — IT / технологии
    /// Manufacturing — производство
    /// Energy — энергетика
    /// Transportation — транспорт / логистика
    /// Agriculture — сельское хозяйство
    /// Media — СМИ
    /// Hospitality — туризм / гостиницы
    /// Other — другое
    /// </summary>
    public string? Businesssector { get; set; }

    /// <summary>
    /// уровень риска субъекта:
    /// Low-низкий
    /// 
    /// Medium-средний
    /// 
    /// High-высокий
    /// 
    /// Critical-критический
    /// </summary>
    public string? Risklevel { get; set; }

    /// <summary>Контакт организации по инцидентам — последний указанный ответственный (видит админка).</summary>
    public string? IncidentContactPerson { get; set; }

    public string? IncidentContactPosition { get; set; }

    public string? IncidentContactPhone { get; set; }

    public string? IncidentContactEmail { get; set; }

    public virtual OrganizationBuisnesSector? BusinesssectorNavigation { get; set; }

    public virtual ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();

    public virtual ICollection<PublicUser> PublicUsers { get; set; } = new List<PublicUser>();

    public virtual ICollection<Incident> Incidents { get; set; } = new List<Incident>();
}

/// <summary>Значения organizations.details_source.</summary>
public static class OrganizationDetailsSources
{
    public const string MinJust = "minjust";
    public const string Manual = "manual";
}
