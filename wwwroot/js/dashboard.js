/**
 * Dashboard JavaScript
 * Funcionalidad del dashboard
 * - Dropdown menu
 * - Sidebar navigation
 * - Active nav item
 */

class DashboardPage {
    constructor() {
        this.userDropdownBtn = document.getElementById('user-dropdown-btn');
        this.userDropdownMenu = document.getElementById('user-dropdown-menu');
        this.sidebarToggleBtn = document.getElementById('sidebar-toggle');
        this.sidebar = document.querySelector('.sidebar');
        this.navItems = document.querySelectorAll('.nav-item');

        this.init();
    }

    init() {
        this.setupDropdownMenu();
        this.setupSidebarToggle();
        this.setActiveNavItem();
        this.setupEventListeners();
        this.logPageLoad();
    }

    /**
     * Setup dropdown menu
     */
    setupDropdownMenu() {
        if (!this.userDropdownBtn) return;

        this.userDropdownBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            const isOpen = this.userDropdownBtn.getAttribute('aria-expanded') === 'true';
            this.toggleDropdown(!isOpen);
        });

        // Cerrar al hacer click en un item del dropdown
        this.userDropdownMenu?.querySelectorAll('.dropdown-item').forEach(item => {
            item.addEventListener('click', () => {
                this.toggleDropdown(false);
            });
        });

        // Cerrar dropdown al hacer click afuera
        document.addEventListener('click', (e) => {
            if (!e.target.closest('.user-menu')) {
                this.toggleDropdown(false);
            }
        });
    }

    /**
     * Toggle dropdown visibility
     */
    toggleDropdown(show) {
        if (show) {
            this.userDropdownMenu?.classList.add('show');
            this.userDropdownBtn?.setAttribute('aria-expanded', 'true');
        } else {
            this.userDropdownMenu?.classList.remove('show');
            this.userDropdownBtn?.setAttribute('aria-expanded', 'false');
        }
    }

    /**
     * Setup sidebar toggle (mobile)
     */
    setupSidebarToggle() {
        if (!this.sidebarToggleBtn) return;

        this.sidebarToggleBtn.addEventListener('click', () => {
            if (this.sidebar) {
                this.sidebar.classList.toggle('show');
            }
        });

        // Cerrar sidebar al hacer click en un nav item
        this.navItems.forEach(item => {
            item.addEventListener('click', () => {
                if (window.innerWidth < 768) {
                    this.sidebar?.classList.remove('show');
                }
            });
        });
    }

    /**
     * Establece el item del menú como activo
     */
    setActiveNavItem() {
        const currentPath = window.location.pathname.toLowerCase();

        this.navItems.forEach(item => {
            const href = item.getAttribute('href')?.toLowerCase();
            const isActive = href && (
                (currentPath.includes('/dashboard') && href.includes('/dashboard')) ||
                (currentPath.includes('/settings') && href.includes('/settings'))
            );

            if (isActive && !href.includes('/logout')) {
                item.classList.add('active');
            } else {
                item.classList.remove('active');
            }
        });
    }

    /**
     * Setup otros event listeners
     */
    setupEventListeners() {
        // Actualizar nav item activo al hacer click
        this.navItems.forEach(item => {
            item.addEventListener('click', () => {
                this.setActiveNavItem();
            });
        });

        // Cerrar dropdown al presionar Escape
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                this.toggleDropdown(false);
            }
        });
    }

    /**
     * Log de página cargada
     */
    logPageLoad() {
        console.log('📊 Dashboard page loaded successfully');
    }
}

// Inicializar cuando el DOM esté listo
document.addEventListener('DOMContentLoaded', () => {
    new DashboardPage();
});
