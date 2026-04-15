using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

/// <summary>
/// нарушения
/// </summary>
public partial class InspectionActViolation
{
    public string Id { get; set; } = null!;

    /// <summary>
    /// нарушения по акту
    /// </summary>
    public string? Act { get; set; }

    /// <summary>
    /// нарушение
    /// </summary>
    public string? ViolationText { get; set; }

    /// <summary>
    /// причины нарушений(через точку с запятой):
    /// </summary>
    public string? Violationcause { get; set; }

    /// <summary>
    /// нарушенные нормы законодательства
    /// </summary>
    public string? Violatedlegalnorms { get; set; }

    public virtual InspectionAct? ActNavigation { get; set; }

    public virtual ICollection<InspectionPrecriptionViolation> InspectionPrecriptionViolations { get; set; } = new List<InspectionPrecriptionViolation>();
}
