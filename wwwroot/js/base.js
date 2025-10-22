/**
 * Decatron Base JavaScript
 * Funcionalidad compartida para todas las páginas
 * - Theme Manager (único, sin duplicación)
 * - API Helper
 * - Utilidades compartidas
 */

// ============================================
// THEME MANAGER - Singleton
// ============================================
class ThemeManager {
    static instance = null;

    constructor() {
        // Singleton pattern - solo una instancia
        if (ThemeManager.instance) {
            return ThemeManager.instance;
        }
        
        this.themeKey = 'decatron-theme';
        this.init();
        ThemeManager.instance = this;
    }

    init() {
        this.loadTheme();
        this.setupEventListeners();
    }

    /**
     * Carga el tema guardado o el predeterminado del sistema
     */
    loadTheme() {
        const savedTheme = localStorage.getItem(this.themeKey);
        const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;

        if (savedTheme) {
            this.setTheme(savedTheme);
        } else if (prefersDark) {
            this.setTheme('dark');
        } else {
            this.setTheme('light');
        }
    }

    /**
     * Establece el tema actual
     * @param {string} theme - 'light' o 'dark'
     */
    setTheme(theme) {
        const isDark = theme === 'dark';
        document.documentElement.setAttribute('data-theme', theme);
        document.body.classList.toggle('dark-mode', isDark);
        localStorage.setItem(this.themeKey, theme);
        this.updateToggleButton();
    }

    /**
     * Alterna entre temas
     */
    toggleTheme() {
        const currentTheme = localStorage.getItem(this.themeKey) || 'light';
        const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
        this.setTheme(newTheme);
    }

    /**
     * Actualiza el botón del toggle
     */
    updateToggleButton() {
        const button = document.getElementById('theme-toggle');
        if (button) {
            const isDark = document.body.classList.contains('dark-mode');
            button.textContent = isDark ? '☀️' : '🌙';
            button.setAttribute('aria-pressed', isDark);
        }
    }

    /**
     * Setup de event listeners
     */
    setupEventListeners() {
        const button = document.getElementById('theme-toggle');
        if (button) {
            button.addEventListener('click', () => this.toggleTheme());
        }
    }

    /**
     * Obtiene el tema actual
     * @returns {string} 'light' o 'dark'
     */
    getCurrentTheme() {
        return localStorage.getItem(this.themeKey) || 'light';
    }
}

// ============================================
// API HELPER - Utilidades para fetch
// ============================================
class ApiHelper {
    /**
     * Request genérico
     * @param {string} url - URL del endpoint
     * @param {object} options - Opciones de fetch
     * @returns {Promise<object>} Respuesta JSON
     */
    static async request(url, options = {}) {
        const defaultOptions = {
            headers: {
                'Content-Type': 'application/json'
            }
        };

        try {
            const response = await fetch(url, { ...defaultOptions, ...options });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            return await response.json();
        } catch (error) {
            console.error('API Error:', error);
            throw error;
        }
    }

    /**
     * GET request
     * @param {string} url - URL del endpoint
     * @returns {Promise<object>} Respuesta JSON
     */
    static get(url) {
        return this.request(url, { method: 'GET' });
    }

    /**
     * POST request
     * @param {string} url - URL del endpoint
     * @param {object} data - Datos a enviar
     * @returns {Promise<object>} Respuesta JSON
     */
    static post(url, data) {
        return this.request(url, {
            method: 'POST',
            body: JSON.stringify(data)
        });
    }

    /**
     * PUT request
     * @param {string} url - URL del endpoint
     * @param {object} data - Datos a enviar
     * @returns {Promise<object>} Respuesta JSON
     */
    static put(url, data) {
        return this.request(url, {
            method: 'PUT',
            body: JSON.stringify(data)
        });
    }

    /**
     * DELETE request
     * @param {string} url - URL del endpoint
     * @returns {Promise<object>} Respuesta JSON
     */
    static delete(url) {
        return this.request(url, { method: 'DELETE' });
    }
}

// ============================================
// UTILITIES - Funciones de utilidad
// ============================================
const Utils = {
    /**
     * Espera un tiempo (para delays)
     * @param {number} ms - Milisegundos
     * @returns {Promise<void>}
     */
    delay: (ms) => new Promise(resolve => setTimeout(resolve, ms)),

    /**
     * Debounce para funciones
     * @param {function} func - Función a ejecutar
     * @param {number} wait - Milisegundos de espera
     * @returns {function} Función debounceada
     */
    debounce: (func, wait) => {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    },

    /**
     * Throttle para funciones
     * @param {function} func - Función a ejecutar
     * @param {number} limit - Milisegundos de límite
     * @returns {function} Función throttleada
     */
    throttle: (func, limit) => {
        let inThrottle;
        return function(...args) {
            if (!inThrottle) {
                func.apply(this, args);
                inThrottle = true;
                setTimeout(() => inThrottle = false, limit);
            }
        };
    }
};

// ============================================
// INITIALIZATION
// ============================================
document.addEventListener('DOMContentLoaded', () => {
    // Inicializar Theme Manager (singleton)
    new ThemeManager();
    console.log('🚀 Decatron Base initialized successfully');
});
