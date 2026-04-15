using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

/// <summary>
/// Документы:
/// протоколы;
/// постановления;
/// судебные акты;
/// иные подтверждающие материалы.
/// </summary>
public partial class SanctionMaterial
{
    public string Id { get; set; } = null!;

    public string? Name { get; set; }

    /// <summary>
    /// Протокол — Protocol
    /// 
    /// 
    /// Постановления — Resolutions
    /// 
    /// 
    /// Судебные акты — JudicialActs
    /// 
    /// 
    /// Иные материалы — OtherMaterials
    /// </summary>
    public string? TypeFile { get; set; }

    public string? DateCreate { get; set; }

    /// <summary>
    /// формат файла - docx, xlsx итд
    /// </summary>
    public string? FormatFile { get; set; }

    public string? Sanction { get; set; }

    public virtual Sanction? SanctionNavigation { get; set; }
}
