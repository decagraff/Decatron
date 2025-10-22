/**
 * Index Page JavaScript
 * Funcionalidad específica de la página de inicio (landing page)
 */

class IndexPage {
    constructor() {
        this.init();
    }

    init() {
        this.setupScrollAnimations();
        this.setupEventListeners();
        this.logPageLoad();
    }

    /**
     * Setup event listeners específicos de Index
     */
    setupEventListeners() {
        // Smooth scroll para enlaces internos
        document.querySelectorAll('a[href^="#"]').forEach(anchor => {
            anchor.addEventListener('click', (e) => this.handleAnchorClick(e));
        });
    }

    /**
     * Handle anchor links con scroll suave
     */
    handleAnchorClick(event) {
        const href = event.currentTarget.getAttribute('href');
        if (href === '#') return;

        event.preventDefault();
        const element = document.querySelector(href);
        if (element) {
            element.scrollIntoView({ behavior: 'smooth' });
        }
    }

    /**
     * Setup animaciones al hacer scroll
     */
    setupScrollAnimations() {
        // Opción 1: Usar Intersection Observer si existe
        if ('IntersectionObserver' in window) {
            this.initIntersectionObserver();
        }
    }

    /**
     * Intersection Observer para animaciones
     */
    initIntersectionObserver() {
        const observerOptions = {
            threshold: 0.1,
            rootMargin: '0px 0px -100px 0px'
        };

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('fade-in');
                    observer.unobserve(entry.target);
                }
            });
        }, observerOptions);

        // Observar cards
        document.querySelectorAll('.feature-card').forEach(card => {
            observer.observe(card);
        });
    }

    /**
     * Log de página cargada (solo desarrollo)
     */
    logPageLoad() {
        console.log('📄 Index page loaded successfully');
    }
}

// Inicializar cuando el DOM esté listo
document.addEventListener('DOMContentLoaded', () => {
    new IndexPage();
});
