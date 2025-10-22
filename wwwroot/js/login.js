/**
 * Login Page JavaScript
 * OAuth Twitch solamente
 */

class LoginPage {
    constructor() {
        this.twitchButton = document.querySelector('.oauth-twitch');
        this.init();
    }

    init() {
        this.setupEventListeners();
        this.logPageLoad();
    }

    /**
     * Setup event listeners
     */
    setupEventListeners() {
        if (this.twitchButton) {
            this.twitchButton.addEventListener('click', (e) => this.handleTwitchLogin(e));
        }
    }

    /**
     * Handle Twitch login click
     */
    handleTwitchLogin(e) {
        e.preventDefault();
        const href = this.twitchButton.getAttribute('href');
        
        if (href && href !== '#') {
            Toast.info('Redirigiendo a Twitch...');
            setTimeout(() => {
                window.location.href = href;
            }, 300);
        }
    }

    /**
     * Log de página cargada
     */
    logPageLoad() {
        console.log('🔐 Login page loaded - Twitch OAuth only');
    }
}

// Inicializar cuando el DOM esté listo
document.addEventListener('DOMContentLoaded', () => {
    new LoginPage();
});
