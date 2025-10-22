using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Decatron.Pages
{
    /// <summary>
    /// Dashboard Page Model - Página principal protegida
    /// Requiere autenticación
    /// </summary>
    [Authorize]
    public class DashboardModel : PageModel
    {
        private readonly ILogger<DashboardModel> _logger;

        public DashboardModel(ILogger<DashboardModel> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// GET request handler - Carga el dashboard
        /// </summary>
        public void OnGet()
        {
            var userName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            _logger.LogInformation($"Dashboard accessed by user: {userName}");
        }
    }
}
