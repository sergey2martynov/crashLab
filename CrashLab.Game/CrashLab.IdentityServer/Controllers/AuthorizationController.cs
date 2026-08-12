using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace CrashLab.IdentityServer.Controllers;

public class AuthorizationController : ControllerBase
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;

        // юзер не залогинен (нет cookie Identity.Application) -> редирект на /account/login,
        // сохраняя исходный authorize-запрос в ReturnUrl, чтобы вернуться сюда после логина
        if (!User.Identity!.IsAuthenticated)
        {
            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + Request.QueryString
                });
        }

        // юзер залогинен -> собираем claims-принципал для OpenIddict-схемы
        var identity = new ClaimsIdentity(
            authenticationType: "Bearer",
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        identity.SetClaim(OpenIddictConstants.Claims.Subject, User.FindFirstValue(ClaimTypes.NameIdentifier));
        identity.SetClaim(OpenIddictConstants.Claims.Name, User.Identity.Name);
        identity.SetScopes(request.GetScopes());
        identity.SetDestinations(GetDestinations);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
    
    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        // sub и name кладём и в access_token, и в id_token (если запрошен scope profile)
        if (claim.Type is OpenIddictConstants.Claims.Subject or OpenIddictConstants.Claims.Name)
        {
            yield return OpenIddictConstants.Destinations.AccessToken;
            if (claim.Subject!.HasScope(OpenIddictConstants.Scopes.Profile))
                yield return OpenIddictConstants.Destinations.IdentityToken;
        }
        else
        {
            yield return OpenIddictConstants.Destinations.AccessToken;
        }
    }

    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;

        if (request.IsAuthorizationCodeGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            return SignIn(result.Principal!, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new NotImplementedException("Unsupported grant type");
    }
}