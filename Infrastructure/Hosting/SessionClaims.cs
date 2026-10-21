using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace data_registry_public.Infrastructure.Hosting
{
    /// <summary>Обновление claims текущей cookie-сессии без повторного входа через ЕСИ.</summary>
    public static class SessionClaims
    {
        /// <summary>Название организации в шапке (claim orgName) — после ручного ввода реквизитов.</summary>
        public static async Task UpdateOrgNameAsync(HttpContext context, string? orgName)
        {
            var auth = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!auth.Succeeded || auth.Principal?.Identity is not ClaimsIdentity identity) return;

            var updated = new ClaimsIdentity(identity.Claims.Where(c => c.Type != "orgName"),
                identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType);
            updated.AddClaim(new Claim("orgName", orgName ?? ""));
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(updated), auth.Properties);
        }
    }
}
