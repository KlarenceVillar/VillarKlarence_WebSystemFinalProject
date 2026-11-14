/*
 * login.js — signs in with fetch() → POST /api/auth/login (ASP.NET Core Identity checks the password,
 * the API returns a JWT). The token and role are stored in sessionStorage and the user is sent to the
 * dashboard for their role.
 */
(function () {
    'use strict';

    const form = document.getElementById('loginForm');
    const emailInput = document.getElementById('email');
    const passwordInput = document.getElementById('password');
    const button = document.getElementById('loginButton');
    const alertBox = document.getElementById('loginAlert');
    const toggle = document.getElementById('togglePassword');

    const REASONS = {
        expired: { type: 'warning', text: 'Your session has expired. Please sign in again.' },
        signin: { type: 'info', text: 'Please sign in to continue.' },
        signedout: { type: 'info', text: 'You have been signed out.' }
    };

    // Already signed in with a valid token? Go straight to the dashboard.
    if (Api.session.isSignedIn() && Api.session.role) {
        window.location.replace(Api.dashboardFor(Api.session.role));
        return;
    }
    Api.session.clear();

    const reason = new URLSearchParams(window.location.search).get('reason');
    if (reason && REASONS[reason]) {
        UI.showAlert(alertBox, REASONS[reason].text, REASONS[reason].type);
    }

    toggle.addEventListener('click', () => {
        const show = passwordInput.type === 'password';
        passwordInput.type = show ? 'text' : 'password';
        toggle.setAttribute('aria-pressed', String(show));
        toggle.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
        toggle.querySelector('.bi').className = 'bi ' + (show ? 'bi-eye-slash' : 'bi-eye');
    });

    document.querySelectorAll('[data-demo-email]').forEach(demo => {
        demo.addEventListener('click', () => {
            emailInput.value = demo.getAttribute('data-demo-email');
            UI.clearFieldErrors(form);
            passwordInput.focus();
        });
    });

    function validateLocally() {
        const errors = {};
        const email = emailInput.value.trim();
        if (!email) errors.email = ['Email is required.'];
        else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) errors.email = ['Enter a valid email address.'];
        if (!passwordInput.value) errors.password = ['Password is required.'];
        return Object.keys(errors).length ? errors : null;
    }

    form.addEventListener('submit', async event => {
        event.preventDefault();
        UI.clearFieldErrors(form);
        UI.hideAlert(alertBox);

        const localErrors = validateLocally();
        if (localErrors) {
            UI.showFieldErrors(form, localErrors);
            return;
        }

        UI.setButtonLoading(button, true, 'Signing in…');
        const result = await Api.login(emailInput.value.trim(), passwordInput.value);

        if (result.ok && result.data && result.data.token) {
            Api.session.save(result.data);
            const role = result.data.user && result.data.user.role;
            UI.showAlert(alertBox, 'Welcome, ' + ((result.data.user && result.data.user.fullName) || 'back') + '! Opening your dashboard…', 'success');
            window.setTimeout(() => window.location.replace(Api.dashboardFor(role)), UI.prefersReducedMotion() ? 0 : 450);
            return;
        }

        UI.setButtonLoading(button, false);
        passwordInput.value = '';

        if (result.status === 400 && result.errors) {
            const unmatched = UI.showFieldErrors(form, result.errors);
            if (unmatched.length) UI.showAlert(alertBox, result.message, 'danger', unmatched);
            return;
        }

        if (result.status === 401) {
            UI.showAlert(alertBox, result.message || 'Invalid email or password.', 'danger');
            passwordInput.focus();
            return;
        }

        // 0 (network / API down / certificate), 403 (account not linked), 500...
        UI.showAlert(alertBox, result.message, 'danger');
    });
})();
