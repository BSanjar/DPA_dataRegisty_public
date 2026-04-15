using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

public partial class Sanction
{
    public string Id { get; set; } = null!;

    /// <summary>
    /// примененные меры ответственности
    /// </summary>
    public string? Appliedsanctions { get; set; }

    /// <summary>
    /// размер наложенного штрафа
    /// </summary>
    public decimal? Fineamount { get; set; }

    /// <summary>
    /// по какому акту - протокол
    /// </summary>
    public string? InspectionAct { get; set; }

    /// <summary>
    /// по какому предписанию - протокол
    /// </summary>
    public string? InspectionActPrescription { get; set; }

    /// <summary>
    /// не исполненные предписания по нарушениям. 
    /// id из inspection_precription_violations через точку с запятой
    /// </summary>
    public string? Violations { get; set; }

    /// <summary>
    /// дата составления протокола
    /// </summary>
    public DateTime? DateReg { get; set; }

    /// <summary>
    /// основание привлечения
    /// </summary>
    public string? Reasonforliability { get; set; }

    /// <summary>
    /// Completed-исполнено
    /// NotCompleted-не исполнено
    /// 
    /// Appealed-обжалуется
    ///    UnderJudicialProceeding-находится в судебном производстве
    /// </summary>
    public string? Executionstatus { get; set; }

    /// <summary>
    /// оплаченная сумма
    /// </summary>
    public string? Paidamount { get; set; }

    /// <summary>
    /// дата исполения
    /// </summary>
    public DateTime? DateExecuted { get; set; }

    public string? Protocolofficer { get; set; }

    /// <summary>
    /// исполнивший сотрудник
    /// </summary>
    public string? Executedofficer { get; set; }

    /// <summary>
    /// основание на исполнение
    /// </summary>
    public string? Executionreason { get; set; }

    /// <summary>
    /// ссылка не предыдущий протокол если повтроно составляется протокол с новой суммой
    /// </summary>
    public string? PreSanction { get; set; }

    public virtual LocalUser? ExecutedofficerNavigation { get; set; }

    public virtual InspectionAct? InspectionActNavigation { get; set; }

    public virtual InspectionActPrescription? InspectionActPrescriptionNavigation { get; set; }

    public virtual ICollection<Sanction> InversePreSanctionNavigation { get; set; } = new List<Sanction>();

    public virtual Sanction? PreSanctionNavigation { get; set; }

    public virtual LocalUser? ProtocolofficerNavigation { get; set; }

    public virtual ICollection<SanctionMaterial> SanctionMaterials { get; set; } = new List<SanctionMaterial>();
}
