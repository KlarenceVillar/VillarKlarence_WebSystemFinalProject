# UST Angelicum College — Student Grade Management System

**Repository:** `VillarKlarence_WebSystemFinalProject`
**Author:** Klarence Villar — Web Systems and Technologies, Final Project

In this system, faculty members post, edit and delete student grades, and students sign in to see **only their own**
grades. The **ASP.NET Core Web API** computes remarks automatically, validates every grade on the server,
and protects every endpoint with **ASP.NET Core Identity + JWT** roles. The **ASP.NET Core MVC** site gets all
of its data from the API using JavaScript `fetch()` with `async/await` and JSON.

> Placeholder logo: `src/VillarKlarence_WebSystemFinalProject.Web/wwwroot/images/logo.png` is a temporary crest
> marked "PLACEHOLDER LOGO". Replace it with the official school logo (same file name, a square PNG works best).
> The navbar, login page, favicon, footer, splash screen and empty states all use this one file.

---

## Contents

1. [Tech stack](#tech-stack)
2. [Solution structure](#solution-structure)
3. [Prerequisites](#prerequisites)
4. [Database setup](#database-setup)
5. [Configuration](#configuration)
6. [Migrations](#migrations)
7. [Run the API and the MVC site](#run-the-api-and-the-mvc-site)
8. [Demo accounts](#demo-accounts)
9. [How to test the Faculty flow](#how-to-test-the-faculty-flow)
10. [How to test the Student flow](#how-to-test-the-student-flow)
11. [Postman](#postman)
12. [Unit and integration tests](#unit-and-integration-tests)
13. [API endpoints](#api-endpoints)
14. [Remarks table](#remarks-table)
15. [Security notes](#security-notes)
16. [Troubleshooting](#troubleshooting)
17. [Push to GitHub](#push-to-github)

---

## Tech stack

| Layer | Technology |
|---|---|
| Backend API | ASP.NET Core Web API (.NET 10, C#, controllers) |
| Frontend | ASP.NET Core MVC (Razor views), Bootstrap 5.3, Bootstrap Icons, JavaScript `fetch()` + `async/await` |
| Database | SQL Server (LocalDB or SQLEXPRESS) — database **StudentGradeManagementDB** |
| Data access | Entity Framework Core 10 (LINQ only, parameterized SQL) |
| Authentication | ASP.NET Core Identity (hashed passwords, roles) + JWT bearer tokens |
| Data format | JSON; errors as RFC 9457 ProblemDetails |
| Testing | xUnit (unit + in-memory integration tests), Postman collection |

## Solution structure

```
VillarKlarence_WebSystemFinalProject/
├── VillarKlarence_WebSystemFinalProject.slnx
├── README.md
├── setup-secrets.cmd                     ← stores Jwt:Key + demo password in dotnet user-secrets
├── dotnet-tools.json                     ← pins the dotnet-ef tool (dotnet tool restore)
├── Database/StudentGradeManagementDB.sql ← the instructor's script (verbatim)
├── docs/
│   ├── EXPLANATION.md                    ← plain-language notes for the live defense
│   └── API_ENDPOINTS.md                  ← every endpoint with JSON examples
├── Postman/
│   ├── VillarKlarence_GradeSystem.postman_collection.json
│   ├── VillarKlarence_GradeSystem.postman_environment.json
│   └── TEST_RESULTS_TEMPLATE.md
├── Screenshots/README.md                 ← expected screenshot file names
├── src/
│   ├── VillarKlarence_WebSystemFinalProject.Api/
│   │   ├── Controllers/   Auth, Grades, StudentGrades, Students, Subjects
│   │   ├── Data/          AppDbContext, IdentitySeeder, DatabaseInitializer
│   │   ├── Dtos/          request/response models (no Remarks in requests)
│   │   ├── Middleware/    global exception handling (500 ProblemDetails)
│   │   ├── Migrations/    InitialIdentity (Identity tables + FKs to Students/Faculty)
│   │   ├── Models/        Student, Faculty, StudentGrade, ApplicationUser
│   │   ├── Security/      roles, claims, JWT settings and 401/403 events
│   │   ├── Services/      RemarksCalculator, GradeService, AccountService, TokenService, LookupService
│   │   └── Validation/    semester/school-year rules, validation-response factory
│   └── VillarKlarence_WebSystemFinalProject.Web/
│       ├── Controllers/   Account (Login, AccessDenied), Faculty, Student, Home (NotFound/Error)
│       ├── Views/         _Layout, Login, Faculty dashboard, Student dashboard, AccessDenied, NotFound, Error
│       └── wwwroot/       css/site.css, css/animations.css, css/print.css,
│                          js/api.js, js/ui.js, js/login.js, js/faculty-dashboard.js, js/student-dashboard.js,
│                          images/logo.png
└── tests/
    └── VillarKlarence_WebSystemFinalProject.Tests/   RemarksCalculator, GradeService, DTO validation, API integration
```

## Prerequisites

| Tool | Version | Check it with |
|---|---|---|
| .NET SDK | 10.0 or later | `dotnet --list-sdks` |
| SQL Server | LocalDB (comes with Visual Studio) **or** SQL Server Express | `sqllocaldb info` / `sqlcmd -S .\SQLEXPRESS -Q "SELECT @@VERSION"` |
| sqlcmd (optional; SSMS works too) | any | `sqlcmd -?` |
| PowerShell (used by setup-secrets.cmd) | Windows PowerShell 5.1+ | `powershell -Command "$PSVersionTable.PSVersion"` |
| Postman | any recent version | — |
| Git | any | `git --version` |

Verify everything in one go (cmd):

```bash
dotnet --list-sdks
```
```bash
sqllocaldb info MSSQLLocalDB
```
```bash
sqlcmd -?
```

If the LocalDB instance does not exist yet, create and start it:

```bash
sqllocaldb create MSSQLLocalDB -s
```

## Database setup

**Step 1 — run the instructor's script first.** It creates the database, the academic tables, the
`dbo.GetGradeRemarks` procedure and the sample data.

*Option A — SSMS:* connect to `(localdb)\MSSQLLocalDB` (or `.\SQLEXPRESS`), open
`Database\StudentGradeManagementDB.sql`, and click **Execute**.

*Option B — sqlcmd with LocalDB* (run from the repository folder):

```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -i Database\StudentGradeManagementDB.sql
```

*Option C — sqlcmd with SQL Server Express:*

```bash
sqlcmd -S ".\SQLEXPRESS" -E -i Database\StudentGradeManagementDB.sql
```

(With newer sqlcmd versions that encrypt by default, add `-C` to trust the local certificate.)

**Step 2 — the API does the rest.** The first time it starts, the API applies the EF Core migration (Identity
tables only) and seeds the roles and demo accounts. See [Migrations](#migrations).

> Order matters: **script first, then the API.** The migration creates foreign keys from `AspNetUsers` to
> `dbo.Students` and `dbo.Faculty`, so those tables must already exist. If they do not, the API stops and
> tells you to run the script.

## Configuration

### Connection string

`src/VillarKlarence_WebSystemFinalProject.Api/appsettings.json` uses **LocalDB** by default:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=StudentGradeManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

For **SQL Server Express**, either run `setup-secrets.cmd sqlexpress` (see below), or change the value to:

```text
Server=.\SQLEXPRESS;Database=StudentGradeManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

(Inside `appsettings.json`, a backslash is written twice: `.\\SQLEXPRESS`.)

### Secrets — run `setup-secrets.cmd` once

The JWT signing key and the demo password are **not** in any file in this repository. They are stored with
`dotnet user-secrets`, in your Windows user profile. From the repository folder, run:

```bash
setup-secrets.cmd
```

or, if you use SQL Server Express:

```bash
setup-secrets.cmd sqlexpress
```

The script:
- generates a random 64-byte `Jwt:Key` with PowerShell;
- sets `Seed:DemoPassword` to `Demo@12345` (a demo-only password used to create the test accounts);
- with `sqlexpress`, also points `ConnectionStrings:DefaultConnection` at `.\SQLEXPRESS`.

Check what was stored (this prints the values):

```bash
dotnet user-secrets list --project src\VillarKlarence_WebSystemFinalProject.Api
```

Other settings:
- `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiresMinutes` (default 120) and `Cors:AllowedOrigins` are in the API's `appsettings.json`.
- The MVC site reads the API address from **one place**: `ApiSettings:BaseUrl` in
  `src/VillarKlarence_WebSystemFinalProject.Web/appsettings.json`. `_Layout.cshtml` passes it to the JavaScript.

## Migrations

**Automatic (default):** when the API starts, it:
1. checks that it can connect and that `dbo.Students` exists (otherwise it stops with a clear message);
2. applies any pending migration (`InitialIdentity`), which creates only the `AspNet*` tables plus the
   `AspNetUsers → Students/Faculty` foreign keys;
3. creates the `Faculty` and `Student` roles and the five demo accounts, but only if they are missing.

The academic tables are mapped with `ExcludeFromMigrations()`, so EF never creates or changes them.

**Manual alternative:**

```bash
dotnet tool restore
```
```bash
dotnet ef database update --project src\VillarKlarence_WebSystemFinalProject.Api
```

The migration was generated with `dotnet ef migrations add InitialIdentity` and is included in `src/…Api/Migrations/`.

## Run the API and the MVC site

Trust the HTTPS development certificate once:

```bash
dotnet dev-certs https --trust
```

Open **two** cmd windows in the repository folder.

**Window 1 — API** (https://localhost:7101):

```bash
cd src\VillarKlarence_WebSystemFinalProject.Api && dotnet run
```

Wait for `Now listening on: https://localhost:7101`. Opening https://localhost:7101 in a browser shows a small JSON status message.

**Window 2 — MVC site** (https://localhost:7102):

```bash
cd src\VillarKlarence_WebSystemFinalProject.Web && dotnet run
```

Then open **https://localhost:7102** and sign in.

You can also build and test the whole solution from the repository folder:

```bash
dotnet build VillarKlarence_WebSystemFinalProject.slnx
```
```bash
dotnet test VillarKlarence_WebSystemFinalProject.slnx
```

## Demo accounts

> **Demo-only test credentials.** All five accounts use the password **`Demo@12345`**, which `setup-secrets.cmd`
> stores in user-secrets (`Seed:DemoPassword`). Identity stores the password only as a hash. Never use this
> password for a real deployment.

| Role | Email | Linked record | Name |
|---|---|---|---|
| Faculty | andrea.garcia@example.com | FAC-001 | Andrea Garcia |
| Faculty | michael.ramos@example.com | FAC-002 | Michael Ramos |
| Student | juan.delacruz@example.com | 2026-0001 | Juan Dela Cruz |
| Student | maria.santos@example.com | 2026-0002 | Maria Santos |
| Student | pedro.reyes@example.com | 2026-0003 | Pedro Reyes |

The login page has a **Demo accounts** list. Clicking an account fills in its email; you type the password yourself.

## How to test the Faculty flow

1. Sign in as **andrea.garcia@example.com**. You land on the **Faculty Dashboard**.
2. The stat cards show total records, students, subjects and pass rate. The table lists all six sample grades
   with student number and name, subject code and name, grade, remark badge, semester, school year, faculty,
   date posted and date modified.
3. **Search** (e.g. `Maria`), filter by **semester / school year / subject**, click a column header to **sort**,
   and change **rows per page**.
4. Click **Add grade**:
   - pick *2026-0003 — Pedro Reyes*, type `MTH101` / `College Algebra`, grade `87.5`, then **Save** → the server
     returns **400** and the message *"Grade must be a whole number from 0 to 100."* appears under the field;
   - change the grade to `78` and **Save** → a toast shows **EP** (computed by the server); the row is highlighted.
5. Click ✏️ on that row, change the grade to `96`, then **Save** → the remark becomes **CMS**, and *Modified* is filled in.
6. Try to add *Juan Dela Cruz / WEB101 / 1st Semester / 2026-2027* again → **409** duplicate warning in the modal.
7. Click 🗑️ → a confirmation modal opens → **Delete permanently** → the record is removed.
8. Use the moon/sun button to toggle **dark mode**, then **Sign out**.

## How to test the Student flow

1. Sign in as **juan.delacruz@example.com**. You land on **My Grades**.
2. The header shows **Juan Dela Cruz** and **Student No. 2026-0001** (from `GET /api/auth/me`).
3. The summary cards show subjects, average grade and highest remark. The table, or cards on a phone,
   is loaded with `fetch()` from `GET /api/student/grades`.
4. The **semester tabs** filter the list. *2nd Semester* shows the friendly empty state.
5. **Print** opens a clean, printable grade report.
6. There are **no** add, edit or delete controls. Typing `https://localhost:7102/Faculty` in the address bar
   redirects to **Access Denied**, and the API would answer 403 anyway.
7. Sign in as Maria or Pedro to see that each student sees only their own grades.

## Postman

1. Start the API (see above).
2. In Postman, choose **Import** and select both files in the `Postman/` folder:
   - `VillarKlarence_GradeSystem.postman_collection.json`
   - `VillarKlarence_GradeSystem.postman_environment.json`
3. Select the environment **VillarKlarence_GradeSystem - Local** (top-right dropdown).
4. If Postman complains about the self-signed certificate, go to *Settings → General* and turn off
   **SSL certificate verification**, or run `dotnet dev-certs https --trust`.
5. Open the collection and click **Run** (Collection Runner). Run all folders **in order**: `00 Auth` saves
   `facultyToken` and `studentToken`, and `01 Faculty CRUD` saves `createdGradeId`, `juanGradeId` and `mariaGradeId`.
6. Expected result: **29 requests, 79 tests, all passing.** The collection cleans up after itself, so you can
   run it again as many times as you like.

| Folder | What it proves |
|---|---|
| 00 Auth | Login for faculty and student (tokens saved); wrong password → 401 |
| 01 Faculty CRUD | GET all/one 200, POST 201 + Location (78 → EP), PUT 200 (96 → CMS), DELETE 204, GET deleted → 404 |
| 02 Validation | 105, -1, 87.5, "abc", missing grade → 400; duplicate → 409 |
| 03 Tampering | `"remarks": "CMS"` with grade 70 → saved BEP/FAILED; `"remarks": "BEP/FAILED"` with 91 → saved SP |
| 04 Student read | `/api/auth/me` and `/api/student/grades` return only Juan's data |
| 05 Ownership | `?studentId=2` ignored; Juan reading Maria's grade → 404; student on `/api/grades` → 403 |
| 06 Student write restriction | POST / PUT / DELETE as student → 403 |
| 07 No auth | No token → 401 |

Record the results and screenshots with `Postman/TEST_RESULTS_TEMPLATE.md` and `Screenshots/README.md`.

## Unit and integration tests

```bash
dotnet test VillarKlarence_WebSystemFinalProject.slnx
```

98 tests, organized as follows:
- **RemarksCalculatorTests:** every boundary (0, 74, 75, 79, 80, 84, 85, 89, 90, 94, 95, 100), values inside each
  range, out-of-range values that throw, and every grade from 0 to 100.
- **GradeServiceTests** (EF Core InMemory): create computes the remark, update recomputes it (EP → CMS), client
  remarks are ignored, duplicates are rejected, unknown or inactive students are rejected, the original poster
  is kept, the student ownership filter works, the query filters work, and delete works.
- **GradeRequestValidationTests:** grade range and required rule, semester list, school-year format, and a check
  that the write DTOs have no `Remarks` or `FacultyId` properties.
- **ApiIntegrationTests:** the real HTTP pipeline (JWT, roles, validation, ProblemDetails), mirroring the Postman collection.

## API endpoints

Full request/response examples are in **[docs/API_ENDPOINTS.md](docs/API_ENDPOINTS.md)**.

| Method | Endpoint | Role | Success | Notes |
|---|---|---|---|---|
| POST | `/api/auth/login` | Anonymous | 200 | Returns JWT + user info; 401 on bad credentials |
| GET | `/api/auth/me` | Faculty / Student | 200 | Name, role, studentNumber / facultyNumber |
| GET | `/api/grades` | Faculty | 200 | Optional `?studentId=&subjectCode=&semester=&schoolYear=&search=` |
| GET | `/api/grades/{id}` | Faculty: any; Student: own only | 200 | Another student's record → 404 |
| POST | `/api/grades` | Faculty | 201 + Location | Remarks computed by the server; FacultyId from the token |
| PUT | `/api/grades/{id}` | Faculty | 200 | Remarks recomputed; original poster kept; DateModified = UTC now |
| DELETE | `/api/grades/{id}` | Faculty | 204 | |
| GET | `/api/student/grades` | Student | 200 | StudentId comes only from the `student_id` claim |
| GET | `/api/students` | Faculty | 200 | Active students (form dropdown) |
| GET | `/api/subjects` | Faculty | 200 | Distinct SubjectCode/SubjectName pairs (autocomplete) |

## Remarks table

Computed only on the server by `RemarksCalculator` (the same rules as `dbo.GetGradeRemarks`), on every create and every update:

| Grade | Remarks |
|---|---|
| 95–100 | CMS |
| 90–94 | SP |
| 85–89 | SAP |
| 80–84 | GP |
| 75–79 | EP |
| 0–74 | BEP/FAILED |

## Security notes

- **Passwords:** ASP.NET Core Identity stores PBKDF2 hashes only. The academic tables have no password column.
  After 5 wrong attempts, the account is locked for 5 minutes.
- **Tokens:** HMAC-SHA256 signed JWTs. The signature, issuer, audience and lifetime are all validated. The
  signing key lives in user-secrets and is never committed. The API refuses to start without a key of at least 32 characters.
- **Authorization on the server:** `[Authorize(Roles = "Faculty")]` on every faculty endpoint and
  `[Authorize(Roles = "Student")]` on `/api/student/grades`. A fallback policy requires sign-in everywhere else.
  No token → 401, wrong role → 403, both as JSON ProblemDetails.
- **Ownership:** the student's identity comes from the signed token's `student_id` claim, never from the URL or
  the body. Another student's record returns 404 so its existence stays hidden.
- **Integrity:** remarks and `FacultyId` are always set by the server. Client-sent values are ignored because
  the request DTOs do not have those properties.
- **Accounts:** a Student account must have a StudentId and no FacultyId; a Faculty account the reverse. This is
  enforced by the seeder, at login, and by a database check constraint.
- **SQL injection:** only EF Core LINQ (parameterized) is used. No SQL strings are concatenated.
- **Errors:** global middleware turns unexpected exceptions into a generic 500 ProblemDetails, with no stack traces.
- **CORS:** only `https://localhost:7102` (the MVC site) may call the API from a browser.
- **The UI is not a security boundary.** Hiding buttons and the page role checks in `api.js` are conveniences;
  the API enforces every rule. See [docs/EXPLANATION.md](docs/EXPLANATION.md).
- **Browser storage:** the token is kept in `sessionStorage`, so it is cleared when the tab closes and by
  **Sign out**. Tokens expire after 120 minutes.

## Troubleshooting

| Problem | Fix |
|---|---|
| **LocalDB vs SQLEXPRESS** — "A network-related or instance-specific error…" | Check which one you have: `sqllocaldb info` (LocalDB) or look for the *SQL Server (SQLEXPRESS)* service. For Express, run `setup-secrets.cmd sqlexpress` or edit the connection string. Start LocalDB with `sqllocaldb start MSSQLLocalDB`. |
| API stops with "The academic tables are missing" or "Cannot connect to the StudentGradeManagementDB database" | Run `Database\StudentGradeManagementDB.sql` against the **same** server that is in the connection string, then start the API again. |
| API stops with "Jwt:Key is missing" | Run `setup-secrets.cmd` from the repository folder. |
| Warning "Demo accounts were not created" | `Seed:DemoPassword` is missing. Run `setup-secrets.cmd`, then restart the API. |
| **"Login failed for user …"** (SQL Server) | Your Windows account has no access to that SQL instance. Use LocalDB (always your own user), or in SSMS add your Windows login to SQLEXPRESS, or connect as an administrator and grant access. |
| **"Invalid email or password"** on the login page | Use one of the demo emails with `Demo@12345`. If the accounts were created with another password, delete the rows from `AspNetUsers` (or drop and recreate the database), set the secret again, and restart the API. After 5 wrong attempts, wait 5 minutes. |
| **Certificate errors** — "Cannot reach the grade server" on the login page, or `ERR_CERT_AUTHORITY_INVALID` | Run `dotnet dev-certs https --trust`, then restart the browser. Open https://localhost:7101 once and accept the certificate. In Postman, turn off *SSL certificate verification*. |
| **CORS error** in the browser console | Open the site at exactly `https://localhost:7102`. If you change the Web port, add the new origin to `Cors:AllowedOrigins` in the API's `appsettings.json`. |
| **Port already in use** (7101 / 7102) | Close the other `dotnet run` window, or find the process: `netstat -ano \| findstr :7101`, then `taskkill /PID <pid> /F`. |
| The Web page says "Cannot reach the grade server" | The API is not running or not on 7101. Check `ApiSettings:BaseUrl` in the Web's `appsettings.json`. |
| `dotnet ef` not found | Run `dotnet tool restore` in the repository folder (uses `dotnet-tools.json`). |
| Session expired message | Tokens last 120 minutes. Sign in again. |

## Push to GitHub

Run these in **cmd**. Replace the folder where you unzipped this project if it is different.

1. Clone the (empty) repository next to the unzipped folder:

```bash
git clone https://github.com/KlarenceVillar/VillarKlarence_WebSystemFinalProject.git VillarKlarence_repo
```

2. Copy the project files into the clone (this copies hidden files too, but skips build output):

```bash
robocopy VillarKlarence_WebSystemFinalProject VillarKlarence_repo /E /XD bin obj .vs TestResults /XF *.user
```

3. Go into the clone:

```bash
cd VillarKlarence_repo
```

4. Check that no secrets or build folders are staged (you should see no `bin`, `obj` or `secrets.json`):

```bash
git add -A && git status
```

5. Commit:

```bash
git commit -m "Student Grade Management System: Web API, MVC, Identity/JWT, tests, Postman"
```

6. Push (use `main`; if your default branch is `master`, use that instead):

```bash
git branch -M main
```
```bash
git push -u origin main
```

If GitHub asks you to sign in, use your browser sign-in or a Personal Access Token as the password.
