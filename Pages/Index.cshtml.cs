using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Decatron.Pages
{
    /// <summary>
    /// Index Page Model - Landing page pública
    /// Página de inicio sin autenticación requerida
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// GET request handler - Carga la página de inicio
        /// </summary>
        public void OnGet()
        {
            _logger.LogInformation("Index page accessed");
        }
    }
}
