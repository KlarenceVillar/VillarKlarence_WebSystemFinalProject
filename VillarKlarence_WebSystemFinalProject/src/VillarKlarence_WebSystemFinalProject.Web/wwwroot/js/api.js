/*
 * api.js — the ONE place the MVC pages talk to the Web API.
 *
 *  - Every request goes through Api.request(): fetch() + async/await + JSON.
 *  - Adds "Authorization: Bearer <token>" from sessionStorage.
 *  - NEVER throws: it always resolves to { ok, status, data, message, errors, problem },
 *    so a failed response (network error, 400, 401, 403, 404, 409, 500) cannot crash a page.
 *  - 401 on an authenticated call = session expired → clear session and go to the login page.
 *
 * The role checks in requireRole() are a UI convenience only. Real security is enforced by
 * the API ([Authorize(Roles = ...)] + ownership checks), which is why hiding buttons is not security.
 */
(function (window) {
    'use strict';

    const config = window.APP_CONFIG || {};
    const BASE_URL = String(config.apiBaseUrl || '').replace(/\/+$/, '');
    const URLS = Object.assign({
        login: '/Account/Login',
        accessDenied: '/Account/AccessDenied',
        faculty: '/Faculty',
        student: '/Student'
    }, config.urls || {});

    const KEYS = { token: 'ac.token', user: 'ac.user', expires: 'ac.expiresAt' };

    const FRIENDLY_MESSAGES = {
        0: 'Cannot reach the grade server. Please check your connection (and that the API is running), then try again.',
        400: 'Some of the information is not valid. Please review the highlighted fields.',
        401: 'Your session has expired. Please sign in again.',
        403: 'You do not have permission to do that.',
        404: 'The requested record was not found. It may have been deleted.',
        409: 'This record conflicts with an existing one.',
        500: 'The server ran into a problem. Please try again in a moment.'
    };

    // ------------------------------------------------------------------ storage (never throws)

    const store = {
        get(key) {
            try { return window.sessionStorage.getItem(key); } catch (e) { return null; }
        },
        set(key, value) {
            try { window.sessionStorage.setItem(key, value); } catch (e) { /* storage unavailable */ }
        },
        remove(key) {
            try { window.sessionStorage.removeItem(key); } catch (e) { /* storage unavailable */ }
        }
    };

    // ------------------------------------------------------------------ session

    function decodeJwtPayload(token) {
        try {
            const part = token.split('.')[1];
            const json = atob(part.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(part.length / 4) * 4, '='));
            return JSON.parse(decodeURIComponent(Array.from(json, c => '%' + c.charCodeAt(0).toString(16).padStart(2, '0')).join('')));
        } catch (e) {
            return null;
        }
    }

    const session = {
        /** Saves the POST /api/auth/login response. */
        save(loginResponse) {
            store.set(KEYS.token, loginResponse.token);
            store.set(KEYS.user, JSON.stringify(loginResponse.user || {}));
            store.set(KEYS.expires, loginResponse.expiresAtUtc || '');
        },
        clear() {
            store.remove(KEYS.token);
            store.remove(KEYS.user);
            store.remove(KEYS.expires);
        },
        get token() {
            return store.get(KEYS.token);
        },
        get user() {
            try { return JSON.parse(store.get(KEYS.user) || 'null'); } catch (e) { return null; }
        },
        get role() {
            const user = this.user;
            return user && user.role ? user.role : null;
        },
        /** True when there is no token or its "exp" claim is in the past. */
        isExpired() {
            const token = this.token;
            if (!token) return true;
            const payload = decodeJwtPayload(token);
            if (!payload || typeof payload.exp !== 'number') return false; // let the API decide
            return payload.exp * 1000 <= Date.now();
        },
        isSignedIn() {
            return !!this.token && !this.isExpired();
        },
        updateUser(user) {
            if (user) store.set(KEYS.user, JSON.stringify(user));
        }
    };

    // ------------------------------------------------------------------ helpers

    async function parseBody(response) {
        if (response.status === 204) return null;
        let text;
        try { text = await response.text(); } catch (e) { return null; }
        if (!text) return null;
        try { return JSON.parse(text); } catch (e) { return text; }
    }

    /** Turns ValidationProblemDetails.errors into { field: [messages] } with camelCase keys. */
    function normalizeErrors(errors) {
        if (!errors || typeof errors !== 'object') return null;
        const result = {};
        Object.keys(errors).forEach(key => {
            const clean = key.replace(/^\$\.?/, '') || 'body';
            const field = clean.charAt(0).toLowerCase() + clean.slice(1);
            const list = Array.isArray(errors[key]) ? errors[key] : [String(errors[key])];
            result[field] = (result[field] || []).concat(list);
        });
        return Object.keys(result).length ? result : null;
    }

    function goTo(url) {
        window.location.replace(url);
    }

    function redirectToLogin(reason) {
        const separator = URLS.login.indexOf('?') >= 0 ? '&' : '?';
        goTo(URLS.login + (reason ? separator + 'reason=' + encodeURIComponent(reason) : ''));
    }

    function dashboardFor(role) {
        if (role === 'Faculty') return URLS.faculty;
        if (role === 'Student') return URLS.student;
        return URLS.login;
    }

    // ------------------------------------------------------------------ the fetch wrapper

    /**
     * @param {string} method  GET | POST | PUT | DELETE
     * @param {string} path    e.g. "/api/grades"
     * @param {object} [options] { body, auth = true, redirectOn401 = true, signal }
     * @returns {Promise<{ok:boolean,status:number,data:any,message:string|null,errors:object|null,problem:object|null,headers:Headers|null}>}
     */
    async function request(method, path, options) {
        const opts = Object.assign({ auth: true, redirectOn401: true }, options || {});
        const headers = { 'Accept': 'application/json' };
        if (opts.body !== undefined) headers['Content-Type'] = 'application/json';

        if (opts.auth) {
            const token = session.token;
            if (token) headers['Authorization'] = 'Bearer ' + token;
        }

        let response;
        try {
            response = await fetch(BASE_URL + path, {
                method: method,
                headers: headers,
                body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
                signal: opts.signal,
                mode: 'cors',
                cache: 'no-store'
            });
        } catch (error) {
            if (error && error.name === 'AbortError') {
                return { ok: false, status: -1, aborted: true, data: null, message: null, errors: null, problem: null, headers: null };
            }
            // Network failure, API down, certificate not trusted, or CORS blocked.
            return { ok: false, status: 0, data: null, message: FRIENDLY_MESSAGES[0], errors: null, problem: null, headers: null };
        }

        const data = await parseBody(response);

        if (response.ok) {
            return { ok: true, status: response.status, data: data, message: null, errors: null, problem: null, headers: response.headers };
        }

        const status = response.status;
        const problem = data && typeof data === 'object' ? data : null;
        const errors = normalizeErrors(problem && problem.errors);

        let message;
        if (status >= 500) {
            message = FRIENDLY_MESSAGES[500];
        } else if (problem && typeof problem.detail === 'string' && problem.detail.trim()) {
            message = problem.detail;
        } else {
            message = FRIENDLY_MESSAGES[status] || ('The request failed (HTTP ' + status + ').');
        }

        if (status === 401 && opts.auth && opts.redirectOn401) {
            session.clear();
            redirectToLogin('expired');
        }

        return { ok: false, status: status, data: null, message: message, errors: errors, problem: problem, headers: response.headers };
    }

    // ------------------------------------------------------------------ page guards

    /**
     * Redirects away when the visitor is not signed in or has the wrong role.
     * UI convenience only: the API rejects the calls anyway (401/403).
     */
    function requireRole(role) {
        if (!session.token) {
            redirectToLogin('signin');
            return false;
        }
        if (session.isExpired()) {
            session.clear();
            redirectToLogin('expired');
            return false;
        }
        if (session.role !== role) {
            goTo(URLS.accessDenied);
            return false;
        }
        return true;
    }

    function logout() {
        session.clear();
        redirectToLogin('signedout');
    }

    window.Api = Object.freeze({
        baseUrl: BASE_URL,
        urls: URLS,
        session: session,
        friendlyMessages: FRIENDLY_MESSAGES,
        request: request,
        get: (path, options) => request('GET', path, options),
        post: (path, body, options) => request('POST', path, Object.assign({}, options, { body: body })),
        put: (path, body, options) => request('PUT', path, Object.assign({}, options, { body: body })),
        del: (path, options) => request('DELETE', path, options),
        /** POST /api/auth/login — anonymous; a 401 here means wrong credentials, not an expired session. */
        login: (email, password) => request('POST', '/api/auth/login', { body: { email: email, password: password }, auth: false, redirectOn401: false }),
        requireRole: requireRole,
        dashboardFor: dashboardFor,
        redirectToLogin: redirectToLogin,
        logout: logout
    });
})(window);
