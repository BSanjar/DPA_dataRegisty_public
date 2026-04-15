using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

/// <summary>
/// скриншоты
/// журналы событий (логи)
/// отчеты
/// документы
/// иные материалы
/// </summary>
public partial class IncidentMaterial
{
    public string Id { get; set; } = null!;

    public string? Name { get; set; }

    public string? Type { get; set; }

    public DateTime? DateCreate { get; set; }

    public string? Incident { get; set; }

    public virtual Incident? IncidentNavigation { get; set; }
}
