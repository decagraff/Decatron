using Decatron.Core.Interfaces;
using Decatron.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace Decatron.Pages
{
    /// <summary>
    /// Settings Page Model - Página de configuración protegida
    /// Carga configuración real desde la base de datos
    /// </summary>
    [Authorize]
    public class SettingsModel : PageModel
    {
        private readonly ISettingsService _settingsService;
        private readonly IAuthService _authService;
        private readonly ILogger<SettingsModel> _logger;

        public SystemSettings Settings { get; set; }
        public User CurrentUser { get; set; }
        public List<UserAccess> UserAccesses { get; set; } = new();
        public string OverlayUrl { get; set; }

        public SettingsModel(
            ISettingsService settingsService,
            IAuthService authService,
            ILogger<SettingsModel> logger)
        {
            _settingsService = settingsService;
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// GET request handler - Carga la página de configuración
        /// </summary>
        public async Task OnGetAsync()
        {
            try
            {
                // Obtener ID del usuario del claim
                var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!long.TryParse(userIdClaim, out var userId))
                {
                    _logger.LogWarning("Invalid or missing user ID claim");
                    RedirectToPage("/login");
                    return;
                }

                // Obtener usuario actual
                CurrentUser = await _authService.GetUserByIdAsync(userId);
                if (CurrentUser == null)
                {
                    _logger.LogWarning($"User not found: {userId}");
                    RedirectToPage("/login");
                    return;
                }

                // Obtener configuración del usuario (o crear default si no existe)
                Settings = await _settingsService.GetSettingsByUserIdAsync(userId);

                // Obtener accesos autorizados
                var accesses = await _settingsService.GetUserAccessesAsync(userId);
                UserAccesses = accesses.ToList();

                OverlayUrl = $"{Request.Scheme}://{Request.Host}/settings";

                _logger.LogInformation($"Settings page loaded for user: {CurrentUser.Login}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings page");
                RedirectToPage("/login");
            }
        }
    }
}