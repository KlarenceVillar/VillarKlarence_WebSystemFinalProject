# Screenshots

Evidence for the required tests, captured on 2026-11-14 from a real run of the system:

- **API tests (`00-…` to `07-…`, `99-…`):** the Postman collection `Postman/VillarKlarence_GradeSystem.postman_collection.json`
  run with **Newman** (Postman's command-line Collection Runner) against the live API (https://localhost:7101) and SQL Server.
  Each image shows the method, endpoint, authenticated role, real response status, request body, response body and
  the Postman test results. JWTs are shortened for display.
- **Web application (`ui-…`):** the running MVC site (https://localhost:7102) in Chromium, with real API data.
- **Database (`db-…`):** `sqlcmd` query results from StudentGradeManagementDB after running the instructor's script and the
  EF Core migration.

If your instructor wants captures from the Postman desktop app itself, import the collection, run it, and replace the
`00-…` to `99-…` images using the same file names.

## Postman (one per request, in collection order)

| File name | Folder | Request |
|---|---|---|
| `00-01-login-faculty-andrea-garcia.png` | 00 Auth | Login Faculty (Andrea Garcia) |
| `00-02-login-student-juan-dela-cruz.png` | 00 Auth | Login Student (Juan Dela Cruz) |
| `00-03-login-with-wrong-password-401.png` | 00 Auth | Login with wrong password (401) |
| `01-01-get-all-grades-faculty.png` | 01 Faculty CRUD | GET all grades (Faculty) |
| `01-02-get-one-grade-faculty.png` | 01 Faculty CRUD | GET one grade (Faculty) |
| `01-03-post-valid-grade-78-ep-201.png` | 01 Faculty CRUD | POST valid grade 78 -> EP (201) |
| `01-04-put-change-grade-78-96-ep-cms.png` | 01 Faculty CRUD | PUT change grade 78 -> 96 (EP -> CMS) |
| `01-05-delete-grade-204.png` | 01 Faculty CRUD | DELETE grade (204) |
| `01-06-get-deleted-grade-404.png` | 01 Faculty CRUD | GET deleted grade (404) |
| `02-01-post-grade-105-400.png` | 02 Validation | POST grade 105 (400) |
| `02-02-post-grade-1-400.png` | 02 Validation | POST grade -1 (400) |
| `02-03-post-grade-87-5-400.png` | 02 Validation | POST grade 87.5 (400) |
| `02-04-post-grade-abc-400.png` | 02 Validation | POST grade "abc" (400) |
| `02-05-post-missing-grade-400.png` | 02 Validation | POST missing grade (400) |
| `02-06-post-duplicate-student-subject-term-409.png` | 02 Validation | POST duplicate student/subject/term (409) |
| `03-01-post-grade-70-with-remarks-cms-saved-bep-failed.png` | 03 Tampering | POST grade 70 with "remarks": "CMS" -> saved BEP/FAILED |
| `03-02-put-grade-91-with-remarks-bep-failed-saved-sp.png` | 03 Tampering | PUT grade 91 with "remarks": "BEP/FAILED" -> saved SP |
| `03-03-cleanup-delete-tampering-test-grade-204.png` | 03 Tampering | Cleanup: DELETE tampering test grade (204) |
| `04-01-get-api-auth-me-student.png` | 04 Student read | GET /api/auth/me (Student) |
| `04-02-get-api-student-grades-juan-only.png` | 04 Student read | GET /api/student/grades (Juan only) |
| `05-01-get-api-student-grades-studentid-2-still-only-ju.png` | 05 Ownership | GET /api/student/grades?studentId=2 (still only Juan's) |
| `05-02-get-api-grades-maria-s-gradeid-as-juan-404.png` | 05 Ownership | GET /api/grades/{Maria's gradeId} as Juan (404) |
| `05-03-get-api-grades-as-student-403.png` | 05 Ownership | GET /api/grades as Student (403) |
| `06-01-post-api-grades-as-student-403.png` | 06 Student write restriction | POST /api/grades as Student (403) |
| `06-02-put-api-grades-id-as-student-403.png` | 06 Student write restriction | PUT /api/grades/{id} as Student (403) |
| `06-03-delete-api-grades-id-as-student-403.png` | 06 Student write restriction | DELETE /api/grades/{id} as Student (403) |
| `07-01-get-api-grades-without-token-401.png` | 07 No auth | GET /api/grades without token (401) |
| `07-02-get-api-student-grades-without-token-401.png` | 07 No auth | GET /api/student/grades without token (401) |
| `07-03-post-api-grades-without-token-401.png` | 07 No auth | POST /api/grades without token (401) |
| `99-collection-runner-summary.png` | (all) | Collection Runner summary: every test passing |

## Web application

| File name | What to capture |
|---|---|
| `ui-01-login-page.png` | Login page (light mode) with the logo hero |
| `ui-02-faculty-dashboard.png` | Faculty dashboard: stat cards and grade table |
| `ui-03-faculty-add-grade-validation.png` | Add grade modal showing the server validation message (e.g. 87.5) |
| `ui-04-faculty-grade-saved-toast.png` | Toast after saving, showing the server-computed remark badge |
| `ui-05-faculty-edit-grade.png` | Edit grade modal (78 → 96) |
| `ui-06-faculty-delete-confirmation.png` | Delete confirmation modal |
| `ui-07-faculty-duplicate-409.png` | Duplicate grade warning (409) inside the modal |
| `ui-08-student-dashboard.png` | Student dashboard (Juan): profile header, summary cards, grade table |
| `ui-09-student-mobile.png` | Student dashboard on a phone-sized window (cards layout) |
| `ui-10-student-empty-semester.png` | Empty state (e.g. the 2nd Semester tab) |
| `ui-11-dark-mode.png` | Dark mode |
| `ui-12-access-denied.png` | Student opening /Faculty → Access Denied page |
| `ui-13-not-found.png` | Not Found page |
| `ui-14-session-expired.png` | Login page after the session expired (401) |
| `db-01-database-tables.png` | Database tables, foreign keys and constraints (academic + AspNet* tables) |
| `db-02-password-hashes.png` | AspNetUsers: hashed passwords, roles and StudentId/FacultyId links |

The full checklist with Expected vs Actual status codes is in `Postman/TEST_RESULTS_TEMPLATE.md`.
