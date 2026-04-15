using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

public partial class Inspection
{
    public string Id { get; set; } = null!;

    public DateTime? DateCreate { get; set; }

    /// <summary>
    /// идентификатор проверки
    /// </summary>
    public string? CheckNumber { get; set; }

    /// <summary>
    /// проверяемый субъект
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// основание проведения проверки
    /// </summary>
    public string? Reasonforcheck { get; set; }

    /// <summary>
    /// вид проверки
    /// </summary>
    public string? Checktype { get; set; }

    /// <summary>
    /// сроки проверки, начало проверки
    /// </summary>
    public DateTime? DateCheckStart { get; set; }

    /// <summary>
    /// сроки проверки, конец проверки
    /// </summary>
    public DateTime? DateCheckEnd { get; set; }

    public string? Inspector { get; set; }

    /// <summary>
    /// plan-check - плановая проверка
    /// control-check - контрольная проверка
    /// 
    /// </summary>
    public string? InspectionType { get; set; }

    /// <summary>
    /// ссылка на плановую проверку если данная проверка контрольная проверка
    /// </summary>
    public string? PlanCheckInspection { get; set; }

    /// <summary>
    /// цикл проверки, 1й - цикл это первая проверка и контрольная проверка.
    /// 2й цикл это проверка и контрольная проверка в след году к примеру.
    /// </summary>
    public decimal? InspectionCycle { get; set; }

    /// <summary>
    /// ссылка на контрольную проверку в предыдущем цикле
    /// </summary>
    public string? PreCycleControlInspection { get; set; }

    public virtual ICollection<InspectionAct> InspectionActs { get; set; } = new List<InspectionAct>();

    public virtual LocalUser? InspectorNavigation { get; set; }

    public virtual ICollection<Inspection> InversePlanCheckInspectionNavigation { get; set; } = new List<Inspection>();

    public virtual ICollection<Inspection> InversePreCycleControlInspectionNavigation { get; set; } = new List<Inspection>();

    public virtual Organization? OrganizationNavigation { get; set; }

    public virtual Inspection? PlanCheckInspectionNavigation { get; set; }

    public virtual Inspection? PreCycleControlInspectionNavigation { get; set; }
}
