/**
 * Channel Switcher JavaScript
 * Maneja el cambio de contexto entre cuentas de forma visual sin cambiar URLs
 * Similar a Google o StreamElements
 */

class ChannelSwitcher {
    constructor() {
        this.currentContext = null;
        this.availableChannels = [];
        this.isInitialized = false;
        this.init();
    }

    async init() {
        try {
            await this.loadCurrentContext();
            await this.loadAvailableChannels();

            // Solo crear UI si realmente es necesario
            if (this.shouldShowChannelSwitcher()) {
                this.createChannelSwitcherUI();
            }

            this.updateNavbarContext();
            this.isInitialized = true;
            console.log('🔄 Channel Switcher initialized', this.currentContext);
        } catch (error) {
            console.error('Error initializing Channel Switcher:', error);
            // Si falla, no mostrar el switcher
        }
    }

    async loadCurrentContext() {
        try {
            const response = await fetch('/api/channel/context', {
                method: 'GET',
                credentials: 'include'
            });

            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    this.currentContext = result.context;
                }
            }
        } catch (error) {
            console.error('Error loading current context:', error);
        }
    }

    async loadAvailableChannels() {
        try {
            const response = await fetch('/api/channel/available', {
                method: 'GET',
                credentials: 'include'
            });

            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    this.availableChannels = result.channels;
                }
            }
        } catch (error) {
            console.error('Error loading available channels:', error);
        }
    }

    createChannelSwitcherUI() {
        // Solo crear UI si hay múltiples canales Y el usuario no es propietario único
        if (this.availableChannels.length <= 1) {
            // Si no hay múltiples cuentas, ocultar cualquier channel switcher existente
            const existingSwitcher = document.querySelector('.channel-switcher');
            if (existingSwitcher) {
                existingSwitcher.style.display = 'none';
            }
            return;
        }

        const navbar = document.querySelector('.navbar-right');
        if (!navbar) return;

        // Verificar si ya existe para evitar duplicados
        const existingSwitcher = document.querySelector('.channel-switcher');
        if (existingSwitcher) {
            existingSwitcher.remove();
        }

        // Crear el dropdown de cambio de contexto
        const channelSwitcher = document.createElement('div');
        channelSwitcher.className = 'channel-switcher';
        channelSwitcher.innerHTML = `
        <button class="channel-switcher-btn" id="channel-switcher-btn" 
                aria-haspopup="true" aria-expanded="false" title="Cambiar cuenta">
            <div class="channel-context">
                <img src="${this.currentContext?.activeChannel?.profileImageUrl || '/images/default-avatar.png'}" 
                     alt="Canal activo" class="channel-avatar" />
                <div class="channel-info">
                    <span class="channel-name">${this.currentContext?.activeChannel?.displayName || 'Canal'}</span>
                    <span class="channel-access">${this.getAccessLevelLabel(this.currentContext?.activeChannel?.accessLevel)}</span>
                </div>
                <span class="channel-dropdown-icon">▼</span>
            </div>
        </button>

        <div class="channel-dropdown" id="channel-dropdown" role="menu">
            <div class="channel-dropdown-header">
                <span>Gestionar cuentas</span>
            </div>
            <div class="channel-list" id="channel-list">
                <!-- Se llena dinámicamente -->
            </div>
        </div>
    `;

        // Insertar ANTES del user menu para mejor orden visual
        const userMenu = navbar.querySelector('.user-menu');
        if (userMenu) {
            navbar.insertBefore(channelSwitcher, userMenu);
        } else {
            navbar.appendChild(channelSwitcher);
        }

        // Llenar la lista de canales
        this.populateChannelList();
        this.setupChannelSwitcherEvents();
    }


    shouldShowChannelSwitcher() {
        // Solo mostrar si:
        // - Hay múltiples canales disponibles
        // - El usuario actual no es propietario de todos
        // - O tiene permisos en canales de otros
        return this.availableChannels.length > 1 &&
            this.availableChannels.some(channel => !channel.isOwner);
    }

    populateChannelList() {
        const channelList = document.getElementById('channel-list');
        if (!channelList) return;

        channelList.innerHTML = '';

        this.availableChannels.forEach(channel => {
            const isActive = channel.channelId === this.currentContext?.activeChannelId;

            const channelItem = document.createElement('div');
            channelItem.className = `channel-item ${isActive ? 'active' : ''}`;
            channelItem.innerHTML = `
                <img src="${channel.profileImageUrl || '/images/default-avatar.png'}" 
                     alt="${channel.displayName}" class="channel-item-avatar" />
                <div class="channel-item-info">
                    <span class="channel-item-name">${channel.displayName}</span>
                    <span class="channel-item-username">@${channel.login}</span>
                    <span class="channel-item-access">${this.getAccessLevelLabel(channel.accessLevel)}</span>
                </div>
                ${isActive ? '<span class="channel-item-active">✓</span>' : ''}
            `;

            if (!isActive) {
                channelItem.addEventListener('click', () => {
                    this.switchToChannel(channel.channelId);
                });
                channelItem.style.cursor = 'pointer';
            }

            channelList.appendChild(channelItem);
        });
    }

    setupChannelSwitcherEvents() {
        const switcherBtn = document.getElementById('channel-switcher-btn');
        const dropdown = document.getElementById('channel-dropdown');

        if (!switcherBtn || !dropdown) return;

        // Toggle dropdown
        switcherBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            const isOpen = dropdown.classList.contains('show');

            // Cerrar otros dropdowns
            this.closeAllDropdowns();

            if (!isOpen) {
                dropdown.classList.add('show');
                switcherBtn.setAttribute('aria-expanded', 'true');
            }
        });

        // Cerrar dropdown al hacer click fuera
        document.addEventListener('click', (e) => {
            if (!switcherBtn.contains(e.target) && !dropdown.contains(e.target)) {
                dropdown.classList.remove('show');
                switcherBtn.setAttribute('aria-expanded', 'false');
            }
        });

        // Cerrar con ESC
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                dropdown.classList.remove('show');
                switcherBtn.setAttribute('aria-expanded', 'false');
            }
        });
    }

    async switchToChannel(channelId) {
        try {
            // Mostrar loading
            this.showSwitchingState();

            const response = await fetch('/api/channel/switch', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                credentials: 'include',
                body: JSON.stringify({ channelId: channelId })
            });

            const result = await response.json();

            if (result.success) {
                // Actualizar contexto local
                await this.loadCurrentContext();

                // Actualizar UI
                this.updateNavbarContext();
                this.populateChannelList();

                // Cerrar dropdown
                this.closeAllDropdowns();

                // Notificar a otros componentes del cambio
                this.notifyContextChange();

                // Mostrar mensaje de éxito
                if (window.AlertManager) {
                    window.AlertManager.success(`Contexto cambiado a ${result.activeChannel.displayName}`);
                }

                console.log('✅ Context switched successfully', result.activeChannel);
            } else {
                throw new Error(result.message || 'Error switching context');
            }
        } catch (error) {
            console.error('Error switching channel:', error);
            if (window.AlertManager) {
                window.AlertManager.error('Error cambiando de cuenta');
            }
        } finally {
            this.hideSwitchingState();
        }
    }

    updateNavbarContext() {
        if (!this.currentContext?.activeChannel) return;

        const activeChannel = this.currentContext.activeChannel;

        // Actualizar el botón del switcher
        const channelAvatar = document.querySelector('.channel-avatar');
        const channelName = document.querySelector('.channel-name');
        const channelAccess = document.querySelector('.channel-access');

        if (channelAvatar) {
            channelAvatar.src = activeChannel.profileImageUrl || '/images/default-avatar.png';
            channelAvatar.alt = activeChannel.displayName;
        }

        if (channelName) {
            channelName.textContent = activeChannel.displayName;
        }

        if (channelAccess) {
            channelAccess.textContent = this.getAccessLevelLabel(activeChannel.accessLevel);
        }

        // Actualizar el menú de usuario para mostrar contexto
        this.updateUserMenuContext();
    }

    updateUserMenuContext() {
        const userButton = document.querySelector('.user-button');
        if (!userButton) return;

        const activeChannel = this.currentContext?.activeChannel;
        const userId = this.currentContext?.userId;

        // Si está gestionando otro canal, mostrar indicador visual
        if (activeChannel && activeChannel.channelId !== userId) {
            userButton.classList.add('managing-context');
            userButton.title = `Gestionando: ${activeChannel.displayName}`;
        } else {
            userButton.classList.remove('managing-context');
            userButton.title = 'Menú de usuario';
        }
    }

    showSwitchingState() {
        const switcherBtn = document.querySelector('.channel-switcher-btn');
        if (switcherBtn) {
            switcherBtn.classList.add('switching');
            switcherBtn.disabled = true;
        }
    }

    hideSwitchingState() {
        const switcherBtn = document.querySelector('.channel-switcher-btn');
        if (switcherBtn) {
            switcherBtn.classList.remove('switching');
            switcherBtn.disabled = false;
        }
    }

    closeAllDropdowns() {
        // Cerrar channel dropdown
        const channelDropdown = document.getElementById('channel-dropdown');
        if (channelDropdown) {
            channelDropdown.classList.remove('show');
        }

        // Cerrar user dropdown
        const userDropdown = document.getElementById('user-dropdown-menu');
        if (userDropdown) {
            userDropdown.classList.remove('show');
        }

        // Resetear aria-expanded
        document.querySelectorAll('[aria-expanded="true"]').forEach(elem => {
            elem.setAttribute('aria-expanded', 'false');
        });
    }

    notifyContextChange() {
        // Notificar a otros componentes que el contexto cambió
        const event = new CustomEvent('channelContextChanged', {
            detail: {
                context: this.currentContext,
                activeChannel: this.currentContext?.activeChannel
            }
        });
        document.dispatchEvent(event);

        // También refrescar permisos de navegación
        if (window.refreshNavigationPermissions) {
            window.refreshNavigationPermissions();
        }

        // Refrescar configuraciones si estamos en settings
        if (window.settingsManager && typeof window.settingsManager.refreshPermissions === 'function') {
            window.settingsManager.refreshPermissions();
        }
    }

    getAccessLevelLabel(accessLevel) {
        const labels = {
            'owner': 'Propietario',
            'control_total': 'Control Total',
            'moderation': 'Moderación',
            'commands': 'Solo Comandos'
        };
        return labels[accessLevel] || 'Sin Acceso';
    }

    // Métodos públicos para uso externo
    getCurrentContext() {
        return this.currentContext;
    }

    async refreshContext() {
        await this.loadCurrentContext();
        await this.loadAvailableChannels();
        this.updateNavbarContext();
        this.populateChannelList();
    }

    isManagingOtherChannel() {
        return this.currentContext &&
            this.currentContext.activeChannelId !== this.currentContext.userId;
    }
}

// CSS para el Channel Switcher
const channelSwitcherStyles = `
<style>
.channel-switcher {
    position: relative;
    margin-right: 16px;
}

.channel-switcher-btn {
    background: none;
    border: none;
    cursor: pointer;
    padding: 8px 12px;
    border-radius: 8px;
    transition: background-color 0.2s;
    display: flex;
    align-items: center;
    gap: 8px;
}

.channel-switcher-btn:hover {
    background-color: var(--bg-secondary);
}

.channel-switcher-btn.switching {
    opacity: 0.6;
    cursor: not-allowed;
}

.channel-context {
    display: flex;
    align-items: center;
    gap: 8px;
}

.channel-avatar {
    width: 32px;
    height: 32px;
    border-radius: 50%;
    object-fit: cover;
    border: 2px solid var(--border-color);
}

.channel-info {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    min-width: 0;
}

.channel-name {
    font-weight: 600;
    color: var(--text-primary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    max-width: 120px;
}

.channel-access {
    font-size: 11px;
    color: var(--text-muted);
    white-space: nowrap;
}

.channel-dropdown-icon {
    color: var(--text-muted);
    font-size: 12px;
    transition: transform 0.2s;
}

.channel-switcher-btn[aria-expanded="true"] .channel-dropdown-icon {
    transform: rotate(180deg);
}

.channel-dropdown {
    position: absolute;
    top: 100%;
    right: 0;
    background: var(--bg-primary);
    border: 1px solid var(--border-color);
    border-radius: 12px;
    box-shadow: 0 4px 12px rgba(0,0,0,0.15);
    min-width: 280px;
    max-width: 320px;
    z-index: 1000;
    opacity: 0;
    visibility: hidden;
    transform: translateY(-8px);
    transition: all 0.2s ease;
}

.channel-dropdown.show {
    opacity: 1;
    visibility: visible;
    transform: translateY(0);
}

.channel-dropdown-header {
    padding: 12px 16px;
    border-bottom: 1px solid var(--border-color);
    font-weight: 600;
    color: var(--text-primary);
    font-size: 14px;
}

.channel-list {
    max-height: 300px;
    overflow-y: auto;
}

.channel-item {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px 16px;
    cursor: pointer;
    transition: background-color 0.2s;
    position: relative;
}

.channel-item:hover:not(.active) {
    background-color: var(--bg-secondary);
}

.channel-item.active {
    background-color: var(--bg-accent);
    cursor: default;
}

.channel-item-avatar {
    width: 40px;
    height: 40px;
    border-radius: 50%;
    object-fit: cover;
    border: 2px solid var(--border-color);
    flex-shrink: 0;
}

.channel-item-info {
    display: flex;
    flex-direction: column;
    min-width: 0;
    flex: 1;
}

.channel-item-name {
    font-weight: 600;
    color: var(--text-primary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.channel-item-username {
    font-size: 13px;
    color: var(--text-muted);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.channel-item-access {
    font-size: 11px;
    color: var(--text-accent);
    font-weight: 500;
}

.channel-item-active {
    color: var(--text-success);
    font-weight: bold;
    font-size: 16px;
}

.user-button.managing-context {
    position: relative;
}

.user-button.managing-context::after {
    content: '';
    position: absolute;
    top: -2px;
    right: -2px;
    width: 8px;
    height: 8px;
    background-color: var(--color-accent);
    border-radius: 50%;
    border: 2px solid var(--bg-primary);
}

@media (max-width: 768px) {
    .channel-switcher {
        display: none;
    }
    
    .channel-info {
        display: none;
    }
}
</style>
`;

// Inyectar estilos
document.head.insertAdjacentHTML('beforeend', channelSwitcherStyles);

// Inicializar cuando se carga la página
document.addEventListener('DOMContentLoaded', () => {
    // Esperar un poco para que otros scripts se carguen
    setTimeout(() => {
        window.channelSwitcher = new ChannelSwitcher();
        console.log('🔄 Channel Switcher ready');
    }, 500);
});

// Funciones globales para compatibilidad
window.getActiveChannel = function () {
    return window.channelSwitcher ? window.channelSwitcher.getCurrentContext() : null;
};

window.refreshChannelContext = async function () {
    if (window.channelSwitcher) {
        await window.channelSwitcher.refreshContext();
    }
};

window.isManagingOtherChannel = function () {
    return window.channelSwitcher ? window.channelSwitcher.isManagingOtherChannel() : false;
};