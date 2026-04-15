using System;
using System.Collections.Generic;

namespace data_registry_public.Models;

/// <summary>
/// пользователи через ЕСИ
/// </summary>
public partial class PublicUser
{
    public string Id { get; set; } = null!;

    public bool? Active { get; set; }

    public string? Organization { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }

    public DateTime? EmailVerifiedAt { get; set; }

    public string? Password { get; set; }

    public string? RegisterId { get; set; }

    public string? OrganizationTin { get; set; }

    public string? OrganizationName { get; set; }

    public string? PositionName { get; set; }

    public string? EsiSub { get; set; }

    public string? EsiPin { get; set; }

    public string? EsiCitizenship { get; set; }

    public string? EsiFamilyName { get; set; }

    public string? EsiGivenName { get; set; }

    public string? EsiName { get; set; }

    public string? EsiGender { get; set; }

    public string? EsiBirthdate { get; set; }

    public string? EsiEmail { get; set; }

    public string? EsiEmailVerified { get; set; }

    public string? EsiPhoneNumber { get; set; }

    public string? EsiPhoneNumberVerified { get; set; }

    public string? IdToken { get; set; }

    public string? AccessToken { get; set; }

    public string? ExpiresIn { get; set; }

    public string? TokenType { get; set; }

    public string? Scope { get; set; }

    public string? RememberToken { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CommentArchive { get; set; }

    public string? CommentDeArchive { get; set; }

    public DateTime? DeletedAt { get; set; }

    public decimal Archived { get; set; }

    public string? Isdeleted { get; set; }

    public string? TelegramId { get; set; }

    public virtual Organization? OrganizationNavigation { get; set; }
}
