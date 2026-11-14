/*
 * ui.js — shared UI helpers: theme toggle, toasts, remark badges, skeleton loaders,
 * count-up numbers, button spinners, inline validation messages and the splash screen.
 */
(function (window, document) {
    'use strict';

    const THEME_KEY = 'ac.theme';
    const reducedMotion = window.matchMedia ? window.matchMedia('(prefers-reduced-motion: reduce)') : { matches: false };

    // ------------------------------------------------------------------ text helpers

    function escapeHtml(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function initials(name) {
        return String(name || '?')
            .split(/\s+/)
            .filter(Boolean)
            .map(part => part.charAt(0))
            .filter((_, index, all) => index === 0 || index === all.length - 1)
            .join('')
            .toUpperCase() || '?';
    }

    function formatDate(iso) {
        if (!iso) return '—';
        const date = new Date(iso);
        if (Number.isNaN(date.getTime())) return '—';
        return date.toLocaleString(undefined, { year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
    }

    // ------------------------------------------------------------------ remark badges

    /** Maps the SERVER-computed remark to a pill style. (Display only — no grading logic here.) */
    const REMARK_CLASSES = {
        'CMS': 'remark-cms',
        'SP': 'remark-sp',
        'SAP': 'remark-sap',
        'GP': 'remark-gp',
        'EP': 'remark-ep',
        'BEP/FAILED': 'remark-failed'
    };

    function remarkBadge(remark, options) {
        const cls = REMARK_CLASSES[remark] || 'remark-unknown';
        const pop = options && options.pop ? ' badge-pop' : '';
        const icon = remark === 'BEP/FAILED' ? '<i class="bi bi-x-circle" aria-hidden="true"></i>' :
            remark === 'CMS' ? '<i class="bi bi-star-fill" aria-hidden="true"></i>' : '';
        return '<span class="remark-pill ' + cls + pop + '">' + icon + escapeHtml(remark || '—') + '</span>';
    }

    // ------------------------------------------------------------------ toasts

    const TOAST_ICONS = { success: 'bi-check-circle-fill', error: 'bi-x-octagon-fill', warning: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill' };

    /**
     * @param {string} message  plain text (escaped) unless options.html is true
     * @param {'success'|'error'|'warning'|'info'} [type]
     * @param {{title?:string, html?:boolean, delay?:number}} [options]
     */
    function toast(message, type, options) {
        const host = document.getElementById('toastHost');
        if (!host || !window.bootstrap) return;
        const kind = TOAST_ICONS[type] ? type : 'info';
        const opts = options || {};
        const titles = { success: 'Success', error: 'Something went wrong', warning: 'Heads up', info: 'Notice' };

        const el = document.createElement('div');
        el.className = 'toast app-toast toast-' + kind;
        el.setAttribute('role', kind === 'error' ? 'alert' : 'status');
        el.setAttribute('aria-live', kind === 'error' ? 'assertive' : 'polite');
        el.setAttribute('aria-atomic', 'true');
        el.innerHTML =
            '<div class="d-flex align-items-start gap-2 p-3">' +
            '  <i class="bi ' + TOAST_ICONS[kind] + ' toast-icon" aria-hidden="true"></i>' +
            '  <div class="flex-grow-1">' +
            '    <strong class="d-block mb-1">' + escapeHtml(opts.title || titles[kind]) + '</strong>' +
            '    <div class="small">' + (opts.html ? message : escapeHtml(message)) + '</div>' +
            '  </div>' +
            '  <button type="button" class="btn-close btn-sm" data-bs-dismiss="toast" aria-label="Close"></button>' +
            '</div>';
        host.appendChild(el);

        const instance = new window.bootstrap.Toast(el, { delay: opts.delay || (kind === 'error' ? 7000 : 4500), animation: !reducedMotion.matches });
        el.addEventListener('hidden.bs.toast', () => el.remove());
        instance.show();
    }

    // ------------------------------------------------------------------ loading states

    function setButtonLoading(button, loading, loadingText) {
        if (!button) return;
        if (loading) {
            if (!button.dataset.originalHtml) button.dataset.originalHtml = button.innerHTML;
            button.disabled = true;
            button.classList.add('is-loading');
            button.setAttribute('aria-busy', 'true');
            button.innerHTML = '<span class="spinner-border btn-spinner me-2" aria-hidden="true"></span>' + escapeHtml(loadingText || 'Please wait…');
        } else {
            button.disabled = false;
            button.classList.remove('is-loading');
            button.removeAttribute('aria-busy');
            if (button.dataset.originalHtml) {
                button.innerHTML = button.dataset.originalHtml;
                delete button.dataset.originalHtml;
            }
        }
    }

    /** Fills a <tbody> with shimmering placeholder rows while data loads. */
    function skeletonRows(tbody, columns, rows) {
        if (!tbody) return;
        const widths = ['w-80', 'w-60', 'w-35', 'w-60', 'w-80'];
        let html = '';
        for (let r = 0; r < (rows || 6); r++) {
            html += '<tr aria-hidden="true">';
            for (let c = 0; c < columns; c++) {
                const cls = columns > 7 && c >= columns - 4 && c < columns - 1 ? ' class="d-none d-lg-table-cell"' : '';
                html += '<td' + cls + '><span class="skeleton skeleton-text ' + widths[(r + c) % widths.length] + '"></span></td>';
            }
            html += '</tr>';
        }
        tbody.innerHTML = html;
        tbody.setAttribute('aria-busy', 'true');
    }

    function skeletonValue(element) {
        if (element) element.innerHTML = '<span class="skeleton skeleton-text w-60" style="height:1.5rem"></span>';
    }

    /** Animates a number from 0 to its value (instant when reduced motion is requested). */
    function countUp(element, value, options) {
        if (!element) return;
        const opts = Object.assign({ decimals: 0, suffix: '', duration: 900 }, options || {});
        const target = Number(value) || 0;
        const render = v => { element.textContent = v.toFixed(opts.decimals) + opts.suffix; };

        if (reducedMotion.matches || target === 0) {
            render(target);
            return;
        }

        const start = performance.now();
        const step = now => {
            const progress = Math.min(1, (now - start) / opts.duration);
            const eased = 1 - Math.pow(1 - progress, 3);
            render(target * eased);
            if (progress < 1) {
                window.requestAnimationFrame(step);
            } else {
                render(target);
                element.classList.remove('count-done');
                void element.offsetWidth; // restart the glow animation
                element.classList.add('count-done');
            }
        };
        window.requestAnimationFrame(step);
    }

    /** Gives each child an index so CSS can stagger the entrance animation. */
    function stagger(container, className) {
        if (!container) return;
        Array.from(container.children).forEach((child, index) => {
            child.classList.add(className || 'row-enter');
            child.style.setProperty('--i', String(Math.min(index, 20)));
        });
    }

    // ------------------------------------------------------------------ inline validation messages

    function clearFieldErrors(form) {
        if (!form) return;
        form.querySelectorAll('.is-invalid').forEach(el => {
            el.classList.remove('is-invalid');
            el.removeAttribute('aria-invalid');
        });
        form.querySelectorAll('.invalid-feedback').forEach(el => { el.textContent = ''; });
    }

    /**
     * Shows server-side validation messages next to the matching fields (by name attribute).
     * Returns the messages that had no matching field so the caller can show them in an alert.
     */
    function showFieldErrors(form, errors) {
        const unmatched = [];
        if (!form || !errors) return unmatched;
        let first = null;
        Object.keys(errors).forEach(field => {
            const input = form.querySelector('[name="' + field + '"]');
            const messages = errors[field] || [];
            if (input) {
                input.classList.add('is-invalid');
                input.setAttribute('aria-invalid', 'true');
                const feedback = form.querySelector('#' + field + 'Feedback');
                if (feedback) feedback.textContent = messages.join(' ');
                if (!first) first = input;
            } else {
                messages.forEach(m => unmatched.push(m));
            }
        });
        if (first) first.focus();
        return unmatched;
    }

    function showAlert(element, message, type, extraItems) {
        if (!element) return;
        element.className = 'alert alert-' + (type || 'danger');
        let html = '<i class="bi bi-exclamation-circle me-2" aria-hidden="true"></i>' + escapeHtml(message);
        if (extraItems && extraItems.length) {
            html += '<ul>' + extraItems.map(item => '<li>' + escapeHtml(item) + '</li>').join('') + '</ul>';
        }
        element.innerHTML = html;
    }

    function hideAlert(element) {
        if (!element) return;
        element.className = 'alert d-none';
        element.innerHTML = '';
    }

    // ------------------------------------------------------------------ theme

    function currentTheme() {
        return document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'dark' : 'light';
    }

    function applyTheme(theme) {
        document.documentElement.setAttribute('data-bs-theme', theme);
        try { window.localStorage.setItem(THEME_KEY, theme); } catch (e) { /* storage unavailable */ }
        document.querySelectorAll('[data-theme-toggle]').forEach(button => {
            const dark = theme === 'dark';
            button.setAttribute('aria-label', dark ? 'Switch to light mode' : 'Switch to dark mode');
            button.setAttribute('aria-pressed', dark ? 'true' : 'false');
            const icon = button.querySelector('.bi');
            if (icon) icon.className = 'bi ' + (dark ? 'bi-sun' : 'bi-moon-stars');
        });
    }

    // ------------------------------------------------------------------ page chrome

    function hideSplash() {
        const splash = document.getElementById('splash');
        if (!splash || splash.classList.contains('is-hidden')) return;
        splash.classList.add('is-hidden');
        splash.setAttribute('aria-hidden', 'true');
        window.setTimeout(() => splash.remove(), reducedMotion.matches ? 0 : 500);
    }

    /** Fills the navbar user chip and wires the logout / home links from the stored session. */
    function bindUserChrome() {
        const api = window.Api;
        const user = api ? api.session.user : null;
        const signedIn = api ? api.session.isSignedIn() : false;

        document.querySelectorAll('[data-user-chip]').forEach(chip => { chip.hidden = !(signedIn && user); });
        document.querySelectorAll('[data-logout]').forEach(button => {
            button.hidden = !signedIn;
            button.addEventListener('click', () => api && api.logout());
        });
        document.querySelectorAll('[data-logout-link]').forEach(link => {
            link.addEventListener('click', () => api && api.session.clear());
        });
        document.querySelectorAll('[data-home-link]').forEach(link => {
            link.setAttribute('href', api && signedIn ? api.dashboardFor(api.session.role) : (api ? api.urls.login : '/'));
        });

        if (user && signedIn) {
            const detail = user.role === 'Student' ? (user.studentNumber || 'Student') : (user.facultyNumber ? 'Faculty • ' + user.facultyNumber : 'Faculty');
            document.querySelectorAll('[data-user-name]').forEach(el => { el.textContent = user.fullName || user.email || ''; });
            document.querySelectorAll('[data-user-role]').forEach(el => { el.textContent = detail; });
            document.querySelectorAll('[data-user-initials]').forEach(el => { el.textContent = initials(user.fullName); });
        }
    }

    function init() {
        applyTheme(currentTheme());
        document.querySelectorAll('[data-theme-toggle]').forEach(button => {
            button.addEventListener('click', () => applyTheme(currentTheme() === 'dark' ? 'light' : 'dark'));
        });
        bindUserChrome();
        // Hide the splash once everything (fonts, CDN CSS) has loaded; a fallback timer makes sure it never sticks.
        if (document.readyState === 'complete') {
            window.setTimeout(hideSplash, 250);
        } else {
            window.addEventListener('load', () => window.setTimeout(hideSplash, 250));
        }
        window.setTimeout(hideSplash, 2500);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    window.UI = Object.freeze({
        escapeHtml: escapeHtml,
        initials: initials,
        formatDate: formatDate,
        remarkBadge: remarkBadge,
        toast: toast,
        setButtonLoading: setButtonLoading,
        skeletonRows: skeletonRows,
        skeletonValue: skeletonValue,
        countUp: countUp,
        stagger: stagger,
        clearFieldErrors: clearFieldErrors,
        showFieldErrors: showFieldErrors,
        showAlert: showAlert,
        hideAlert: hideAlert,
        hideSplash: hideSplash,
        bindUserChrome: bindUserChrome,
        prefersReducedMotion: () => reducedMotion.matches
    });
})(window, document);
