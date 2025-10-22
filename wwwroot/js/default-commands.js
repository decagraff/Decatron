/**
 * Default Commands Management JavaScript - CORREGIDO CON PERMISOS
 * Gestión de comandos por defecto con cards, toggles y permisos completos
 */

class DefaultCommandsManager {
    constructor() {
        this.commands = {};
        this.userPermissions = {};
        this.currentContext = null;
        this.init();
    }

    async init() {
        await this.loadCurrentContext();
        await this.loadUserPermissions();
        this.setupEventListeners();
        this.updateUIBasedOnPermissions();
        await this.loadCommandStatuses();
        this.setupContextChangeListener();
    }

    async loadCurrentContext() {
        try {
            // Si existe channelSwitcher, usar su contexto
            if (window.channelSwitcher) {
                this.currentContext = window.channelSwitcher.getCurrentContext();
            }

            // Si no hay contexto del switcher, usar contexto básico
            if (!this.currentContext) {
                const response = await ApiHelper.get('/api/channel/context');
                if (response.success) {
                    this.currentContext = response.context;
                }
            }
        } catch (error) {
            console.error('Error loading context:', error);
        }
    }

    setupContextChangeListener() {
        // Escuchar cambios de contexto del ChannelSwitcher
        document.addEventListener('channelContextChanged', async (event) => {
            this.currentContext = event.detail.context;
            await this.loadUserPermissions();
            this.updateUIBasedOnPermissions();
            await this.loadCommandStatuses();
        });
    }

    setupEventListeners() {
        // Toggle switches para activar/desactivar comandos
        const toggles = document.querySelectorAll('.command-toggle input[type="checkbox"]');
        toggles.forEach(toggle => {
            toggle.addEventListener('change', this.handleCommandToggle.bind(this));
        });

        // Event listeners para botones de acción
        document.addEventListener('click', (e) => {
            if (e.target.matches('[data-action="open-settings"]')) {
                const commandName = e.target.dataset.command;
                this.openCommandSettings(commandName);
            }

            if (e.target.matches('[data-action="open-details"]')) {
                const commandName = e.target.dataset.command;
                this.openCommandDetails(commandName);
            }
        });
    }

    async loadUserPermissions() {
        try {
            const response = await ApiHelper.get('/api/user/permissions');
            if (response.success) {
                this.userPermissions = response.permissions;
            }
        } catch (error) {
            console.error('Error loading user permissions:', error);
            // Si no puede cargar permisos, asumir permisos mínimos para seguridad
            this.userPermissions = { accessLevel: 'commands', isOwner: false };
        }
    }

    updateUIBasedOnPermissions() {
        const userLevel = this.userPermissions.accessLevel || 'commands';
        const isOwner = this.userPermissions.isOwner || false;

        // Mostrar indicador de contexto si está gestionando otro canal
        this.updateContextIndicator();

        // Gestionar visibilidad de toggles (solo control total puede cambiar estados)
        document.querySelectorAll('.command-toggle').forEach(toggleContainer => {
            const canToggle = userLevel === 'control_total' || isOwner;

            if (!canToggle) {
                toggleContainer.style.display = 'none';

                // Agregar indicador de permisos si no existe
                const card = toggleContainer.closest('.command-card');
                if (card && !card.querySelector('.permission-indicator')) {
                    this.addPermissionIndicator(card, userLevel);
                }
            } else {
                toggleContainer.style.display = 'block';
            }
        });

        // Gestionar botones de configuración (requieren al menos 'commands')
        document.querySelectorAll('[data-action="open-settings"]').forEach(btn => {
            if (!this.hasPermission('commands')) {
                btn.style.display = 'none';
            } else {
                btn.style.display = 'inline-flex';
            }
        });

        // Botón de micro comandos
        const microCommandsBtn = document.querySelector('[onclick="openMicroCommands()"]');
        if (microCommandsBtn) {
            if (!this.hasPermission('commands')) {
                microCommandsBtn.style.display = 'none';
            } else {
                microCommandsBtn.style.display = 'inline-flex';
            }
        }

        // Actualizar textos de permisos
        this.updatePermissionTexts();
    }

    updateContextIndicator() {
        const existingIndicator = document.querySelector('.context-indicator');
        if (existingIndicator) {
            existingIndicator.remove();
        }

        // Solo mostrar si está gestionando otro canal
        if (window.isManagingOtherChannel && window.isManagingOtherChannel()) {
            const activeChannel = this.currentContext?.activeChannel;
            if (activeChannel) {
                const indicator = document.createElement('div');
                indicator.className = 'context-indicator';
                indicator.innerHTML = `
                    <div class="alert alert-info">
                        <strong>Gestionando:</strong> ${activeChannel.displayName} 
                        <span class="badge badge-${activeChannel.accessLevel}">${this.getPermissionLabel(activeChannel.accessLevel)}</span>
                    </div>
                `;

                const container = document.querySelector('.default-commands-container');
                if (container) {
                    container.insertBefore(indicator, container.firstChild);
                }
            }
        }
    }

    addPermissionIndicator(card, userLevel) {
        const indicator = document.createElement('div');
        indicator.className = 'permission-indicator';
        indicator.innerHTML = `
            <div class="permission-badge">
                <span class="badge badge-${userLevel}">Modo: ${this.getPermissionLabel(userLevel)}</span>
                <small>Solo visualización - Se requiere Control Total para cambiar configuración</small>
            </div>
        `;

        const header = card.querySelector('.command-card-header');
        if (header) {
            header.appendChild(indicator);
        }
    }

    updatePermissionTexts() {
        // Actualizar textos en botones según permisos
        document.querySelectorAll('[data-action="open-settings"]').forEach(btn => {
            if (!this.hasPermission('commands')) {
                btn.textContent = 'Sin Permisos';
                btn.disabled = true;
            } else {
                btn.disabled = false;
            }
        });
    }

    hasPermission(section) {
        const userLevel = this.userPermissions.accessLevel || 'commands';
        const isOwner = this.userPermissions.isOwner || false;

        // El propietario siempre tiene todos los permisos
        if (isOwner) return true;

        const permissionLevels = {
            'commands': 1,
            'moderation': 2,
            'control_total': 3
        };

        const sectionRequirements = {
            'commands': 1,
            'microcommands': 1,
            'overlays': 2,
            'user_management': 3,
            'toggle_commands': 3 // Solo control total puede activar/desactivar comandos
        };

        const userLevelValue = permissionLevels[userLevel] || 1;
        const requiredLevel = sectionRequirements[section] || 1;

        return userLevelValue >= requiredLevel;
    }

    getPermissionLabel(level) {
        const labels = {
            'commands': 'Solo Comandos',
            'moderation': 'Moderación',
            'control_total': 'Control Total',
            'owner': 'Propietario'
        };
        return labels[level] || 'Solo Comandos';
    }

    async loadCommandStatuses() {
        await this.loadCommandStatus('title');
        await this.loadCommandStatus('game');
    }

    async loadCommandStatus(commandName) {
        try {
            this.setCommandLoading(commandName, true);

            const response = await ApiHelper.get(`/api/commands/${commandName}/status`);

            if (response.success) {
                this.updateCommandStatus(commandName, response.botEnabled && response.commandEnabled);
                this.updateCommandToggle(commandName, response.commandEnabled);

                // Actualizar visibilidad del toggle basado en permisos del backend
                if (response.canToggle !== undefined) {
                    this.updateToggleVisibility(commandName, response.canToggle);
                }

                // Guardar información de permisos específicos del comando
                this.commands[commandName] = response;
            } else {
                this.updateCommandStatus(commandName, false);
                if (window.AlertManager) {
                    window.AlertManager.warning(`Error cargando estado del comando ${commandName}`);
                }
            }
        } catch (error) {
            console.error(`Error loading ${commandName} status:`, error);
            this.updateCommandStatus(commandName, false);
            if (window.AlertManager) {
                window.AlertManager.error(`Error conectando con el servidor para comando ${commandName}`);
            }
        } finally {
            this.setCommandLoading(commandName, false);
        }
    }

    updateToggleVisibility(commandName, canToggle) {
        const toggleElement = document.getElementById(`toggle-${commandName}`);
        if (toggleElement) {
            const toggleContainer = toggleElement.closest('.command-toggle');
            if (toggleContainer) {
                toggleContainer.style.display = canToggle ? 'block' : 'none';
            }
        }
    }

    async handleCommandToggle(event) {
        const toggle = event.target;
        const commandName = toggle.dataset.command;
        const isEnabled = toggle.checked;

        // Verificar permisos locales antes de enviar request
        if (!this.hasPermission('toggle_commands')) {
            toggle.checked = !isEnabled; // Revertir
            if (window.AlertManager) {
                window.AlertManager.error('No tienes permisos para cambiar el estado de comandos');
            }
            return;
        }

        try {
            // Deshabilitar toggle mientras se procesa
            toggle.disabled = true;
            this.setCommandLoading(commandName, true);

            const response = await ApiHelper.post(`/api/commands/${commandName}/toggle`, {
                enabled: isEnabled
            });

            if (response.success) {
                this.updateCommandStatus(commandName, isEnabled);
                if (window.AlertManager) {
                    window.AlertManager.success(`Comando ${commandName} ${isEnabled ? 'habilitado' : 'deshabilitado'}`);
                }

                // Actualizar el estado en memoria
                if (this.commands[commandName]) {
                    this.commands[commandName].commandEnabled = isEnabled;
                }
            } else {
                // Revertir toggle si falló
                toggle.checked = !isEnabled;
                if (window.AlertManager) {
                    window.AlertManager.error(response.message || `Error al ${isEnabled ? 'habilitar' : 'deshabilitar'} comando`);
                }
            }
        } catch (error) {
            console.error(`Error toggling ${commandName}:`, error);
            // Revertir toggle si falló
            toggle.checked = !isEnabled;
            if (window.AlertManager) {
                window.AlertManager.error('Error de conexión al cambiar estado del comando');
            }
        } finally {
            toggle.disabled = false;
            this.setCommandLoading(commandName, false);
        }
    }

    updateCommandStatus(commandName, isEnabled) {
        const statusElement = document.getElementById(`status-${commandName}`);
        if (!statusElement) return;

        statusElement.className = `status-badge ${isEnabled ? 'enabled' : 'disabled'}`;
        statusElement.innerHTML = `
            <span>${isEnabled ? '✓' : '✕'}</span>
            ${isEnabled ? 'Habilitado' : 'Deshabilitado'}
        `;
    }

    updateCommandToggle(commandName, isEnabled) {
        const toggle = document.getElementById(`toggle-${commandName}`);
        if (toggle) {
            toggle.checked = isEnabled;
        }
    }

    setCommandLoading(commandName, isLoading) {
        const statusElement = document.getElementById(`status-${commandName}`);
        if (!statusElement) return;

        if (isLoading) {
            statusElement.className = 'status-badge loading';
            statusElement.innerHTML = `
                <span class="status-spinner"></span>
                Verificando...
            `;
        }
    }

    // Funciones para los botones de acción
    openCommandSettings(commandName) {
        // Verificar permisos primero
        if (!this.hasPermission('commands')) {
            if (window.AlertManager) {
                window.AlertManager.error('No tienes permisos para configurar comandos');
            }
            return;
        }

        switch (commandName) {
            case 'title':
                this.openTitleSettings();
                break;
            case 'game':
                this.openGameSettings();
                break;
            default:
                if (window.AlertManager) {
                    window.AlertManager.info(`Configuración de ${commandName} próximamente`);
                }
        }
    }

    openCommandDetails(commandName) {
        if (!this.hasPermission('commands')) {
            if (window.AlertManager) {
                window.AlertManager.error('No tienes permisos para ver detalles de comandos');
            }
            return;
        }

        switch (commandName) {
            case 'title':
                window.location.href = '/commands/title/details';
                break;
            case 'game':
                window.location.href = '/commands/game/details';
                break;
            default:
                if (window.AlertManager) {
                    window.AlertManager.info(`Detalles de ${commandName} próximamente`);
                }
        }
    }

    openTitleSettings() {
        if (window.AlertManager) {
            window.AlertManager.info('Configuración avanzada del comando !title próximamente');
        }
    }

    openGameSettings() {
        if (window.AlertManager) {
            window.AlertManager.info('Configuración avanzada del comando !game próximamente');
        }
    }

    async showCommandStats(commandName) {
        if (!this.hasPermission('commands')) {
            if (window.AlertManager) {
                window.AlertManager.error('No tienes permisos para ver estadísticas');
            }
            return;
        }

        try {
            const response = await ApiHelper.get(`/api/commands/${commandName}/stats`);
            if (response.success) {
                if (window.AlertManager) {
                    window.AlertManager.info(`Comando usado ${response.usageCount} veces esta semana`);
                }
            }
        } catch (error) {
            console.error('Error loading command stats:', error);
        }
    }

    exportCommandsConfig() {
        if (!this.hasPermission('control_total')) {
            if (window.AlertManager) {
                window.AlertManager.error('Solo usuarios con Control Total pueden exportar configuración');
            }
            return;
        }

        const config = {
            commands: this.commands,
            context: this.currentContext,
            exportDate: new Date().toISOString(),
            version: '2.0'
        };

        const blob = new Blob([JSON.stringify(config, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);

        const link = document.createElement('a');
        link.href = url;
        link.download = 'comandos-config.json';
        link.click();

        URL.revokeObjectURL(url);
        if (window.AlertManager) {
            window.AlertManager.success('Configuración exportada');
        }
    }

    // Método público para refrescar permisos (llamado desde ChannelSwitcher)
    async refreshPermissions() {
        await this.loadCurrentContext();
        await this.loadUserPermissions();
        this.updateUIBasedOnPermissions();
        await this.loadCommandStatuses();
    }
}

// Funciones globales para los botones de las cards
function openCommandSettings(commandName) {
    if (window.defaultCommandsManager) {
        window.defaultCommandsManager.openCommandSettings(commandName);
    }
}

function openCommandDetails(commandName) {
    if (window.defaultCommandsManager) {
        window.defaultCommandsManager.openCommandDetails(commandName);
    }
}

function openMicroCommands() {
    // Verificar permisos antes de redireccionar
    if (window.defaultCommandsManager && !window.defaultCommandsManager.hasPermission('commands')) {
        if (window.AlertManager) {
            window.AlertManager.error('No tienes permisos para gestionar micro comandos');
        }
        return;
    }

    window.location.href = '/commands/microcommands';
}

// Función para exportar configuración (accesible globalmente)
function exportCommandsConfig() {
    if (window.defaultCommandsManager) {
        window.defaultCommandsManager.exportCommandsConfig();
    }
}

// Inicializar cuando se carga la página
document.addEventListener('DOMContentLoaded', () => {
    if (document.querySelector('.default-commands-container')) {
        window.defaultCommandsManager = new DefaultCommandsManager();
        console.log('📋 Default Commands Manager initialized with full permissions and context support');
    }
});