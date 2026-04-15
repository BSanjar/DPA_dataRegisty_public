using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

public partial class Incident
{
    public string Id { get; set; } = null!;

    public DateTime? DateCreate { get; set; }

    public string? IncidentRegnumber { get; set; }

    /// <summary>
    /// дата обнаружения инцедента
    /// </summary>
    public DateTime? DateDetection { get; set; }

    /// <summary>
    /// дата возникновения инцедента
    /// </summary>
    public DateTime? DateOccurrence { get; set; }

    /// <summary>
    /// место возникновения инцидента (информационная система, подразделение и др.)
    /// </summary>
    public string? IncidetLocation { get; set; }

    /// <summary>
    /// краткое описание инцидента
    /// </summary>
    public string? IncidentDescription { get; set; }

    /// <summary>
    /// Тип инцидента
    /// </summary>
    public string? IncidentType { get; set; }

    /// <summary>
    /// количество затронутых записей
    /// </summary>
    public decimal? Affectedrecordscount { get; set; }

    /// <summary>
    /// факт копирования или похищения данных
    /// </summary>
    public bool? Isdatacopiedorstolen { get; set; }

    /// <summary>
    /// факт публикации данных
    /// </summary>
    public bool? Isdatapublished { get; set; }

    /// <summary>
    /// наличие риска причинения вреда субъектам персональных данных
    /// </summary>
    public bool? Hasriskofharmtosubjects { get; set; }

    /// <summary>
    /// факт уведомления субъектов персональных данных
    /// </summary>
    public bool? Aresubjectsnotified { get; set; }

    /// <summary>
    /// затронутая информационная система
    /// </summary>
    public string? Affectedinformationsystem { get; set; }

    /// <summary>
    /// тип системы:
    /// db - база данных
    /// website - веб-сайт
    /// infosystem - информационная система
    /// cloude_service - облачный сервис
    /// local_network - локальная сеть
    /// </summary>
    public string? Systemtype { get; set; }

    /// <summary>
    /// предполагаемая причина инцидента
    /// </summary>
    public string? Suspectedincidentcause { get; set; }

    /// <summary>
    /// меры, принятые для устранения инцидента
    /// </summary>
    public string? Actionstaken { get; set; }

    /// <summary>
    /// меры по предотвращению повторения инцидента
    /// </summary>
    public string? Preventionmeasures { get; set; }

    /// <summary>
    /// информация о проведении внутреннего расследования
    /// </summary>
    public string? Investigationinfo { get; set; }

    /// <summary>
    /// уровень критичности инцидента:
    /// Low-низкий
    /// Medium-средний
    /// High-высокий
    /// Critical-критический
    /// </summary>
    public string? Incidentseverity { get; set; }

    /// <summary>
    /// дата отправки уведомления
    /// </summary>
    public DateTime? Notificationsentat { get; set; }

    /// <summary>
    /// категории персональных данных которые были затронуты
    /// </summary>
    public string? Personaldatacategories { get; set; }

    /// <summary>
    /// Regular - обычные
    /// Special - специальные
    /// Biometric - биометрические
    /// 
    /// </summary>
    public string? Personaldatatype { get; set; }

    /// <summary>
    /// примерное количество субъектов персональных данных
    /// </summary>
    public string? Estimateddatasubjectscount { get; set; }

    /// <summary>
    /// Категории субъектов данных:
    /// сотрудники
    /// клиенты
    /// граждане
    /// пользователи сайта
    /// иные категории
    /// 
    /// </summary>
    public string? Datasubjectcategory { get; set; }

    public virtual ICollection<IncidentMaterial> IncidentMaterials { get; set; } = new List<IncidentMaterial>();

    public virtual IncidentType? IncidentTypeNavigation { get; set; }
}
