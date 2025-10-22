using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Decatron.Default.Pages
{
    [Authorize]
    public class MicroCommandsModel : PageModel
    {
        private readonly ILogger<MicroCommandsModel> _logger;

        public MicroCommandsModel(ILogger<MicroCommandsModel> logger)
        {
            _logger = logger;
        }

        public string UserLogin { get; private set; } = "";
        public string UserDisplayName { get; private set; } = "";
        public bool IsAuthenticated { get; private set; }

        public void OnGet()
        {
            IsAuthenticated = User.Identity?.IsAuthenticated ?? false;
            
            if (IsAuthenticated)
            {
                UserLogin = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
                UserDisplayName = User.FindFirst(ClaimTypes.GivenName)?.Value ?? UserLogin;
                
                _logger.LogInformation($"Usuario {UserLogin} accedió a la gestión de micro comandos");
            }
        }
    }
}