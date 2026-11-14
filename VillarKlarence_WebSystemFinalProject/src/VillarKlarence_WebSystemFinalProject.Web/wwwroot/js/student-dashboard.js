/*
 * student-dashboard.js — read-only view of the signed-in student's own grades.
 *   GET /api/auth/me          → name and student number (from the JWT identity)
 *   GET /api/student/grades   → own grades only; the API takes the StudentId from the token
 * There are no create/edit/delete controls for students anywhere.
 */
(function () {
    'use strict';

    if (!Api.requireRole('Student')) return;

    const $ = id => document.getElementById(id);
    const els = {
        initials: $('profileInitials'),
        name: $('profileName'),
        number: $('profileNumber'),
        email: $('profileEmail'),
        refresh: $('refreshButton'),
        print: $('printButton'),
        subjects: $('summarySubjects'),
        average: $('summaryAverage'),
        highest: $('summaryHighest'),
        tabs: $('semesterTabs'),
        rows: $('gradeRows'),
        cards: $('gradeCards'),
        panel: $('gradesPanel'),
        empty: $('emptyState'),
        emptyTitle: $('emptyTitle'),
        emptyText: $('emptyText'),
        alert: $('gradesAlert'),
        alertText: $('gradesAlertText'),
        retry: $('retryButton'),
        summary: $('resultSummary'),
        printDate: $('printDate')
    };

    const state = { grades: [], semester: '', loaded: false };

    // ------------------------------------------------------------------ profile

    async function loadProfile() {
        const result = await Api.get('/api/auth/me');
        if (!result.ok) {
            if (result.status === 401) return; // redirecting to login
            const cached = Api.session.user;
            if (cached) renderProfile(cached);
            if (result.status === 403) showError('Your account is not allowed to open the student dashboard.');
            return;
        }
        Api.session.updateUser(result.data);
        UI.bindUserChrome();
        renderProfile(result.data);
    }

    function renderProfile(profile) {
        els.initials.textContent = UI.initials(profile.fullName);
        els.name.textContent = profile.fullName || 'Student';
        els.number.textContent = profile.studentNumber || '—';
        els.email.textContent = profile.email || '—';
        document.title = (profile.fullName || 'My Grades') + ' • UST Angelicum College';
    }

    // ------------------------------------------------------------------ grades

    function showLoading() {
        hideError();
        els.empty.classList.add('d-none');
        els.panel.classList.remove('d-none');
        UI.skeletonRows(els.rows, 7, 4);
        els.cards.innerHTML = Array.from({ length: 3 }, () => '<div class="grade-card" aria-hidden="true"><span class="skeleton skeleton-block"></span></div>').join('');
        [els.subjects, els.average, els.highest].forEach(UI.skeletonValue);
        els.summary.textContent = 'Loading your grades…';
    }

    function showError(message) {
        els.alertText.textContent = message;
        els.alert.classList.remove('d-none');
    }

    function hideError() {
        els.alert.classList.add('d-none');
        els.alertText.textContent = '';
    }

    async function loadGrades() {
        showLoading();
        const result = await Api.get('/api/student/grades');

        if (!result.ok) {
            if (result.status === 401) return; // api.js redirects to the login page
            state.grades = [];
            els.rows.innerHTML = '';
            els.cards.innerHTML = '';
            els.rows.removeAttribute('aria-busy');
            [els.subjects, els.average, els.highest].forEach(el => { el.textContent = '—'; });
            els.summary.textContent = '';
            showError(result.status === 403
                ? 'You do not have permission to view these grades. Please sign in with your student account.'
                : result.message);
            return;
        }

        state.grades = Array.isArray(result.data) ? result.data : [];
        state.loaded = true;
        renderSummary();
        renderTabs();
        renderGrades();
    }

    function renderSummary() {
        const grades = state.grades;
        const subjects = new Set(grades.map(g => g.subjectCode)).size;
        UI.countUp(els.subjects, subjects);

        if (!grades.length) {
            els.average.textContent = '—';
            els.highest.textContent = '—';
            return;
        }

        const average = grades.reduce((sum, g) => sum + g.grade, 0) / grades.length;
        UI.countUp(els.average, average, { decimals: 2 });

        // The remark shown is the one the SERVER computed for the student's best grade.
        const best = grades.reduce((top, g) => (g.grade > top.grade ? g : top), grades[0]);
        els.highest.innerHTML = UI.remarkBadge(best.remarks, { pop: true });
    }

    function renderTabs() {
        els.tabs.querySelectorAll('[data-semester]').forEach(tab => {
            const semester = tab.dataset.semester;
            const count = semester ? state.grades.filter(g => g.semester === semester).length : state.grades.length;
            const label = semester || 'All';
            tab.innerHTML = UI.escapeHtml(label) + '<span class="tab-count">' + count + '</span>';
            const active = semester === state.semester;
            tab.classList.toggle('active', active);
            tab.setAttribute('aria-selected', active ? 'true' : 'false');
            tab.setAttribute('tabindex', active ? '0' : '-1');
        });
    }

    function visibleGrades() {
        return state.semester ? state.grades.filter(g => g.semester === state.semester) : state.grades;
    }

    function renderGrades() {
        const grades = visibleGrades();
        els.rows.removeAttribute('aria-busy');

        if (!grades.length) {
            els.rows.innerHTML = '';
            els.cards.innerHTML = '';
            els.panel.classList.add('d-none');
            els.empty.classList.remove('d-none');
            if (state.grades.length) {
                els.emptyTitle.textContent = 'No grades for ' + state.semester;
                els.emptyText.textContent = 'Choose another semester tab to see your other grades.';
            } else {
                els.emptyTitle.textContent = 'No grades posted yet';
                els.emptyText.textContent = 'Your grades will appear here as soon as your instructors post them.';
            }
            els.summary.textContent = state.grades.length ? 'Showing 0 of ' + state.grades.length + ' grades' : 'No grades yet';
            return;
        }

        els.panel.classList.remove('d-none');
        els.empty.classList.add('d-none');

        els.rows.innerHTML = grades.map(g =>
            '<tr>' +
            '<td><span class="cell-main">' + UI.escapeHtml(g.subjectCode) + '</span></td>' +
            '<td>' + UI.escapeHtml(g.subjectName) + '</td>' +
            '<td class="text-end"><span class="grade-number">' + UI.escapeHtml(g.grade) + '</span></td>' +
            '<td>' + UI.remarkBadge(g.remarks) + '</td>' +
            '<td class="text-nowrap">' + UI.escapeHtml(g.semester) + '</td>' +
            '<td class="text-nowrap">' + UI.escapeHtml(g.schoolYear) + '</td>' +
            '<td class="d-none d-lg-table-cell">' + UI.escapeHtml(g.facultyName) + '</td>' +
            '</tr>').join('');
        UI.stagger(els.rows, 'row-enter');

        els.cards.innerHTML = grades.map(g =>
            '<article class="grade-card' + (g.remarks === 'BEP/FAILED' ? ' is-failed' : '') + '" aria-label="' + UI.escapeHtml(g.subjectCode + ' grade') + '">' +
            '  <div class="gc-top">' +
            '    <div>' +
            '      <div class="gc-code">' + UI.escapeHtml(g.subjectCode) + '</div>' +
            '      <div class="gc-name">' + UI.escapeHtml(g.subjectName) + '</div>' +
            '      ' + UI.remarkBadge(g.remarks) +
            '    </div>' +
            '    <div class="gc-grade" aria-label="Grade ' + UI.escapeHtml(g.grade) + '">' + UI.escapeHtml(g.grade) + '</div>' +
            '  </div>' +
            '  <div class="gc-meta">' +
            '    <span><i class="bi bi-calendar3 me-1" aria-hidden="true"></i>' + UI.escapeHtml(g.semester) + '</span>' +
            '    <span><i class="bi bi-mortarboard me-1" aria-hidden="true"></i>' + UI.escapeHtml(g.schoolYear) + '</span>' +
            '    <span><i class="bi bi-person me-1" aria-hidden="true"></i>' + UI.escapeHtml(g.facultyName) + '</span>' +
            '  </div>' +
            '</article>').join('');
        UI.stagger(els.cards, 'card-enter');

        els.summary.textContent = 'Showing ' + grades.length + ' of ' + state.grades.length + ' grade' + (state.grades.length === 1 ? '' : 's');
    }

    // ------------------------------------------------------------------ events

    function selectTab(tab) {
        state.semester = tab.dataset.semester || '';
        renderTabs();
        renderGrades();
        tab.focus();
    }

    els.tabs.addEventListener('click', event => {
        const tab = event.target.closest('[data-semester]');
        if (tab) selectTab(tab);
    });

    // Arrow-key navigation between tabs (WAI-ARIA tabs pattern).
    els.tabs.addEventListener('keydown', event => {
        const tabs = Array.from(els.tabs.querySelectorAll('[data-semester]'));
        const index = tabs.indexOf(document.activeElement);
        if (index < 0) return;
        let next = null;
        if (event.key === 'ArrowRight') next = tabs[(index + 1) % tabs.length];
        if (event.key === 'ArrowLeft') next = tabs[(index - 1 + tabs.length) % tabs.length];
        if (event.key === 'Home') next = tabs[0];
        if (event.key === 'End') next = tabs[tabs.length - 1];
        if (next) {
            event.preventDefault();
            selectTab(next);
        }
    });

    els.refresh.addEventListener('click', async () => {
        UI.setButtonLoading(els.refresh, true, 'Refreshing…');
        await loadGrades();
        UI.setButtonLoading(els.refresh, false);
    });
    els.retry.addEventListener('click', loadGrades);

    els.print.addEventListener('click', () => {
        els.printDate.textContent = 'Printed ' + new Date().toLocaleString();
        window.print();
    });

    loadProfile();
    loadGrades();
})();
