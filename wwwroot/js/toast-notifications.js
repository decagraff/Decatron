/**
 * Toast Notification System
 * Notificaciones en esquina superior derecha, sin interferir con nada
 */

class ToastNotification {
    constructor() {
        this.container = null;
        this.initContainer();
    }

    initContainer() {
        // Crear contenedor si no existe
        if (!document.getElementById('toast-container')) {
            this.container = document.createElement('div');
            this.container.id = 'toast-container';
            this.container.className = 'toast-container';
            document.body.appendChild(this.container);
        } else {
            this.container = document.getElementById('toast-container');
        }
    }

    show(message, type = 'info', duration = 4000) {
        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;

        const icon = this.getIcon(type);
        toast.innerHTML = `
      <div class="toast-content">
        <span class="toast-icon">${icon}</span>
        <span class="toast-message">${message}</span>
        <button class="toast-close" onclick="this.parentElement.parentElement.remove()">×</button>
      </div>
    `;

        this.container.appendChild(toast);

        // Auto remove después del tiempo especificado
        if (duration > 0) {
            setTimeout(() => {
                toast.classList.add('removing');
                setTimeout(() => toast.remove(), 300);
            }, duration);
        }

        return toast;
    }

    success(message, duration = 4000) {
        return this.show(message, 'success', duration);
    }

    error(message, duration = 5000) {
        return this.show(message, 'error', duration);
    }

    warning(message, duration = 4000) {
        return this.show(message, 'warning', duration);
    }

    info(message, duration = 4000) {
        return this.show(message, 'info', duration);
    }

    getIcon(type) {
        const icons = {
            success: '✓',
            error: '✕',
            warning: '⚠',
            info: 'ℹ'
        };
        return icons[type] || icons.info;
    }

    clear() {
        const toasts = this.container.querySelectorAll('.toast');
        toasts.forEach(toast => {
            toast.classList.add('removing');
            setTimeout(() => toast.remove(), 300);
        });
    }
}

// Crear instancia global
const Toast = new ToastNotification();

// Compatibilidad con AlertManager antiguo
const AlertManager = {
    success: (msg) => Toast.success(msg),
    error: (msg) => Toast.error(msg),
    warning: (msg) => Toast.warning(msg),
    info: (msg) => Toast.info(msg)
};

console.log('🔔 Toast Notification System initialized');