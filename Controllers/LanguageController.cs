using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace data_registry_public.Controllers
{
    /// <summary>
    /// Смена языка интерфейса (записывает cookie культуры).
    /// </summary>
    public class LanguageController : Controller
    {
        /// <summary>
        /// POST /Language/Set?culture=en&amp;returnUrl=/
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Set(string culture, string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(culture))
            {
                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                    new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddYears(1),
                        IsEssential = true,
                        SameSite = SameSiteMode.Lax,
                        HttpOnly = false
                    });
            }
            return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "~/" : returnUrl);
        }
    }
}
