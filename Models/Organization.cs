using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

/// <summary>
/// субьект, организация
/// </summary>
public partial class Organization
{
    public string Id { get; set; } = null!;

    public string? Fullnamegl { get; set; }

    /// <summary>
    /// сфера деятельности субъекта:
    /// Government — государственные органы
    /// Telecommunications — телеком
    /// Healthcare — медицина
    /// Banking — банки / финансы
    /// Education — образование
    /// Insurance — страхование
    /// Retail — торговля
    /// IT — IT / технологии
    /// Manufacturing — производство
    /// Energy — энергетика
    /// Transportation — транспорт / логистика
    /// Agriculture — сельское хозяйство
    /// Media — СМИ
    /// Hospitality — туризм / гостиницы
    /// Other — другое
    /// </summary>
    public string? Businesssector { get; set; }

    /// <summary>
    /// уровень риска субъекта:
    /// Low-низкий
    /// 
    /// Medium-средний
    /// 
    /// High-высокий
    /// 
    /// Critical-критический
    /// </summary>
    public string? Risklevel { get; set; }

    public virtual OrganizationBuisnesSector? BusinesssectorNavigation { get; set; }

    public virtual ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();

    public virtual ICollection<PublicUser> PublicUsers { get; set; } = new List<PublicUser>();
}
