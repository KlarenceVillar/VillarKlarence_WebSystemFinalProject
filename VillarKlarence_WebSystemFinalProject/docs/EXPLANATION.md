# How the System Works — Live Defense Notes

**Project:** UST Angelicum College Student Grade Management System
**Author:** Klarence Villar

This document explains the important parts of the system in plain language, with the exact
files and line numbers to open during the defense. Paths are relative to the repository root.

- `Api/…` = `src/VillarKlarence_WebSystemFinalProject.Api/…`
- `Web/…` = `src/VillarKlarence_WebSystemFinalProject.Web/…`

---

## 1. The big picture (30-second version)

```
Browser (MVC pages + JavaScript)  ──fetch() + JSON + Bearer token──►  Web API  ──EF Core──►  SQL Server
   https://localhost:7102                                              https://localhost:7101   StudentGradeManagementDB
```

- The **MVC app** (`Web`) only serves HTML, CSS and JavaScript. It holds no data and makes no security decisions.
- The **Web API** (`Api`) does everything that matters: it signs users in, checks roles, checks ownership,
  validates input, computes remarks, and reads and writes the database.
- There is **one database**, `StudentGradeManagementDB`. The instructor's script creates the academic tables
  (`Students`, `Faculty`, `StudentGrades`). The EF Core migration creates only the ASP.NET Core Identity
  tables (`AspNetUsers`, `AspNetRoles`, …) plus the foreign keys from `AspNetUsers` to `Students` and `Faculty`.

---

## 2. Authentication flow, step by step

| Step | What happens | Where |
|---|---|---|
| 1 | The user types an email and password on the login page. `login.js` sends them to `POST /api/auth/login` using `fetch()` with `async/await`. | `Web/wwwroot/js/login.js` line 71 → `Web/wwwroot/js/api.js` line 147 (`request`) |
| 2 | `AuthController.Login` passes the credentials to `AccountService.LoginAsync`. | `Api/Controllers/AuthController.cs` |
| 3 | ASP.NET Core Identity finds the user by email and checks lock-out. | `Api/Services/AccountService.cs` line 24 |
| 4 | **Identity checks the password against the stored PBKDF2 hash.** No plaintext password exists anywhere: the `AspNetUsers.PasswordHash` column holds values like `AQAAAAIAAYag…`. A wrong password counts toward lock-out (5 tries → 5 minutes). | `AccountService.cs` lines 29–31; options in `Api/Program.cs` line 42 |
| 5 | The account must have exactly one role (`Faculty` or `Student`) and must link to an **active** row in `dbo.Students` or `dbo.Faculty`. A Student must have a `StudentId` and no `FacultyId`; a Faculty member must have a `FacultyId` and no `StudentId`. | `AccountService.cs` line 70 → `Api/Security/UserAccountRules.cs` line 15 |
| 6 | A JWT is created and signed with HMAC-SHA256. Its claims are `NameIdentifier` (Identity user id), `Name`, `Email`, `Role`, and `student_id` **or** `faculty_id`. | `Api/Services/TokenService.cs` lines 27–37 and 51 |
| 7 | The browser stores the token and role in `sessionStorage`. | `login.js` line 74 → `api.js` `session.save` |
| 8 | Every later call adds `Authorization: Bearer <token>`. | `api.js` line 154 |
| 9 | On each request the API validates the token's signature, issuer, audience and expiry. The signing key comes from user-secrets, never from source code. | `Api/Program.cs` lines 66–78 (validation), line 28 (`ValidateOnStart` fails fast if `Jwt:Key` is missing) |
| 10 | `[Authorize(Roles = "...")]` on each action decides who may call it. There is also a fallback policy, so a new endpoint is private unless it is explicitly marked `[AllowAnonymous]`. | `Api/Controllers/GradesController.cs` lines 18, 28, 48, 67, 80; `Program.cs` line 85 |
| 11 | No token or a bad token returns **401**; the wrong role returns **403**. Both come back as JSON ProblemDetails. | `Api/Security/JwtProblemEvents.cs` lines 12 and 30 |
| 12 | In the browser, a 401 means the session expired. `api.js` clears the session and redirects to the login page with "Your session has expired". | `api.js` line 194 |

**Seeded accounts.** On startup the API creates the two roles and the five demo users, but only if they are
missing. Each account is linked to the matching row from the instructor's script. The password is read from
configuration (`Seed:DemoPassword`, set by `setup-secrets.cmd`), and the account rule is checked before saving.
See `Api/Data/IdentitySeeder.cs` lines 44, 87–89 and 97.

**One database, Identity mapped onto it.** `Api/Data/AppDbContext.cs` maps the instructor's tables with
`ExcludeFromMigrations()` (lines 25, 39, 53), so the migration never tries to create or alter them. It also
declares the `AspNetUsers → Students/Faculty` foreign keys (lines 91, 96) and a check constraint that blocks
an account from being both a student and a faculty member (line 86).

---

## 3. How the student ownership check works

A student must **only** ever see their own grades. There are three layers of protection.

1. **`GET /api/student/grades` has no `studentId` parameter at all.**
   `Api/Controllers/StudentGradesController.cs` line 24 reads the student id from the token:
   `User.GetStudentId()`, which comes from the `student_id` claim (`Api/Security/ClaimsPrincipalExtensions.cs` line 14).
   The token is signed by the server, so the student cannot change this value. If someone calls
   `/api/student/grades?studentId=2`, the parameter is ignored because nothing reads it. The Postman request
   "05 Ownership / ?studentId=2" proves Juan still receives only his own records.

2. **`GET /api/grades/{id}` filters by owner in the database query.**
   For a student caller, `GradesController.cs` lines 33–37 set `ownerStudentId` from the token. Then
   `GradeService.GetByIdAsync` adds `WHERE StudentId = @owner` (`Api/Services/GradeService.cs` lines 94–95).
   If Juan asks for Maria's grade id, the query returns nothing and the API answers **404 Not Found**, not
   403. That way Juan cannot even learn that the record exists.

3. **Faculty-only endpoints reject students.** `GET /api/grades`, `POST`, `PUT` and `DELETE` carry
   `[Authorize(Roles = "Faculty")]`, so a student token gets **403**. Authorization runs before model binding,
   so a student cannot even trigger validation on these endpoints.

The target record is validated and authorized on every read, update and delete: update and delete load the
record by id and return 404 if it does not exist (`GradeService.cs` `UpdateAsync` / `DeleteAsync`).

---

## 4. How validation rejects bad grades

Grades must be **whole numbers from 0 to 100**. Each kind of bad input is stopped at a different layer:

| Input | What stops it | Result |
|---|---|---|
| `105` or `-1` | `[Range(0, 100)]` on `Grade` (`Api/Dtos/GradeDtos.cs` line 33) | 400 `"Grade must be a whole number from 0 to 100."` |
| missing `grade` or `null` | `int?` + `[Required]` (`GradeDtos.cs` lines 32–34). Because the type is `int?`, a missing value is `null`, not `0`, and `[Required]` catches it. | 400 `"Grade is required. Grade must be a whole number from 0 to 100."` |
| `87.5` | The JSON number does not fit an `int`, so System.Text.Json fails while reading the body. | 400 with the friendly grade message |
| `"abc"` or `"90"` | Strict number handling (`Program.cs` line 107): a grade must be a real JSON number, not a string. | 400 with the friendly grade message |

**Friendly messages for conversion errors.** When the body cannot be converted, ASP.NET Core records an
error under the key `$.grade` with a technical message. `Api/Validation/ValidationProblemFactory.cs` is
registered as `InvalidModelStateResponseFactory` (`Program.cs` line 110). It replaces that message with
`"Grade must be a whole number from 0 to 100."` (line 39), removes the noisy "request field is required"
error (line 45), uses camelCase field names, and fills `detail` with a readable summary.

**Other rules:**
- `subjectCode`, `subjectName`, `semester` and `schoolYear` are required and have length limits.
- Semester must be `1st Semester`, `2nd Semester` or `Summer` (`Api/Validation/SemesterAttribute.cs`).
- School year must look like `2026-2027`, with consecutive years (`Api/Validation/SchoolYearAttribute.cs`).
- The student must exist and be active (`GradeService.cs` line 118, `ValidateStudentAsync`) → 400 on `studentId`.
- **Duplicates** (same student + subject + semester + school year) → **409 Conflict**. The API checks first
  (`GradeService.cs` line 121). It also catches SQL Server's unique-constraint error (`UQ_StudentGrades_StudentSubjectTerm`,
  error numbers 2627 and 2601) in case two requests save at the same moment (line 250).

The database adds a final safety net: `CK_StudentGrades_Grade CHECK (Grade BETWEEN 0 AND 100)`.

---

## 5. How and where remarks are computed

**One class decides remarks:** `Api/Services/RemarksCalculator.cs`, behind the interface `IRemarksCalculator`.
It mirrors the instructor's stored procedure `dbo.GetGradeRemarks` exactly:

| Grade | Remark |
|---|---|
| 95–100 | CMS |
| 90–94 | SP |
| 85–89 | SAP |
| 80–84 | GP |
| 75–79 | EP |
| 0–74 | BEP/FAILED |

- The `switch` expression starts at line 27. A grade outside 0–100 throws with the same message as the
  procedure's `THROW 50001` (line 24).
- **Create:** `Remarks = remarksCalculator.Calculate(input.Grade)` (`GradeService.cs` line 131).
- **Update:** recomputed on every update (`GradeService.cs` line 168), so an update can never keep an old remark.
- **The client cannot send a remark.** `GradeCreateRequest` and `GradeUpdateRequest` have **no `Remarks`
  property** (`GradeDtos.cs` line 8), so a `"remarks"` field in the JSON is simply ignored. The Postman
  folder "03 Tampering" sends grade 70 with `"remarks": "CMS"` and gets `BEP/FAILED` back.
- **The JavaScript never computes remarks.** It only displays the remark the server returns
  (`faculty-dashboard.js` line 393: "deliberately NO remarks field").
- Unit tests check every boundary: `tests/VillarKlarence_WebSystemFinalProject.Tests/RemarksCalculatorTests.cs`.

**Who posted it:** `FacultyId` comes from the logged-in faculty member's `faculty_id` claim
(`GradesController.cs` line 55 → `GradeService.cs` line 129), never from the request body. On update, the
original `FacultyId` and `DatePosted` are kept (line 172) and `DateModified` is set to UTC now (line 171).

---

## 6. Why hiding buttons is NOT security

The student dashboard has no Add, Edit or Delete buttons, and `requireRole()` in `api.js` (line 208)
redirects a student who opens `/Faculty`. **These are conveniences, not protection.** Anyone can:

- open the browser developer tools and run their own `fetch()`,
- call the API directly with Postman or curl,
- edit the JavaScript or the token stored in `sessionStorage`.

So every rule is enforced on the **server**:

| Rule | Server enforcement |
|---|---|
| Only faculty can create, update or delete | `[Authorize(Roles = "Faculty")]` → 403 for students (Postman folder 06) |
| Must be signed in | JWT bearer authentication + fallback policy → 401 (Postman folder 07) |
| Students see only their own grades | StudentId taken from the signed token, plus an owner filter in the SQL query (Postman folder 05) |
| Remarks are correct | Computed by `RemarksCalculator`; client values are ignored (Postman folder 03) |
| Grades are valid | DTO validation + strict JSON + database CHECK constraint (Postman folder 02) |
| Token cannot be forged | The signature is verified with a secret key that never leaves the server; changing any claim breaks the signature → 401 |

The instructor's note in the SQL script says the same: *"The MVC page is not a security boundary.
Protect API endpoints server-side."*

---

## 7. Error handling (the app never crashes)

- **API:** `Api/Middleware/ExceptionHandlingMiddleware.cs` catches any unexpected exception (line 22) and
  returns a **500 ProblemDetails** with a generic message. The details are only logged on the server, so
  there are no stack traces in responses. 401, 403, 404 and 409 responses are ProblemDetails as well.
- **MVC:** every `fetch()` goes through `Api.request()` in `api.js` (line 147). It **never throws**:
  - network failure, API down, or certificate not trusted → "Cannot reach the grade server…"
  - 400 → field messages shown inline under the matching input
  - 401 → session cleared, redirect to the login page
  - 403 → "You do not have permission…"
  - 404 → "The requested record was not found…"
  - 409 → the server's duplicate message
  - 500 → "The server ran into a problem…"

---

## 8. Likely defense questions

**Why JWT instead of cookies?** The MVC site and the API run on different origins (ports 7102 and 7101).
A bearer token works cleanly across origins with CORS and is easy to test in Postman.

**Where is the password stored?** Only as an Identity password hash in `AspNetUsers.PasswordHash`. The
academic tables have no password column, as the instructor's script requires.

**Where is the JWT signing key?** In `dotnet user-secrets`, which lives in your user profile and outside the
repository. `setup-secrets.cmd` generates a random 64-byte key. `appsettings.json` contains only the issuer
and audience.

**Is the SQL injectable?** No. All queries are EF Core LINQ, which sends parameterized SQL. No SQL strings
are concatenated.

**What happens when the API restarts?** Nothing is lost: all data lives in SQL Server. Migrations and seeding
are idempotent, so they skip work that is already done.
