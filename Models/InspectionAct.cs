using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

public partial class InspectionAct
{
    public string Id { get; set; } = null!;

    public string? Inspection { get; set; }

    /// <summary>
    /// дата создание акта
    /// </summary>
    public DateTime? Actdate { get; set; }

    /// <summary>
    /// выводы по результатам проверки
    /// </summary>
    public string? Findings { get; set; }

    public virtual ICollection<InspectionActViolation> InspectionActViolations { get; set; } = new List<InspectionActViolation>();

    public virtual Inspection? InspectionNavigation { get; set; }

    public virtual ICollection<Sanction> Sanctions { get; set; } = new List<Sanction>();
}
