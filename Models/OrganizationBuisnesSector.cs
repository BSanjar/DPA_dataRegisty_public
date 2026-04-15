using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

public partial class OrganizationBuisnesSector
{
    public string Id { get; set; } = null!;

    public string? SectorEn { get; set; }

    public string? SectorRu { get; set; }

    public string? SectorKg { get; set; }

    public virtual ICollection<Organization> Organizations { get; set; } = new List<Organization>();
}
