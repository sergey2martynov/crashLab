using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CrashLab.IdentityServer.Controllers;

public class AccountController(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager)
    : ControllerBase
{
    [HttpGet("~/account/login")]
    public IActionResult Login(string returnUrl)
    {
        var encodedReturnUrl = System.Net.WebUtility.HtmlEncode(returnUrl);
        var html = $"""
                    <html><body>
                    <form method="post" action="/account/login">
                        <input type="hidden" name="returnUrl" value="{encodedReturnUrl}" />
                        <input type="email" name="email" placeholder="email" required />
                        <input type="password" name="password" placeholder="password" required />
                        <button type="submit">Login</button>
                    </form>
                    </body></html>
                    """;
        return Content(html, "text/html");
    }

    [HttpPost("~/account/login")]
    public async Task<IActionResult> Login(string email, string password, string returnUrl)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return Login(returnUrl);

        if (!Url.IsLocalUrl(returnUrl))
            return BadRequest();
        
        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: false);
        if (!result.Succeeded)
            return Login(returnUrl);

        return Redirect(returnUrl);
    }
}