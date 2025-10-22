/**
 * Navigation Permissions JavaScript
 * Maneja la visibilidad de elementos de navegación según permisos del usuario
 */

class NavigationPermissionsManager {
    constructor() {
        this.userPermissions = {};
        this.init();
    }

    async init() {
        await this.loadUserPermissions();
        this.updateNavigationPermissions();
        this.updatePermissionIndicators();
    }

    async loadUserPermissions() {
        try {
            const response = await fetch('/api/user/permissions', {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                },
                credentials: 'include'
            });

            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    this.userPermissions = result.permissions;
                    console.log('🔒 User permissions loaded:', this.userPermissions);
                }
            }
        } catch (error) {
            console.error('Error loading user permissions:', error);
            // Default a control total si no puede cargar permisos (probablemente es el owner)
            this.userPermissions = {
                accessLevel: 'control_total',
                isOwner: true,
                sections: {
                    commands: true,
                    microcommands: true,
                    overlays: true,
                    userManagement: true
                }
            };
        }
    }

    updateNavigationPermissions() {
        const userLevel = this.userPermissions.accessLevel || 'control_total';

        // Mapeo de niveles de permisos
        const permissionLevels = {
            'commands': 1,
            'moderation': 2,
            'control_total': 3
        };

        const userLevelValue = permissionLevels[userLevel] || 3;

        // Ocultar/mostrar secciones según permisos
        document.querySelectorAll('[data-permission]').forEach(element => {
            const requiredPermission = element.getAttribute('data-permission');
            const requiredLevel = permissionLevels[requiredPermission] || 0;

            if (userLevelValue < requiredLevel) {
                element.style.display = 'none';
            } else {
                element.style.display = '';
            }
        });

        // Caso especial: si no es owner, ocultar configuración
        if (!this.userPermissions.isOwner) {
            const settingsLinks = document.querySelectorAll('a[href="/settings"]');
            settingsLinks.forEach(link => {
                if (userLevel !== 'control_total') {
                    link.style.display = 'none';
                }
            });
        }
    }

    updatePermissionIndicators() {
        const accessLevel = this.userPermissions.accessLevel || 'control_total';
        const isOwner = this.userPermissions.isOwner || false;

        const permissionLabels = {
            'commands': 'Solo Comandos',
            'moderation': 'Moderación',
            'control_total': 'Control Total'
        };

        const badgeClasses = {
            'commands': 'badge-info',
            'moderation': 'badge-warning',
            'control_total': 'badge-success'
        };

        const label = isOwner ? 'Propietario' : (permissionLabels[accessLevel] || 'Sin Acceso');
        const badgeClass = isOwner ? 'badge-owner' : (badgeClasses[accessLevel] || 'badge-secondary');

        // Actualizar indicadores
        const indicators = document.querySelectorAll('.permission-badge');
        indicators.forEach(badge => {
            badge.textContent = label;
            badge.className = `permission-badge ${badgeClass}`;
        });

        // Mostrar indicadores solo si no es control total o si es editor
        const shouldShowIndicator = !isOwner && accessLevel !== 'control_total';

        const sidebarIndicator = document.getElementById('sidebar-permission-indicator');
        const navbarIndicator = document.getElementById('navbar-permission-indicator');

        if (shouldShowIndicator) {
            if (sidebarIndicator) sidebarIndicator.style.display = 'block';
            if (navbarIndicator) navbarIndicator.style.display = 'block';
        } else {
            if (sidebarIndicator) sidebarIndicator.style.display = 'none';
            if (navbarIndicator) navbarIndicator.style.display = 'none';
        }
    }

    hasPermission(section) {
        const userLevel = this.userPermissions.accessLevel || 'control_total';
        const permissionLevels = {
            'commands': 1,
            'moderation': 2,
            'control_total': 3
        };

        const sectionRequirements = {
            'commands': 1,
            'microcommands': 1,
            'overlays': 2,
            'timers': 2,
            'loyalty': 2,
            'settings': 3,
            'user_management': 3
        };

        const userLevelValue = permissionLevels[userLevel] || 3;
        const requiredLevel = sectionRequirements[section] || 0;

        return userLevelValue >= requiredLevel;
    }

    // Método para que otros scripts puedan verificar permisos
    getUserPermissions() {
        return this.userPermissions;
    }

    // Método para refrescar permisos (útil después de cambios)
    async refreshPermissions() {
        await this.loadUserPermissions();
        this.updateNavigationPermissions();
        this.updatePermissionIndicators();
    }
}

// CSS adicional para los indicadores de permisos
const permissionStyles = `
<style>
.nav-section-header {
    padding: 8px 16px;
    color: var(--text-muted);
    font-size: 12px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.nav-section-title {
    display: block;
}

.sidebar-footer {
    padding: 16px;
    border-top: 1px solid var(--border-color);
    margin-top: auto;
}

.permission-indicator {
    text-align: center;
}

.permission-badge {
    display: inline-block;
    padding: 4px 8px;
    border-radius: 12px;
    font-size: 11px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.025em;
}

.badge-info {
    background-color: #3b82f6;
    color: white;
}

.badge-warning {
    background-color: #f59e0b;
    color: white;
}

.badge-success {
    background-color: #10b981;
    color: white;
}

.badge-owner {
    background-color: #8b5cf6;
    color: white;
}

.badge-secondary {
    background-color: #6b7280;
    color: white;
}

.navbar-permission {
    margin-right: 16px;
}

@media (max-width: 768px) {
    .sidebar-footer {
        display: none;
    }
    
    .navbar-permission {
        display: block !important;
    }
}
</style>
`;

// Inyectar estilos
document.head.insertAdjacentHTML('beforeend', permissionStyles);

// Inicializar cuando se carga la página
document.addEventListener('DOMContentLoaded', () => {
    window.navigationPermissionsManager = new NavigationPermissionsManager();
    console.log('🚀 Navigation Permissions Manager initialized');
});

// Exponer funciones globales para uso en otros scripts
window.hasNavigationPermission = function (section) {
    return window.navigationPermissionsManager ?
        window.navigationPermissionsManager.hasPermission(section) :
        true; // Default true si no está cargado
};

window.getUserNavigationPermissions = function () {
    return window.navigationPermissionsManager ?
        window.navigationPermissionsManager.getUserPermissions() :
        {};
};

window.refreshNavigationPermissions = async function () {
    if (window.navigationPermissionsManager) {
        await window.navigationPermissionsManager.refreshPermissions();
    }
};