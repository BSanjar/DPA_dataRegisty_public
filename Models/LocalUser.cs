using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

public partial class LocalUser
{
    public string Id { get; set; } = null!;

    /// <summary>
    /// if local_user
    /// </summary>
    public string? Login { get; set; }

    /// <summary>
    /// if local_user
    /// </summary>
    public string? Password { get; set; }

    public bool? Active { get; set; }

    /// <summary>
    /// local
    /// active_directory
    /// </summary>
    public string? UserType { get; set; }

    public string? Name { get; set; }

    public virtual ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();

    public virtual ICollection<Sanction> SanctionExecutedofficerNavigations { get; set; } = new List<Sanction>();

    public virtual ICollection<Sanction> SanctionProtocolofficerNavigations { get; set; } = new List<Sanction>();
}
