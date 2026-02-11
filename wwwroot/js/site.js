/**
 * ByteBill - Main Application JavaScript
 * Handles navigation, toasts, modals, and common interactions
 */

// ===========================================
// Navigation Module
// ===========================================
const Navigation = {
    init() {
        this.sidebar = document.querySelector('.side-nav');
        this.sidebarToggle = document.getElementById('nav-toggle') || document.querySelector('[data-toggle="sidebar"]');
        this.navLinks = document.querySelectorAll('.nav-item');
        
        this.bindEvents();
        this.setActiveLink();
    },

    bindEvents() {
        // Mobile sidebar toggle
        if (this.sidebarToggle) {
            this.sidebarToggle.addEventListener('click', () => this.toggleSidebar());
        }

        // Close sidebar on overlay click (mobile)
        document.addEventListener('click', (e) => {
            if (this.sidebar?.classList.contains('open') && 
                !this.sidebar.contains(e.target) && 
                !this.sidebarToggle?.contains(e.target)) {
                this.closeSidebar();
            }
        });

        // Keyboard navigation
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && this.sidebar?.classList.contains('open')) {
                this.closeSidebar();
            }
        });
    },

    toggleSidebar() {
        this.sidebar?.classList.toggle('open');
        document.body.classList.toggle('sidebar-open');
    },

    closeSidebar() {
        this.sidebar?.classList.remove('open');
        document.body.classList.remove('sidebar-open');
    },

    setActiveLink() {
        const currentPath = window.location.pathname;
        this.navLinks.forEach(link => {
            const href = link.getAttribute('href');
            if (href && currentPath.startsWith(href) && href !== '/') {
                link.classList.add('active');
                // Expand parent section if collapsed
                const section = link.closest('.nav-section');
                if (section) {
                    section.classList.add('expanded');
                }
            }
        });
    }
};

// ===========================================
// Toast Notifications Module
// ===========================================
const Toast = {
    container: null,
    defaultDuration: 5000,

    init() {
        this.createContainer();
        this.showServerMessages();
    },

    createContainer() {
        this.container = document.createElement('div');
        this.container.className = 'toast-container';
        this.container.setAttribute('aria-live', 'polite');
        this.container.setAttribute('aria-atomic', 'true');
        document.body.appendChild(this.container);
    },

    show(message, type = 'info', duration = this.defaultDuration) {
        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;
        toast.setAttribute('role', 'alert');
        
        const icon = this.getIcon(type);
        toast.innerHTML = `
            <div class="toast-icon">${icon}</div>
            <div class="toast-content">
                <p class="toast-message">${this.escapeHtml(message)}</p>
            </div>
            <button type="button" class="toast-close" aria-label="Dismiss">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
            </button>
        `;

        // Add close handler
        toast.querySelector('.toast-close').addEventListener('click', () => this.dismiss(toast));
        
        this.container.appendChild(toast);
        
        // Trigger entrance animation
        requestAnimationFrame(() => toast.classList.add('show'));

        // Auto dismiss
        if (duration > 0) {
            setTimeout(() => this.dismiss(toast), duration);
        }

        return toast;
    },

    dismiss(toast) {
        toast.classList.remove('show');
        toast.classList.add('hide');
        setTimeout(() => toast.remove(), 300);
    },

    success(message, duration) {
        return this.show(message, 'success', duration);
    },

    error(message, duration) {
        return this.show(message, 'error', duration);
    },

    warning(message, duration) {
        return this.show(message, 'warning', duration);
    },

    info(message, duration) {
        return this.show(message, 'info', duration);
    },

    getIcon(type) {
        const icons = {
            success: '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>',
            error: '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>',
            warning: '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>',
            info: '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>'
        };
        return icons[type] || icons.info;
    },

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    },

    showServerMessages() {
        // Check for TempData messages from server
        const successMsg = document.querySelector('[data-toast-success]');
        const errorMsg = document.querySelector('[data-toast-error]');
        
        if (successMsg) {
            this.success(successMsg.dataset.toastSuccess);
            successMsg.remove();
        }
        if (errorMsg) {
            this.error(errorMsg.dataset.toastError);
            errorMsg.remove();
        }
    }
};

// ===========================================
// Modal Module
// ===========================================
const Modal = {
    activeModal: null,

    open(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        this.activeModal = modal;
        modal.classList.add('open');
        document.body.classList.add('modal-open');
        
        // Focus first focusable element
        const focusable = modal.querySelector('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
        if (focusable) focusable.focus();

        // Trap focus
        modal.addEventListener('keydown', this.trapFocus);
    },

    close(modalId) {
        const modal = modalId ? document.getElementById(modalId) : this.activeModal;
        if (!modal) return;

        modal.classList.remove('open');
        document.body.classList.remove('modal-open');
        modal.removeEventListener('keydown', this.trapFocus);
        this.activeModal = null;
    },

    trapFocus(e) {
        if (e.key !== 'Tab') return;
        
        const focusable = e.currentTarget.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
        const first = focusable[0];
        const last = focusable[focusable.length - 1];

        if (e.shiftKey && document.activeElement === first) {
            e.preventDefault();
            last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
            e.preventDefault();
            first.focus();
        }
    },

    confirm(options) {
        return new Promise((resolve) => {
            const modal = document.createElement('div');
            modal.className = 'modal open';
            modal.innerHTML = `
                <div class="modal-backdrop"></div>
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h3 class="modal-title">${options.title || 'Confirm'}</h3>
                        </div>
                        <div class="modal-body">
                            <p>${options.message || 'Are you sure?'}</p>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-ghost modal-cancel">${options.cancelText || 'Cancel'}</button>
                            <button type="button" class="btn ${options.danger ? 'btn-danger' : 'btn-primary'} modal-confirm">${options.confirmText || 'Confirm'}</button>
                        </div>
                    </div>
                </div>
            `;

            document.body.appendChild(modal);
            document.body.classList.add('modal-open');

            modal.querySelector('.modal-cancel').addEventListener('click', () => {
                modal.remove();
                document.body.classList.remove('modal-open');
                resolve(false);
            });

            modal.querySelector('.modal-confirm').addEventListener('click', () => {
                modal.remove();
                document.body.classList.remove('modal-open');
                resolve(true);
            });

            modal.querySelector('.modal-backdrop').addEventListener('click', () => {
                modal.remove();
                document.body.classList.remove('modal-open');
                resolve(false);
            });
        });
    }
};

// ===========================================
// Forms Module
// ===========================================
const Forms = {
    init() {
        this.initFloatingLabels();
        this.initDeleteConfirmation();
        this.initFormValidation();
    },

    initFloatingLabels() {
        document.querySelectorAll('.form-floating input, .form-floating textarea').forEach(input => {
            const updateState = () => {
                input.classList.toggle('has-value', input.value.length > 0);
            };
            input.addEventListener('input', updateState);
            input.addEventListener('change', updateState);
            updateState();
        });
    },

    initDeleteConfirmation() {
        document.querySelectorAll('[data-confirm-delete]').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                e.preventDefault();
                const confirmed = await Modal.confirm({
                    title: 'Delete Confirmation',
                    message: btn.dataset.confirmDelete || 'Are you sure you want to delete this item? This action cannot be undone.',
                    confirmText: 'Delete',
                    danger: true
                });
                if (confirmed) {
                    const form = btn.closest('form');
                    if (form) form.submit();
                    else if (btn.href) window.location.href = btn.href;
                }
            });
        });
    },

    initFormValidation() {
        document.querySelectorAll('form[data-validate]').forEach(form => {
            form.addEventListener('submit', (e) => {
                if (!form.checkValidity()) {
                    e.preventDefault();
                    e.stopPropagation();
                }
                form.classList.add('was-validated');
            });
        });
    }
};

// ===========================================
// Utilities
// ===========================================
const Utils = {
    formatCurrency(amount, currency = 'USD') {
        return new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: currency
        }).format(amount);
    },

    formatDate(date, options = {}) {
        return new Intl.DateTimeFormat('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
            ...options
        }).format(new Date(date));
    },

    debounce(func, wait) {
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

    copyToClipboard(text) {
        navigator.clipboard.writeText(text).then(() => {
            Toast.success('Copied to clipboard');
        }).catch(() => {
            Toast.error('Failed to copy');
        });
    }
};

// ===========================================
// Initialize on DOM Ready
// ===========================================
document.addEventListener('DOMContentLoaded', () => {
    Navigation.init();
    Toast.init();
    Forms.init();
    UserDropdown.init();

    // Global escape key handler for modals
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && Modal.activeModal) {
            Modal.close();
        }
    });

    // Handle modal triggers
    document.querySelectorAll('[data-modal-open]').forEach(trigger => {
        trigger.addEventListener('click', () => Modal.open(trigger.dataset.modalOpen));
    });

    document.querySelectorAll('[data-modal-close]').forEach(trigger => {
        trigger.addEventListener('click', () => Modal.close(trigger.dataset.modalClose));
    });
});

// ===========================================
// User Dropdown Module
// ===========================================
const UserDropdown = {
    init() {
        this.toggle = document.getElementById('user-menu-toggle');
        this.dropdown = document.getElementById('user-dropdown');
        
        if (this.toggle && this.dropdown) {
            this.bindEvents();
        }
    },

    bindEvents() {
        // Toggle dropdown on click
        this.toggle.addEventListener('click', (e) => {
            e.stopPropagation();
            this.toggleDropdown();
        });

        // Close dropdown when clicking outside
        document.addEventListener('click', (e) => {
            if (!this.dropdown.classList.contains('hidden') && 
                !this.dropdown.contains(e.target) && 
                !this.toggle.contains(e.target)) {
                this.closeDropdown();
            }
        });

        // Close dropdown on Escape key
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && !this.dropdown.classList.contains('hidden')) {
                this.closeDropdown();
            }
        });
    },

    toggleDropdown() {
        this.dropdown.classList.toggle('hidden');
    },

    openDropdown() {
        this.dropdown.classList.remove('hidden');
    },

    closeDropdown() {
        this.dropdown.classList.add('hidden');
    }
};

// Export modules for use in other scripts
window.ByteBill = { Navigation, Toast, Modal, Forms, Utils, UserDropdown };

// ===========================================
// AJAX Modal Module
// ===========================================
function openAjaxModal(url, title) {
    const backdrop = document.getElementById('ajax-modal-backdrop');
    const modal = document.getElementById('ajax-modal');
    const titleEl = document.getElementById('ajax-modal-title');
    const contentEl = document.getElementById('ajax-modal-content');

    if (!backdrop || !modal) return;

    // Set title and show loading
    titleEl.textContent = title || 'Loading...';
    contentEl.innerHTML = '<div class="ajax-modal-loading"><div class="spinner"></div><p>Loading...</p></div>';

    // Open modal
    backdrop.classList.add('open');
    modal.classList.add('open');
    document.body.classList.add('modal-open');

    // Fetch content
    fetch(url, {
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    })
    .then(response => {
        if (!response.ok) throw new Error('Failed to load content');
        return response.text();
    })
    .then(html => {
        contentEl.innerHTML = html;
        // Bind AJAX form submission
        bindAjaxForms(contentEl);
        // Focus first focusable element
        const focusable = contentEl.querySelector('input:not([type="hidden"]), select, textarea, button');
        if (focusable) focusable.focus();
    })
    .catch(error => {
        contentEl.innerHTML = '<div class="ajax-modal-loading"><p style="color: #ef4444;">Failed to load content. Please try again.</p></div>';
        console.error('Modal load error:', error);
    });
}

function closeAjaxModal() {
    const backdrop = document.getElementById('ajax-modal-backdrop');
    const modal = document.getElementById('ajax-modal');

    if (!backdrop || !modal) return;

    backdrop.classList.remove('open');
    modal.classList.remove('open');
    document.body.classList.remove('modal-open');
}

function bindAjaxForms(container) {
    const forms = container.querySelectorAll('form[data-ajax="true"]');
    forms.forEach(form => {
        form.addEventListener('submit', function(e) {
            e.preventDefault();

            const formData = new FormData(form);
            const url = form.action;
            const submitBtn = form.querySelector('button[type="submit"]');
            const originalText = submitBtn ? submitBtn.innerHTML : '';

            // Show loading state
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<div class="spinner" style="width:16px;height:16px;border-width:2px;display:inline-block;vertical-align:middle;margin-right:8px;"></div> Saving...';
            }

            fetch(url, {
                method: 'POST',
                body: formData,
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
            .then(response => {
                const contentType = response.headers.get('content-type') || '';
                if (contentType.includes('application/json')) {
                    return response.json().then(data => ({ type: 'json', data }));
                }
                return response.text().then(html => ({ type: 'html', data: html }));
            })
            .then(result => {
                if (result.type === 'json' && result.data.success) {
                    // Success - close modal and reload
                    closeAjaxModal();
                    if (result.data.message) {
                        Toast.success(result.data.message);
                    }
                    // Reload page to reflect changes
                    setTimeout(() => window.location.reload(), 500);
                } else if (result.type === 'html') {
                    // Validation errors - replace form content
                    const contentEl = document.getElementById('ajax-modal-content');
                    contentEl.innerHTML = result.data;
                    bindAjaxForms(contentEl);
                }
            })
            .catch(error => {
                console.error('Form submit error:', error);
                Toast.error('An error occurred. Please try again.');
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalText;
                }
            });
        });
    });
}

// Global escape key handler for AJAX modal
document.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') {
        const ajaxModal = document.getElementById('ajax-modal');
        if (ajaxModal && ajaxModal.classList.contains('open')) {
            closeAjaxModal();
        }
    }
});

// Make functions globally available
window.openAjaxModal = openAjaxModal;
window.closeAjaxModal = closeAjaxModal;
