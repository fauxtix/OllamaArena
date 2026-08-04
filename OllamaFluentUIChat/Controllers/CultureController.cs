using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace OllamaFluentUIChat.Controllers;

[Route("[controller]/[action]")]
public class CultureController : Controller
{
    [HttpGet]
    public IActionResult Set(string culture, string redirectUri)
    {
        if (!string.IsNullOrWhiteSpace(culture))
        {
            var cultureInfo = culture.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
                ? "pt"
                : "en";

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(cultureInfo)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    Path = "/",
                    SameSite = SameSiteMode.Lax
                });
        }

        return LocalRedirect(string.IsNullOrWhiteSpace(redirectUri) ? "/" : redirectUri);
    }
}
