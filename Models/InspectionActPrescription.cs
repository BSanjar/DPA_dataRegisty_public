using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

/// <summary>
/// Предписание по акту
/// </summary>
public partial class InspectionActPrescription
{
    public string Id { get; set; } = null!;

    /// <summary>
    /// по каким нарушениям предписание. 
    /// перечисляется id нарушений через точку с запятой
    /// </summary>
    public string? Violations { get; set; }

    /// <summary>
    /// номер предписания
    /// </summary>
    public string? PrescriptionNumber { get; set; }

    /// <summary>
    /// срок исполнения
    /// </summary>
    public DateTime? Duedate { get; set; }

    /// <summary>
    /// статус исполнения:
    /// Completed-исполнено
    /// 
    /// PartiallyCompleted-частично исполнено
    ///     NotCompleted-не исполнено.
    /// 
    /// т.е если в inspection_precription_violations - все записи по данному предписанию - исполнены, то статус - Completed.
    /// если есть исполненные и не исполненные то - PartiallyCompleted,
    /// если все не исполненные то NotCompleted.
    /// </summary>
    public string? Executionstatus { get; set; }

    public virtual ICollection<InspectionPrecriptionViolation> InspectionPrecriptionViolations { get; set; } = new List<InspectionPrecriptionViolation>();

    public virtual ICollection<Sanction> Sanctions { get; set; } = new List<Sanction>();
}
