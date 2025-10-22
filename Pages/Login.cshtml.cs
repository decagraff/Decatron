using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Decatron.Pages
{
    /// <summary>
    /// Login Page Model - Página de autenticación
    /// Obtiene el mensaje de error de los parámetros de query
    /// </summary>
    public class LoginModel : PageModel
    {
        private readonly ILogger<LoginModel> _logger;

        /// <summary>
        /// Mensaje de error (si existe)
        /// Se obtiene de la query string: ?error=mensaje
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public string Error { get; set; }

        public LoginModel(ILogger<LoginModel> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// GET request handler - Carga la página de login
        /// </summary>
        public void OnGet()
        {
            if (!string.IsNullOrEmpty(Error))
            {
                _logger.LogWarning($"Login page accessed with error: {Error}");
            }
            else
            {
                _logger.LogInformation("Login page accessed");
            }
        }
    }
}
