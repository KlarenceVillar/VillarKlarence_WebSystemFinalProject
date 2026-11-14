/*
 * faculty-dashboard.js — Faculty grade management.
 *   GET    /api/grades (+ ?search=&semester=&schoolYear=&subjectCode= filters)
 *   GET    /api/students, GET /api/subjects  (form dropdown + autocomplete)
 *   POST   /api/grades        PUT /api/grades/{id}        DELETE /api/grades/{id}
 * Remarks are NEVER computed here: the page shows whatever the server returns.
 */
(function () {
    'use strict';

    if (!Api.requireRole('Faculty')) return;

    // ------------------------------------------------------------------ elements

    const $ = id => document.getElementById(id);
    const els = {
        rows: $('gradeRows'),
        empty: $('emptyState'),
        emptyTitle: $('emptyTitle'),
        emptyText: $('emptyText'),
        tableAlert: $('tableAlert'),
        summary: $('resultSummary'),
        pagination: $('pagination'),
        pageSize: $('pageSize'),
        search: $('searchInput'),
        semesterFilter: $('semesterFilter'),
        schoolYearFilter: $('schoolYearFilter'),
        subjectFilter: $('subjectFilter'),
        clearFilters: $('clearFilters'),
        refresh: $('refreshButton'),
        add: $('addGradeButton'),
        statTotal: $('statTotal'),
        statStudents: $('statStudents'),
        statSubjects: $('statSubjects'),
        statPassRate: $('statPassRate'),
        facultyNumber: document.querySelector('[data-faculty-number]'),
        // form modal
        modalEl: $('gradeModal'),
        form: $('gradeForm'),
        formAlert: $('formAlert'),
        modalTitle: $('gradeModalTitle'),
        modalEyebrow: $('gradeModalEyebrow'),
        auditInfo: $('auditInfo'),
        save: $('saveGradeButton'),
        studentId: $('studentId'),
        subjectCode: $('subjectCode'),
        subjectName: $('subjectName'),
        grade: $('grade'),
        semester: $('semester'),
        schoolYear: $('schoolYear'),
        subjectList: $('subjectSuggestions'),
        // delete modal
        deleteModalEl: $('deleteModal'),
        deleteSummary: $('deleteSummary'),
        confirmDelete: $('confirmDeleteButton')
    };

    const gradeModal = new bootstrap.Modal(els.modalEl);
    const deleteModal = new bootstrap.Modal(els.deleteModalEl);

    // ------------------------------------------------------------------ state

    const state = {
        all: [],          // every record (stats + filter options)
        view: [],         // records matching the current filters (from the API)
        students: [],
        subjects: [],
        sort: { key: 'studentNumber', dir: 'asc' },
        page: 1,
        pageSize: 10,
        editing: null,    // grade being edited, or null when adding
        deleting: null,
        highlightId: null,
        filterRequest: null
    };

    const user = Api.session.user || {};
    if (els.facultyNumber && user.facultyNumber) els.facultyNumber.textContent = '• ' + user.facultyNumber;

    // ------------------------------------------------------------------ helpers

    function defaultSchoolYear() {
        const now = new Date();
        const start = now.getMonth() >= 5 ? now.getFullYear() : now.getFullYear() - 1; // school year starts around June
        return start + '-' + (start + 1);
    }

    function currentFilters() {
        return {
            search: els.search.value.trim(),
            semester: els.semesterFilter.value,
            schoolYear: els.schoolYearFilter.value,
            subjectCode: els.subjectFilter.value
        };
    }

    function hasFilters(f) {
        return !!(f.search || f.semester || f.schoolYear || f.subjectCode);
    }

    function queryString(f) {
        const params = new URLSearchParams();
        Object.keys(f).forEach(key => { if (f[key]) params.set(key, f[key]); });
        const text = params.toString();
        return text ? '?' + text : '';
    }

    function compare(a, b, key) {
        const x = a[key];
        const y = b[key];
        if (x === y) return 0;
        if (x === null || x === undefined || x === '') return 1;
        if (y === null || y === undefined || y === '') return -1;
        if (typeof x === 'number' && typeof y === 'number') return x - y;
        return String(x).localeCompare(String(y), undefined, { numeric: true, sensitivity: 'base' });
    }

    function sortedView() {
        const { key, dir } = state.sort;
        const factor = dir === 'asc' ? 1 : -1;
        return state.view.slice().sort((a, b) => compare(a, b, key) * factor || compare(a, b, 'gradeId'));
    }

    function handleListError(result, what) {
        if (result.status === 401) return; // api.js is already redirecting to the login page
        if (result.status === 403) {
            UI.showAlert(els.tableAlert, 'Your account is not allowed to view ' + what + '. Please sign in with a faculty account.', 'danger');
            return;
        }
        UI.showAlert(els.tableAlert, result.message || ('Could not load ' + what + '.'), 'danger');
    }

    // ------------------------------------------------------------------ loading

    async function loadAll(options) {
        const opts = options || {};
        UI.hideAlert(els.tableAlert);
        if (!opts.quiet) {
            UI.skeletonRows(els.rows, 10, 6);
            els.empty.classList.add('d-none');
            [els.statTotal, els.statStudents, els.statSubjects, els.statPassRate].forEach(UI.skeletonValue);
        }

        const [grades, students, subjects] = await Promise.all([
            Api.get('/api/grades'),
            Api.get('/api/students'),
            Api.get('/api/subjects')
        ]);

        if (!grades.ok) {
            handleListError(grades, 'grade records');
            state.all = [];
            state.view = [];
            renderStats();
            renderTable();
            return;
        }

        state.all = Array.isArray(grades.data) ? grades.data : [];
        state.students = students.ok && Array.isArray(students.data) ? students.data : state.students;
        state.subjects = subjects.ok && Array.isArray(subjects.data) ? subjects.data : state.subjects;
        if (!students.ok && students.status !== 401) UI.toast(students.message || 'Could not load the student list.', 'warning');
        if (!subjects.ok && subjects.status !== 401) UI.toast(subjects.message || 'Could not load subject suggestions.', 'warning');

        renderStats();
        renderFilterOptions();
        renderStudentOptions();
        renderSubjectSuggestions();
        await applyFilters({ keepPage: opts.keepPage });
    }

    /** Uses the API's filter parameters when any filter is set; otherwise reuses the full list. */
    async function applyFilters(options) {
        const opts = options || {};
        const filters = currentFilters();
        if (!opts.keepPage) state.page = 1;

        if (!hasFilters(filters)) {
            state.view = state.all.slice();
            renderTable();
            return;
        }

        if (state.filterRequest) state.filterRequest.abort();
        const controller = new AbortController();
        state.filterRequest = controller;

        els.summary.textContent = 'Filtering…';
        const result = await Api.get('/api/grades' + queryString(filters), { signal: controller.signal });
        if (result.aborted) return;
        state.filterRequest = null;

        if (!result.ok) {
            handleListError(result, 'grade records');
            state.view = [];
        } else {
            UI.hideAlert(els.tableAlert);
            state.view = Array.isArray(result.data) ? result.data : [];
        }
        renderTable();
    }

    // ------------------------------------------------------------------ rendering

    function renderStats() {
        const total = state.all.length;
        const students = new Set(state.all.map(g => g.studentId)).size;
        const subjects = new Set(state.all.map(g => g.subjectCode)).size;
        const passed = state.all.filter(g => g.remarks !== 'BEP/FAILED').length; // remark comes from the server
        const passRate = total ? (passed / total) * 100 : 0;

        UI.countUp(els.statTotal, total);
        UI.countUp(els.statStudents, students);
        UI.countUp(els.statSubjects, subjects);
        UI.countUp(els.statPassRate, passRate, { decimals: total ? 1 : 0, suffix: '%' });
    }

    function fillSelect(select, values, labelFn) {
        const current = select.value;
        select.innerHTML = '<option value="">All</option>' + values
            .map(v => '<option value="' + UI.escapeHtml(v) + '">' + UI.escapeHtml(labelFn ? labelFn(v) : v) + '</option>')
            .join('');
        if (values.indexOf(current) >= 0) select.value = current;
    }

    function renderFilterOptions() {
        const years = Array.from(new Set(state.all.map(g => g.schoolYear))).sort().reverse();
        fillSelect(els.schoolYearFilter, years);

        const subjectNames = {};
        state.all.forEach(g => { subjectNames[g.subjectCode] = g.subjectName; });
        const codes = Object.keys(subjectNames).sort();
        fillSelect(els.subjectFilter, codes, code => code + ' — ' + subjectNames[code]);
    }

    function renderStudentOptions() {
        const selected = els.studentId.value;
        if (!state.students.length) {
            els.studentId.innerHTML = '<option value="">No students available</option>';
            return;
        }
        els.studentId.innerHTML = '<option value="">Select a student…</option>' + state.students
            .map(s => '<option value="' + s.studentId + '">' + UI.escapeHtml(s.studentNumber + ' — ' + s.fullName) + '</option>')
            .join('');
        if (selected) els.studentId.value = selected;
    }

    function renderSubjectSuggestions() {
        els.subjectList.innerHTML = state.subjects
            .map(s => '<option value="' + UI.escapeHtml(s.subjectCode) + '">' + UI.escapeHtml(s.subjectName) + '</option>')
            .join('');
    }

    function renderSortIndicators() {
        document.querySelectorAll('.grade-table th[aria-sort]').forEach(th => {
            const button = th.querySelector('.sort-btn');
            const icon = button.querySelector('.bi');
            const active = button.dataset.sort === state.sort.key;
            th.setAttribute('aria-sort', active ? (state.sort.dir === 'asc' ? 'ascending' : 'descending') : 'none');
            icon.className = 'bi ' + (active ? (state.sort.dir === 'asc' ? 'bi-sort-up' : 'bi-sort-down') : 'bi-arrow-down-up');
        });
    }

    function rowHtml(g) {
        const highlight = g.gradeId === state.highlightId;
        return '<tr data-id="' + g.gradeId + '"' + (highlight ? ' class="row-highlight"' : '') + '>' +
            '<td><span class="cell-main">' + UI.escapeHtml(g.studentNumber) + '</span><span class="cell-sub">' + UI.escapeHtml(g.studentName) + '</span></td>' +
            '<td><span class="cell-main">' + UI.escapeHtml(g.subjectCode) + '</span><span class="cell-sub">' + UI.escapeHtml(g.subjectName) + '</span></td>' +
            '<td class="text-end"><span class="grade-number">' + UI.escapeHtml(g.grade) + '</span></td>' +
            '<td>' + UI.remarkBadge(g.remarks, { pop: highlight }) + '</td>' +
            '<td class="text-nowrap">' + UI.escapeHtml(g.semester) + '</td>' +
            '<td class="text-nowrap">' + UI.escapeHtml(g.schoolYear) + '</td>' +
            '<td class="d-none d-xl-table-cell">' + UI.escapeHtml(g.facultyName) + '</td>' +
            '<td class="d-none d-lg-table-cell small text-nowrap">' + UI.escapeHtml(UI.formatDate(g.datePosted)) + '</td>' +
            '<td class="d-none d-lg-table-cell small text-nowrap">' + (g.dateModified ? UI.escapeHtml(UI.formatDate(g.dateModified)) : '<span class="text-body-secondary">—</span>') + '</td>' +
            '<td class="text-end text-nowrap">' +
            '  <button type="button" class="btn-action me-1" data-action="edit" data-id="' + g.gradeId + '" aria-label="Edit grade of ' + UI.escapeHtml(g.studentName + ' in ' + g.subjectCode) + '" title="Edit"><i class="bi bi-pencil-square" aria-hidden="true"></i></button>' +
            '  <button type="button" class="btn-action danger" data-action="delete" data-id="' + g.gradeId + '" aria-label="Delete grade of ' + UI.escapeHtml(g.studentName + ' in ' + g.subjectCode) + '" title="Delete"><i class="bi bi-trash3" aria-hidden="true"></i></button>' +
            '</td></tr>';
    }

    function renderTable() {
        renderSortIndicators();
        els.rows.removeAttribute('aria-busy');

        const rows = sortedView();
        const total = rows.length;
        const pages = Math.max(1, Math.ceil(total / state.pageSize));
        state.page = Math.min(Math.max(1, state.page), pages);
        const start = (state.page - 1) * state.pageSize;
        const pageRows = rows.slice(start, start + state.pageSize);

        if (!total) {
            els.rows.innerHTML = '';
            els.empty.classList.remove('d-none');
            const filtered = hasFilters(currentFilters());
            els.emptyTitle.textContent = filtered ? 'No records match your filters' : 'No grade records yet';
            els.emptyText.textContent = filtered ? 'Try a different search or clear the filters.' : 'Click “Add grade” to post the first grade.';
            els.summary.textContent = state.all.length ? 'Showing 0 of ' + state.all.length + ' records' : 'No records';
        } else {
            els.empty.classList.add('d-none');
            els.rows.innerHTML = pageRows.map(rowHtml).join('');
            UI.stagger(els.rows, 'row-enter');
            els.summary.textContent = 'Showing ' + (start + 1) + '–' + (start + pageRows.length) + ' of ' + total +
                (total !== state.all.length ? ' (filtered from ' + state.all.length + ')' : '') + ' records';
        }

        renderPagination(pages);

        if (state.highlightId) {
            const id = state.highlightId;
            window.setTimeout(() => {
                const row = els.rows.querySelector('tr[data-id="' + id + '"]');
                if (row) row.classList.remove('row-highlight');
                if (state.highlightId === id) state.highlightId = null;
            }, 2200);
        }
    }

    function renderPagination(pages) {
        if (pages <= 1) {
            els.pagination.innerHTML = '';
            return;
        }
        const item = (label, page, opts) => {
            const o = opts || {};
            const cls = 'page-item' + (o.disabled ? ' disabled' : '') + (o.active ? ' active' : '');
            const aria = o.active ? ' aria-current="page"' : '';
            const ariaLabel = o.ariaLabel ? ' aria-label="' + o.ariaLabel + '"' : '';
            return '<li class="' + cls + '"><button type="button" class="page-link" data-page="' + page + '"' + aria + ariaLabel + (o.disabled ? ' disabled' : '') + '>' + label + '</button></li>';
        };

        let html = item('<i class="bi bi-chevron-left" aria-hidden="true"></i>', state.page - 1, { disabled: state.page === 1, ariaLabel: 'Previous page' });
        const windowStart = Math.max(1, Math.min(state.page - 2, pages - 4));
        const windowEnd = Math.min(pages, windowStart + 4);
        for (let p = windowStart; p <= windowEnd; p++) {
            html += item(String(p), p, { active: p === state.page, ariaLabel: 'Page ' + p });
        }
        html += item('<i class="bi bi-chevron-right" aria-hidden="true"></i>', state.page + 1, { disabled: state.page === pages, ariaLabel: 'Next page' });
        els.pagination.innerHTML = html;
    }

    // ------------------------------------------------------------------ add / edit

    function resetForm() {
        els.form.reset();
        UI.clearFieldErrors(els.form);
        UI.hideAlert(els.formAlert);
        els.auditInfo.classList.add('d-none');
        els.auditInfo.textContent = '';
        delete els.subjectName.dataset.autofilled;
    }

    function openAddModal() {
        state.editing = null;
        resetForm();
        renderStudentOptions();
        els.modalEyebrow.textContent = 'New record';
        els.modalTitle.textContent = 'Add grade';
        els.schoolYear.value = defaultSchoolYear();
        els.semester.value = '1st Semester';
        gradeModal.show();
    }

    function openEditModal(grade) {
        state.editing = grade;
        resetForm();
        renderStudentOptions();
        els.modalEyebrow.textContent = 'Edit record #' + grade.gradeId;
        els.modalTitle.textContent = 'Edit grade';

        if (!state.students.some(s => s.studentId === grade.studentId)) {
            // Student may be inactive now: keep them selectable so the record can still be viewed.
            const option = document.createElement('option');
            option.value = String(grade.studentId);
            option.textContent = grade.studentNumber + ' — ' + grade.studentName;
            els.studentId.appendChild(option);
        }
        els.studentId.value = String(grade.studentId);
        els.subjectCode.value = grade.subjectCode;
        els.subjectName.value = grade.subjectName;
        els.grade.value = String(grade.grade);
        els.semester.value = grade.semester;
        els.schoolYear.value = grade.schoolYear;

        els.auditInfo.textContent = 'Posted by ' + grade.facultyName + ' on ' + UI.formatDate(grade.datePosted) +
            (grade.dateModified ? ' • last modified ' + UI.formatDate(grade.dateModified) : '') +
            ' • current remark: ' + grade.remarks;
        els.auditInfo.classList.remove('d-none');
        gradeModal.show();
    }

    /** Builds the request body. Note: there is deliberately NO "remarks" field. */
    function readForm() {
        const rawGrade = els.grade.value.trim();
        const rawStudent = els.studentId.value;
        return {
            studentId: rawStudent ? Number(rawStudent) : null,
            subjectCode: els.subjectCode.value.trim(),
            subjectName: els.subjectName.value.trim(),
            grade: rawGrade === '' ? null : Number(rawGrade),
            semester: els.semester.value,
            schoolYear: els.schoolYear.value.trim()
        };
    }

    async function saveGrade(event) {
        event.preventDefault();
        UI.clearFieldErrors(els.form);
        UI.hideAlert(els.formAlert);

        // A browser number input returns "" for text like "abc"; tell the user instead of sending nothing.
        if (els.grade.validity && els.grade.validity.badInput) {
            UI.showFieldErrors(els.form, { grade: ['Grade must be a whole number from 0 to 100.'] });
            return;
        }

        const body = readForm();
        const editing = state.editing;
        UI.setButtonLoading(els.save, true, editing ? 'Saving changes…' : 'Saving…');

        const result = editing
            ? await Api.put('/api/grades/' + editing.gradeId, body)
            : await Api.post('/api/grades', body);

        UI.setButtonLoading(els.save, false);

        if (result.ok && result.data) {
            const saved = result.data;
            gradeModal.hide();
            state.highlightId = saved.gradeId;
            UI.toast(
                UI.escapeHtml(saved.studentName + ' • ' + saved.subjectCode) + ': grade <strong>' + UI.escapeHtml(saved.grade) +
                '</strong> → ' + UI.remarkBadge(saved.remarks, { pop: true }) +
                '<div class="mt-1 text-body-secondary">Remark computed by the server.</div>',
                'success',
                { title: editing ? 'Grade updated' : 'Grade added', html: true }
            );
            await loadAll({ quiet: true, keepPage: !!editing });
            return;
        }

        switch (result.status) {
            case 400: {
                const unmatched = UI.showFieldErrors(els.form, result.errors);
                UI.showAlert(els.formAlert, 'Please fix the highlighted fields.', 'danger', unmatched);
                break;
            }
            case 409:
                UI.showAlert(els.formAlert, result.message, 'warning');
                UI.showFieldErrors(els.form, { subjectCode: ['Already graded for this student, semester and school year.'] });
                break;
            case 404:
                gradeModal.hide();
                UI.toast(result.message || 'This grade record no longer exists.', 'warning');
                await loadAll({ quiet: true });
                break;
            case 403:
                UI.showAlert(els.formAlert, result.message || 'You do not have permission to save grades.', 'danger');
                break;
            case 401:
                break; // redirecting to login
            default:
                UI.showAlert(els.formAlert, result.message, 'danger');
        }
    }

    // ------------------------------------------------------------------ delete

    function openDeleteModal(grade) {
        state.deleting = grade;
        const rows = [
            ['Student', grade.studentNumber + ' — ' + grade.studentName],
            ['Subject', grade.subjectCode + ' — ' + grade.subjectName],
            ['Grade', String(grade.grade) + ' (' + grade.remarks + ')'],
            ['Term', grade.semester + ', ' + grade.schoolYear]
        ];
        els.deleteSummary.innerHTML = rows.map(r => '<dt>' + UI.escapeHtml(r[0]) + '</dt><dd>' + UI.escapeHtml(r[1]) + '</dd>').join('');
        deleteModal.show();
    }

    async function confirmDelete() {
        const grade = state.deleting;
        if (!grade) return;
        UI.setButtonLoading(els.confirmDelete, true, 'Deleting…');
        const result = await Api.del('/api/grades/' + grade.gradeId);
        UI.setButtonLoading(els.confirmDelete, false);

        if (result.ok || result.status === 404) {
            deleteModal.hide();
            state.deleting = null;
            UI.toast(result.ok
                ? 'Deleted ' + grade.subjectCode + ' grade of ' + grade.studentName + '.'
                : 'That record had already been deleted.', result.ok ? 'success' : 'warning');
            await loadAll({ quiet: true, keepPage: true });
            return;
        }
        if (result.status === 401) return;
        deleteModal.hide();
        UI.toast(result.message, 'error');
    }

    // ------------------------------------------------------------------ events

    function debounce(fn, wait) {
        let timer;
        return function () {
            window.clearTimeout(timer);
            timer = window.setTimeout(fn, wait);
        };
    }

    els.add.addEventListener('click', openAddModal);
    els.refresh.addEventListener('click', async () => {
        UI.setButtonLoading(els.refresh, true, 'Refreshing…');
        await loadAll({ keepPage: true });
        UI.setButtonLoading(els.refresh, false);
    });

    els.search.addEventListener('input', debounce(() => applyFilters(), 300));
    [els.semesterFilter, els.schoolYearFilter, els.subjectFilter].forEach(select => select.addEventListener('change', () => applyFilters()));
    document.getElementById('filterForm').addEventListener('submit', event => { event.preventDefault(); applyFilters(); });
    els.clearFilters.addEventListener('click', () => {
        els.search.value = '';
        els.semesterFilter.value = '';
        els.schoolYearFilter.value = '';
        els.subjectFilter.value = '';
        applyFilters();
        els.search.focus();
    });

    els.pageSize.addEventListener('change', () => {
        state.pageSize = Number(els.pageSize.value) || 10;
        state.page = 1;
        renderTable();
    });

    els.pagination.addEventListener('click', event => {
        const button = event.target.closest('[data-page]');
        if (!button || button.disabled) return;
        state.page = Number(button.dataset.page);
        renderTable();
        els.summary.scrollIntoView({ block: 'nearest' });
    });

    document.querySelectorAll('.sort-btn').forEach(button => {
        button.addEventListener('click', () => {
            const key = button.dataset.sort;
            state.sort = { key: key, dir: state.sort.key === key && state.sort.dir === 'asc' ? 'desc' : 'asc' };
            renderTable();
        });
    });

    els.rows.addEventListener('click', event => {
        const button = event.target.closest('[data-action]');
        if (!button) return;
        const id = Number(button.dataset.id);
        const grade = state.view.find(g => g.gradeId === id) || state.all.find(g => g.gradeId === id);
        if (!grade) return;
        if (button.dataset.action === 'edit') openEditModal(grade);
        if (button.dataset.action === 'delete') openDeleteModal(grade);
    });

    // Subject autocomplete: picking a known code fills in its name.
    els.subjectCode.addEventListener('input', () => {
        const code = els.subjectCode.value.trim().toUpperCase();
        const match = state.subjects.find(s => s.subjectCode.toUpperCase() === code);
        if (match && (!els.subjectName.value.trim() || els.subjectName.dataset.autofilled === 'true')) {
            els.subjectName.value = match.subjectName;
            els.subjectName.dataset.autofilled = 'true';
        }
    });
    els.subjectName.addEventListener('input', () => { delete els.subjectName.dataset.autofilled; });

    els.form.addEventListener('submit', saveGrade);
    els.confirmDelete.addEventListener('click', confirmDelete);
    els.modalEl.addEventListener('shown.bs.modal', () => els.studentId.focus());
    els.deleteModalEl.addEventListener('shown.bs.modal', () => els.deleteModalEl.querySelector('[data-bs-dismiss="modal"]').focus());

    loadAll();
})();
