using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

public partial class IncidentType
{
    public string Id { get; set; } = null!;

    public string? Name { get; set; }

    public virtual ICollection<Incident> Incidents { get; set; } = new List<Incident>();
}
