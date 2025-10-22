/**
 * Micro Commands Management JavaScript - CORREGIDO CON PERMISOS
 * Gestión de micro comandos !g con sistema completo de permisos
 */

class MicroCommandsManager {
    constructor() {
        this.microCommands = [];
        this.isLoading = false;
        this.userPermissions = {};
        this.currentContext = null;
        this.init();
    }

    async init() {
        await this.loadCurrentContext();
        await this.loadUserPermissions();
        this.setupEventListeners();
        this.updateUIBasedOnPermissions();
        await this.loadGCommandStatus();
        await this.loadMicroCommands();
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
            await this.loadGCommandStatus();
            await this.loadMicroCommands();
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

        // Solo mostrar formulario de creación si tiene permisos
        const createForm = document.querySelector('.create-microcommand-form');
        if (createForm) {
            if (!this.hasPermission('commands')) {
                createForm.style.display = 'none';
                this.addPermissionMessage();
            } else {
                createForm.style.display = 'block';
            }
        }

        // Actualizar botones de acción según permisos
        this.updateActionButtons();
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

                const container = document.querySelector('.microcommands-container');
                if (container) {
                    container.insertBefore(indicator, container.firstChild);
                }
            }
        }
    }

    addPermissionMessage() {
        const existingMessage = document.querySelector('.permission-message');
        if (existingMessage) return;

        const message = document.createElement('div');
        message.className = 'permission-message';
        message.innerHTML = `
            <div class="alert alert-warning">
                <h4>Permisos Insuficientes</h4>
                <p>Tu nivel actual: <strong>${this.getPermissionLabel(this.userPermissions.accessLevel)}</strong></p>
                <p>Se requiere nivel <strong>Solo Comandos</strong> o superior para gestionar micro comandos.</p>
            </div>
        `;

        const container = document.querySelector('.microcommands-container');
        if (container) {
            container.insertBefore(message, container.firstChild);
        }
    }

    updateActionButtons() {
        // Actualizar botones de editar y eliminar en cada item
        document.querySelectorAll('.microcommand-actions button').forEach(btn => {
            if (!this.hasPermission('commands')) {
                btn.disabled = true;
                btn.style.opacity = '0.5';
                btn.title = 'No tienes permisos para esta acción';
            } else {
                btn.disabled = false;
                btn.style.opacity = '1';
                btn.title = '';
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
            'microcommands': 1
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

    setupEventListeners() {
        // Botón crear micro comando
        const createBtn = document.getElementById('create-microcommand-btn');
        if (createBtn) {
            createBtn.addEventListener('click', this.createMicroCommand.bind(this));
        }

        // Botón refrescar
        const refreshBtn = document.getElementById('refresh-microcommands');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', this.refreshMicroCommands.bind(this));
        }

        // Enter en los inputs
        const commandInput = document.getElementById('new-command');
        const categoryInput = document.getElementById('new-category');

        if (commandInput) {
            commandInput.addEventListener('keypress', this.handleKeyPress.bind(this));
            commandInput.addEventListener('input', this.validateCommandInput.bind(this));
        }

        if (categoryInput) {
            categoryInput.addEventListener('keypress', this.handleKeyPress.bind(this));
        }
    }

    handleKeyPress(event) {
        if (event.key === 'Enter') {
            event.preventDefault();
            if (this.hasPermission('commands')) {
                this.createMicroCommand();
            } else {
                if (window.AlertManager) {
                    window.AlertManager.error('No tienes permisos para crear micro comandos');
                }
            }
        }
    }

    validateCommandInput(event) {
        const input = event.target;
        let value = input.value;

        // Auto-agregar ! si no lo tiene
        if (value.length > 0 && !value.startsWith('!')) {
            input.value = '!' + value;
        }

        // Validar caracteres permitidos
        const validPattern = /^![a-zA-Z0-9]+$/;
        if (value.length > 1 && !validPattern.test(value)) {
            input.value = value.replace(/[^a-zA-Z0-9!]/g, '');
        }
    }

    async loadGCommandStatus() {
        try {
            const response = await ApiHelper.get('/api/commands/microcommands/status');
            if (response.success) {
                this.updateGCommandStatus(true, response.channel);
                this.updatePermissionDisplay(response.userAccessLevel, response.canManage);
            } else {
                this.updateGCommandStatus(false);
            }
        } catch (error) {
            console.error('Error loading G command status:', error);
            this.updateGCommandStatus(false);
        }
    }

    updateGCommandStatus(isEnabled, channel) {
        const statusElement = document.getElementById('command-g-status');
        if (!statusElement) return;

        statusElement.className = `status-badge ${isEnabled ? 'enabled' : 'disabled'}`;
        statusElement.innerHTML = `
            <span>${isEnabled ? '✓' : '✕'}</span>
            Comando !g ${isEnabled ? 'Habilitado' : 'Deshabilitado'}
            ${channel ? ` en ${channel}` : ''}
        `;
    }

    updatePermissionDisplay(accessLevel, canManage) {
        const permissionDisplay = document.querySelector('.permission-display');
        if (permissionDisplay) {
            const badge = canManage ? 'badge-success' : 'badge-secondary';
            const text = canManage ? this.getPermissionLabel(accessLevel) : 'Sin Permisos';

            permissionDisplay.innerHTML = `<span class="badge ${badge}">${text}</span>`;
        }
    }

    async loadMicroCommands() {
        try {
            this.setLoading(true);

            const response = await ApiHelper.get('/api/commands/microcommands');

            if (response.success) {
                this.microCommands = response.microCommands || [];
                this.displayMicroCommands();

                // Actualizar información de permisos
                if (response.userAccessLevel) {
                    this.updatePermissionDisplay(response.userAccessLevel, response.canEdit);
                }
            } else {
                if (window.AlertManager) {
                    window.AlertManager.error('Error cargando micro comandos: ' + (response.message || 'Error desconocido'));
                }
            }
        } catch (error) {
            console.error('Error loading micro commands:', error);
            if (window.AlertManager) {
                window.AlertManager.error('Error de conexión al cargar micro comandos');
            }
        } finally {
            this.setLoading(false);
        }
    }

    async createMicroCommand() {
        // Verificar permisos antes de proceder
        if (!this.hasPermission('commands')) {
            if (window.AlertManager) {
                window.AlertManager.error('No tienes permisos para crear micro comandos');
            }
            return;
        }

        const commandInput = document.getElementById('new-command');
        const categoryInput = document.getElementById('new-category');

        if (!commandInput || !categoryInput) return;

        const command = commandInput.value.trim();
        const category = categoryInput.value.trim();

        // Validaciones
        if (!command || !category) {
            if (window.AlertManager) {
                window.AlertManager.warning('Complete todos los campos');
            }
            return;
        }

        if (!command.startsWith('!')) {
            if (window.AlertManager) {
                window.AlertManager.warning('El comando debe empezar con !');
            }
            return;
        }

        if (command.length < 2) {
            if (window.AlertManager) {
                window.AlertManager.warning('El comando debe tener al menos 2 caracteres');
            }
            return;
        }

        // Verificar palabras reservadas
        const reservedWords = ['!g', '!game', '!set', '!remove', '!delete', '!list', '!help'];
        if (reservedWords.includes(command.toLowerCase())) {
            if (window.AlertManager) {
                window.AlertManager.error(`'${command}' es una palabra reservada`);
            }
            return;
        }

        try {
            this.setCreateButtonLoading(true);

            const response = await ApiHelper.post('/api/commands/microcommands', {
                command: command,
                category: category
            });

            if (response.success) {
                if (window.AlertManager) {
                    window.AlertManager.success(response.message || 'Micro comando creado correctamente');
                }

                // Limpiar formulario
                commandInput.value = '';
                categoryInput.value = '';

                // Recargar lista
                await this.loadMicroCommands();
            } else {
                if (window.AlertManager) {
                    window.AlertManager.error(response.message || 'Error creando micro comando');
                }
            }
        } catch (error) {
            console.error('Error creating micro command:', error);
            if (window.AlertManager) {
                window.AlertManager.error('Error de conexión al crear micro comando');
            }
        } finally {
            this.setCreateButtonLoading(false);
        }
    }

    async deleteMicroCommand(id, command) {
        if (!this.hasPermission('commands')) {
            if (window.AlertManager) {
                window.AlertManager.error('No tienes permisos para eliminar micro comandos');
            }
            return;
        }

        if (!confirm(`¿Eliminar el micro comando ${command}?`)) {
            return;
        }

        try {
            const response = await ApiHelper.delete(`/api/commands/microcommands/${id}`);

            if (response.success) {
                if (window.AlertManager) {
                    window.AlertManager.success(`Micro comando ${command} eliminado`);
                }
                await this.loadMicroCommands();
            } else {
                if (window.AlertManager) {
                    window.AlertManager.error(response.message || 'Error eliminando micro comando');
                }
            }
        } catch (error) {
            console.error('Error deleting micro command:', error);
            if (window.AlertManager) {
                window.AlertManager.error('Error de conexión al eliminar micro comando');
            }
        }
    }

    async editMicroCommand(id, currentCommand, currentCategory) {
        if (!this.hasPermission('commands')) {
            if (window.AlertManager) {
                window.AlertManager.error('No tienes permisos para editar micro comandos');
            }
            return;
        }

        const newCategory = prompt(`Editar categoría para ${currentCommand}:`, currentCategory);

        if (newCategory === null || newCategory.trim() === currentCategory) {
            return; // Cancelado o sin cambios
        }

        if (!newCategory.trim()) {
            if (window.AlertManager) {
                window.AlertManager.warning('La categoría no puede estar vacía');
            }
            return;
        }

        try {
            const response = await ApiHelper.put(`/api/commands/microcommands/${id}`, {
                category: newCategory.trim()
            });

            if (response.success) {
                if (window.AlertManager) {
                    window.AlertManager.success(`Micro comando ${currentCommand} actualizado`);
                }
                await this.loadMicroCommands();
            } else {
                if (window.AlertManager) {
                    window.AlertManager.error(response.message || 'Error actualizando micro comando');
                }
            }
        } catch (error) {
            console.error('Error updating micro command:', error);
            if (window.AlertManager) {
                window.AlertManager.error('Error de conexión al actualizar micro comando');
            }
        }
    }

    displayMicroCommands() {
        const container = document.getElementById('microcommands-list');
        if (!container) return;

        if (this.microCommands.length === 0) {
            container.innerHTML = `
                <div class="microcommands-empty">
                    <div class="microcommands-empty-icon">🎮</div>
                    <p>No hay micro comandos creados</p>
                    <small>Crea tu primer micro comando arriba</small>
                </div>
            `;
            return;
        }

        const canManage = this.hasPermission('commands');

        container.innerHTML = this.microCommands.map(mc => `
            <div class="microcommand-item">
                <div class="microcommand-info">
                    <div class="microcommand-command">${this.escapeHtml(mc.shortCommand)}</div>
                    <div class="microcommand-category">${this.escapeHtml(mc.categoryName)}</div>
                    <div class="microcommand-meta">
                        Creado por ${this.escapeHtml(mc.createdBy)} - ${this.formatDate(mc.createdAt)}
                    </div>
                </div>
                <div class="microcommand-actions">
                    <button class="btn btn-secondary btn-sm" 
                            onclick="microCommandsManager.editMicroCommand(${mc.id}, '${this.escapeHtml(mc.shortCommand)}', '${this.escapeHtml(mc.categoryName)}')"
                            ${!canManage ? 'disabled title="No tienes permisos"' : ''}>
                        ✏️ Editar
                    </button>
                    <button class="btn btn-danger btn-sm" 
                            onclick="microCommandsManager.deleteMicroCommand(${mc.id}, '${this.escapeHtml(mc.shortCommand)}')"
                            ${!canManage ? 'disabled title="No tienes permisos"' : ''}>
                        🗑️ Eliminar
                    </button>
                </div>
            </div>
        `).join('');

        // Actualizar estado visual de botones según permisos
        this.updateActionButtons();
    }

    async refreshMicroCommands() {
        await this.loadMicroCommands();
        if (window.AlertManager) {
            window.AlertManager.info('Lista actualizada');
        }
    }

    setLoading(isLoading) {
        const container = document.getElementById('microcommands-list');
        if (!container) return;

        if (isLoading) {
            container.innerHTML = `
                <div class="microcommands-loading">
                    <span class="loading-spinner"></span>
                    Cargando micro comandos...
                </div>
            `;
        }
    }

    setCreateButtonLoading(isLoading) {
        const button = document.getElementById('create-microcommand-btn');
        if (!button) return;

        button.disabled = isLoading;
        button.innerHTML = isLoading
            ? '<span class="loading-spinner"></span> Creando...'
            : '➕ Crear Comando';
    }

    formatDate(dateString) {
        const date = new Date(dateString);
        return date.toLocaleDateString('es-ES', {
            day: '2-digit',
            month: '2-digit',
            year: '2-digit'
        });
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Método público para refrescar permisos (llamado desde ChannelSwitcher)
    async refreshPermissions() {
        await this.loadCurrentContext();
        await this.loadUserPermissions();
        this.updateUIBasedOnPermissions();
        await this.loadGCommandStatus();
        await this.loadMicroCommands();
    }
}

// Variable global para acceso desde HTML
let microCommandsManager;

// Inicializar cuando se carga la página
document.addEventListener('DOMContentLoaded', () => {
    if (document.querySelector('.microcommands-container')) {
        microCommandsManager = new MicroCommandsManager();
        console.log('🎮 Micro Commands Manager initialized with full permissions support');
    }
});