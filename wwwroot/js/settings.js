/**
 * Settings JavaScript con sistema de permisos completo
 */

class SettingsManager {
    constructor() {
        this.userPermissions = {};
        this.init();
    }

    async init() {
        await this.loadUserPermissions();
        this.setupEventListeners();
        this.setActiveNavItem();
        this.updateUIBasedOnPermissions();
        this.displayCurrentUserPermissions();
        
        // Solo cargar usuarios si tiene permisos
        if (this.hasPermission('control_total')) {
            await this.loadChannelUsers();
        }
    }

    async loadUserPermissions() {
        try {
            const response = await ApiHelper.get('/api/user/permissions');
            if (response.success) {
                this.userPermissions = response.permissions;
                console.log('🔒 User permissions loaded:', this.userPermissions);
            }
        } catch (error) {
            console.error('Error loading user permissions:', error);
            // Default a control total si no puede cargar permisos
            this.userPermissions = { accessLevel: 'control_total', isOwner: true };
        }
    }

    displayCurrentUserPermissions() {
        const permissionDisplay = document.getElementById('current-user-permission');
        if (!permissionDisplay) return;

        const accessLevel = this.userPermissions.accessLevel || 'control_total';
        const isOwner = this.userPermissions.isOwner || false;

        const labels = {
            'commands': 'Solo Comandos',
            'moderation': 'Moderación',
            'control_total': 'Control Total'
        };

        const badgeClasses = {
            'commands': 'badge-commands',
            'moderation': 'badge-moderation',
            'control_total': 'badge-control_total'
        };

        const label = isOwner ? 'Propietario' : (labels[accessLevel] || 'Sin Acceso');
        const badgeClass = isOwner ? 'badge-owner' : (badgeClasses[accessLevel] || 'badge-secondary');

        permissionDisplay.innerHTML = `<span class="badge ${badgeClass}">${label}</span>`;
    }

    updateUIBasedOnPermissions() {
        const userLevel = this.userPermissions.accessLevel || 'control_total';

        // Solo ocultar elementos si NO tiene control total
        if (userLevel !== 'control_total') {
            // Ocultar configuración de bot
            const botSettings = document.querySelector('.bot-settings');
            if (botSettings) {
                botSettings.style.display = 'none';
            }

            // Ocultar gestión de usuarios
            const userManagement = document.querySelector('.user-management');
            if (userManagement) {
                userManagement.style.display = 'none';
            }

            // Mostrar indicador de permisos
            this.showPermissionIndicator(userLevel);
        }
    }

    showPermissionIndicator(level) {
        const labels = {
            'commands': 'Solo Comandos',
            'moderation': 'Moderación',
            'control_total': 'Control Total'
        };

        const indicator = document.createElement('div');
        indicator.className = 'permission-indicator';
        indicator.innerHTML = `
            <strong>Nivel de acceso:</strong> ${labels[level] || 'Desconocido'}
            <br><small>Algunas configuraciones pueden no estar disponibles.</small>
        `;

        const container = document.querySelector('.settings-container');
        if (container) {
            container.insertBefore(indicator, container.firstChild);
        }
    }

    setupEventListeners() {
        const saveBtn = document.getElementById('save-settings');
        if (saveBtn) {
            saveBtn.addEventListener('click', () => this.saveSettings());
        }

        const addAccessBtn = document.getElementById('add-access-btn');
        if (addAccessBtn) {
            addAccessBtn.addEventListener('click', () => this.showAddAccessDialog());
        }

        // Event listeners para modales - MEJORADO
        const modal = document.getElementById('addAccessModal');
        if (modal) {
            // Cerrar modal al hacer click en el overlay (no en el contenido)
            modal.addEventListener('click', (e) => {
                if (e.target === modal) {
                    this.closeAddAccessModal();
                }
            });

            // Cerrar con Escape
            document.addEventListener('keydown', (e) => {
                if (e.key === 'Escape' && modal.classList.contains('show')) {
                    this.closeAddAccessModal();
                }
            });
        }
    }

    setActiveNavItem() {
        const navItems = document.querySelectorAll('.nav-item');
        navItems.forEach(item => {
            if (item.href && item.href.includes('/settings')) {
                item.classList.add('active');
            } else {
                item.classList.remove('active');
            }
        });
    }

    async saveSettings() {
        // Verificar permisos antes de guardar
        if (!this.hasPermission('control_total')) {
            AlertManager.error('No tienes permisos para cambiar la configuración');
            return;
        }

        try {
            const settings = {
                botEnabled: document.getElementById('bot-enabled')?.checked || false
            };

            // CORREGIDO: Usar settings en lugar de requestData no definido
            const response = await ApiHelper.post('/api/settings/update', settings);

            if (response.success) {
                AlertManager.success('✅ Configuración guardada correctamente');
                // Actualizar estado del bot si es necesario
                await this.updateBotStatus();
            } else {
                AlertManager.error(response.message || 'Error al guardar la configuración');
            }
        } catch (error) {
            console.error('Error saving settings:', error);
            AlertManager.error('Error guardando configuración');
        }
    }

    async updateBotStatus() {
        try {
            const response = await ApiHelper.get('/api/settings/bot/status');
            if (response.success) {
                // Actualizar indicadores de estado del bot en la UI
                const statusElement = document.getElementById('bot-status');
                if (statusElement) {
                    statusElement.textContent = response.botConnected ? 'Conectado' : 'Desconectado';
                    statusElement.className = `status ${response.botConnected ? 'connected' : 'disconnected'}`;
                }
            }
        } catch (error) {
            console.error('Error updating bot status:', error);
        }
    }

    hasPermission(requiredLevel) {
        const userLevel = this.userPermissions.accessLevel || 'control_total';
        const permissionLevels = {
            'commands': 1,
            'moderation': 2,
            'control_total': 3
        };

        const userLevelValue = permissionLevels[userLevel] || 3;
        const requiredLevelValue = permissionLevels[requiredLevel] || 0;

        return userLevelValue >= requiredLevelValue;
    }

    // CORREGIDO: Modal centrado correctamente
    showAddAccessDialog() {
        if (!this.hasPermission('control_total')) {
            AlertManager.error('Solo usuarios con control total pueden agregar accesos');
            return;
        }

        const modal = document.getElementById('addAccessModal');
        if (modal) {
            // Agregar clase show para usar flexbox y centrar
            modal.classList.add('show');
            modal.style.display = 'flex'; // Asegurar que use flex

            // Limpiar campos
            const userIdField = document.getElementById('accessUserId');
            const permissionField = document.getElementById('accessPermission');
            if (userIdField) userIdField.value = '';
            if (permissionField) permissionField.value = '';

            // Focus en el primer campo
            setTimeout(() => {
                if (userIdField) userIdField.focus();
            }, 100);
        }
    }

    closeAddAccessModal() {
        const modal = document.getElementById('addAccessModal');
        if (modal) {
            modal.classList.remove('show');
            modal.style.display = 'none';
        }
    }

    async submitAddAccess() {
        const userId = document.getElementById('accessUserId')?.value?.trim();
        const permission = document.getElementById('accessPermission')?.value;

        if (!userId) {
            AlertManager.error('Ingresa el ID del usuario');
            return;
        }

        if (!permission) {
            AlertManager.error('Selecciona un nivel de permisos');
            return;
        }

        await this.addUserAccess(userId, permission);
        this.closeAddAccessModal();
    }

    async addUserAccess(userId, permission) {
        try {
            console.log('🔧 Adding user access:', { userId, permission });

            const requestData = {
                authorizedUserId: userId,
                permissionLevel: permission
            };

            console.log('📤 Request data:', requestData);

            const response = await ApiHelper.post('/api/settings/add-access', requestData);

            console.log('📥 Response:', response);

            if (response.success) {
                AlertManager.success('Usuario agregado correctamente');
                await this.loadChannelUsers(); // Recargar lista
            } else {
                console.error('❌ Server returned error:', response);
                AlertManager.error(response.message || 'Error agregando usuario');
            }
        } catch (error) {
            console.error('💥 Network/Parse error:', error);

            // Intentar obtener más detalles del error
            if (error.response) {
                console.error('Response status:', error.response.status);
                console.error('Response data:', await error.response.text());
            }

            AlertManager.error('Error agregando usuario');
        }
    }

    async removeAccess(accessId) {
        if (!this.hasPermission('control_total')) {
            AlertManager.error('No tienes permisos para remover usuarios');
            return;
        }

        if (!confirm('¿Deseas remover este usuario?')) return;

        try {
            const response = await ApiHelper.delete(`/api/settings/remove-access/${accessId}`);

            if (response.success) {
                AlertManager.success('Usuario removido');
                await this.loadChannelUsers(); // Recargar lista
            } else {
                AlertManager.error('Error removiendo usuario');
            }
        } catch (error) {
            console.error('Error removing access:', error);
            AlertManager.error('Error removiendo usuario');
        }
    }

    async loadChannelUsers() {
        if (!this.hasPermission('control_total')) {
            return; // No cargar si no tiene permisos
        }

        try {
            const response = await ApiHelper.get('/api/settings/channel-users');

            if (response.success) {
                this.updateUsersTable(response.users);
            }
        } catch (error) {
            console.error('Error loading channel users:', error);
            // No mostrar error ya que puede ser que el endpoint aún no esté implementado
        }
    }

    // CORREGIDO: Tabla con estilos contenidos
    updateUsersTable(users) {
        const tbody = document.querySelector('#users-table tbody');
        const tableContainer = document.getElementById('users-table-container');
        const noUsersMessage = document.getElementById('no-users-message');

        if (!tbody || !tableContainer) return;

        tbody.innerHTML = '';

        if (users && users.length > 0) {
            // Mostrar tabla y ocultar mensaje
            tableContainer.style.display = 'block';
            if (noUsersMessage) noUsersMessage.style.display = 'none';

            users.forEach(user => {
                const row = document.createElement('tr');

                // Truncar nombres largos
                const displayName = user.displayName || user.username;
                const truncatedName = displayName.length > 15 ? displayName.substring(0, 15) + '...' : displayName;
                const truncatedUsername = user.username.length > 12 ? user.username.substring(0, 12) + '...' : user.username;

                row.innerHTML = `
                    <td title="${displayName}">${truncatedName}</td>
                    <td title="@${user.username}">@${truncatedUsername}</td>
                    <td>
                        <span class="badge badge-${user.accessLevel}" title="${user.permissionLabel}">
                            ${user.permissionLabel}
                        </span>
                    </td>
                    <td title="${user.grantedBy}">${user.grantedBy.length > 10 ? user.grantedBy.substring(0, 10) + '...' : user.grantedBy}</td>
                    <td title="${new Date(user.createdAt).toLocaleString()}">${new Date(user.createdAt).toLocaleDateString()}</td>
                    <td>
                        <button class="btn btn-sm btn-secondary" onclick="settingsManager.removeAccess(${user.id})" title="Remover usuario">
                            ❌
                        </button>
                    </td>
                `;
                tbody.appendChild(row);
            });
        } else {
            // Ocultar tabla y mostrar mensaje
            tableContainer.style.display = 'none';
            if (noUsersMessage) noUsersMessage.style.display = 'block';
        }
    }
           
    showAccountManagement() {
        // Implementar modal o página para gestión de cuentas
        const modal = document.createElement('div');
        modal.className = 'modal show';
        modal.style.display = 'flex';
        modal.innerHTML = `
            <div class="modal-content">
                <div class="modal-header">
                    <h3 class="modal-title">Gestionar Cuentas</h3>
                    <button type="button" class="close" onclick="this.closest('.modal').remove()">&times;</button>
                </div>
                <div class="modal-body">
                    <p>Aquí puedes cambiar entre las cuentas autorizadas para gestionar este canal.</p>
                    <div id="available-accounts">
                        <p><i class="fas fa-spinner fa-spin"></i> Cargando cuentas disponibles...</p>
                    </div>
                </div>
            </div>
        `;

        document.body.appendChild(modal);

        // Cargar cuentas disponibles
        this.loadAvailableAccounts();
    }

    async loadAvailableAccounts() {
        try {
            // Usar fetch directamente para evitar errores en ApiHelper
            const response = await fetch('/api/user/available-accounts', {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            const container = document.getElementById('available-accounts');
            if (!container) return;

            if (response.ok) {
                const data = await response.json();
                if (data.success && data.accounts && data.accounts.length > 0) {
                    container.innerHTML = data.accounts.map(account => `
                        <div class="account-option" onclick="settingsManager.switchToAccount('${account.id}')">
                            <img src="${account.profileImageUrl || '/images/default-avatar.png'}" alt="${account.displayName}" width="40" height="40">
                            <div class="account-info">
                                <strong>${account.displayName}</strong>
                                <small>@${account.username}</small>
                                ${account.isOwner ? '<span class="badge badge-owner">Propietario</span>' : `<span class="badge badge-${account.accessLevel}">${account.permissionLabel}</span>`}
                            </div>
                        </div>
                    `).join('');
                } else {
                    container.innerHTML = '<p>No tienes acceso a otras cuentas.</p>';
                }
            } else {
                container.innerHTML = '<p>Error cargando cuentas disponibles.</p>';
            }
        } catch (error) {
            const container = document.getElementById('available-accounts');
            if (container) {
                container.innerHTML = '<p>Error cargando cuentas disponibles.</p>';
            }
        }
    }

    async switchToAccount(accountId) {
        try {
            const response = await ApiHelper.post('/api/user/switch-account', { accountId });

            if (response.success) {
                AlertManager.success('Cambiando a cuenta seleccionada...');
                // Recargar página después de un breve delay
                setTimeout(() => {
                    window.location.reload();
                }, 1000);
            } else {
                AlertManager.error(response.message || 'Error cambiando de cuenta');
            }
        } catch (error) {
            console.error('Error switching account:', error);
            AlertManager.error('Error cambiando de cuenta');
        }
    }
}

// Funciones globales para mantener compatibilidad
function closeAddAccessModal() {
    if (window.settingsManager) {
        window.settingsManager.closeAddAccessModal();
    }
}

function submitAddAccess() {
    if (window.settingsManager) {
        window.settingsManager.submitAddAccess();
    }
}

function removeAccess(accessId) {
    if (window.settingsManager) {
        window.settingsManager.removeAccess(accessId);
    }
}

// Funciones globales para dropdown unificado
function toggleUserDropdown() {
    const dropdown = document.getElementById('userDropdown');
    const arrow = document.querySelector('.dropdown-arrow');

    if (dropdown && dropdown.classList.contains('show')) {
        dropdown.classList.remove('show');
        if (arrow) arrow.classList.remove('rotated');
    } else if (dropdown) {
        dropdown.classList.add('show');
        if (arrow) arrow.classList.add('rotated');
    }
}

// Cerrar dropdown al hacer click fuera
document.addEventListener('click', function (event) {
    const container = document.querySelector('.user-dropdown-container');
    const dropdown = document.getElementById('userDropdown');

    if (container && !container.contains(event.target) && dropdown) {
        dropdown.classList.remove('show');
        const arrow = document.querySelector('.dropdown-arrow');
        if (arrow) arrow.classList.remove('rotated');
    }
});

// Inicializar cuando se carga la página
document.addEventListener('DOMContentLoaded', () => {
    window.settingsManager = new SettingsManager();
    console.log('⚙️ Settings Manager initialized with permissions');
});

console.log('Settings script loaded with permissions');