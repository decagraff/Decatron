using Decatron.Core.Interfaces;
using Decatron.Core.Models;
using Decatron.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Decatron.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly TwitchBotService _botService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            TwitchBotService botService,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _botService = botService;
            _logger = logger;
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            try
            {
                var loginUrl = _authService.GetLoginUrl();
                _logger.LogInformation("Redirecting to Twitch login");
                return Redirect(loginUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return RedirectToPage("/login", new { error = "Error initiating login" });
            }
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback(
            [FromQuery] string code = null,
            [FromQuery] string error = null,
            [FromQuery] string state = null)
        {
            try
            {
                _logger.LogInformation($"Auth callback received - Error: {error}");

                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogWarning($"Authentication error: {error}");
                    return RedirectToPage("/login", new { error = $"Authentication error: {error}" });
                }

                if (string.IsNullOrEmpty(code))
                {
                    _logger.LogWarning("Authorization code not received");
                    return RedirectToPage("/login", new { error = "Authorization code not received" });
                }

                // Exchange code for token
                _logger.LogInformation("Exchanging code for token...");
                var tokenResponse = await _authService.ExchangeCodeForTokenAsync(code);

                // Get user info
                _logger.LogInformation("Fetching user information...");
                var twitchUser = await _authService.GetUserInfoAsync(tokenResponse.AccessToken);

                // Authenticate user (save to DB)
                _logger.LogInformation($"Authenticating user: {twitchUser.Login}");
                var user = await _authService.AuthenticateUserAsync(twitchUser, tokenResponse);

                // Sign in user
                await SignInUserAsync(user, tokenResponse);

                // Connect bot to channel - TwitchBotService maneja automáticamente la configuración
                _logger.LogInformation($"Connecting bot to channel: {user.Login}");
                if (_botService.IsConnected)
                {
                    _botService.JoinChannel(user.Login);
                }
                else
                {
                    _logger.LogWarning("Bot not connected, channel will be joined when bot starts");
                }

                _logger.LogInformation($"User {user.Login} authenticated successfully");
                return RedirectToPage("/dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication callback");
                return RedirectToPage("/login", new { error = $"Authentication error: {ex.Message}" });
            }
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                _logger.LogInformation("User logged out");
                return RedirectToPage("/");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return RedirectToPage("/");
            }
        }

        private async Task SignInUserAsync(User user, TokenResponse tokenResponse)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Login),
                new Claim(ClaimTypes.GivenName, user.DisplayName),
                new Claim("TwitchId", user.TwitchId),
                new Claim("ProfileImage", user.ProfileImageUrl ?? ""),
                new Claim("Email", user.Email ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                IsPersistent = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }
    }
}