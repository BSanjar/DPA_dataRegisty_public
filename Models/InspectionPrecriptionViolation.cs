using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

/// <summary>
/// по каким нарушениям акта предписания. т.е для связи многие ко многим
/// </summary>
public partial class InspectionPrecriptionViolation
{
    public string Id { get; set; } = null!;

    /// <summary>
    /// предписание
    /// </summary>
    public string? Precription { get; set; }

    /// <summary>
    /// нарушение
    /// </summary>
    public string? Violation { get; set; }

    /// <summary>
    /// статус исполнения:
    /// Completed-исполнено
    /// 
    /// NotCompleted-не исполнено
    /// </summary>
    public string? Executionstatus { get; set; }

    public virtual InspectionActPrescription? PrecriptionNavigation { get; set; }

    public virtual InspectionActViolation? ViolationNavigation { get; set; }
}
