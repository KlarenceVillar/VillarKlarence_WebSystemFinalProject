# API Endpoints

Base URL (development): **`https://localhost:7101`**

- All request and response bodies are JSON (`Content-Type: application/json`).
- Protected endpoints need the header `Authorization: Bearer <token>`. You get the token from `POST /api/auth/login`.
- Errors use the RFC 9457 **ProblemDetails** format (`Content-Type: application/problem+json`).
- Field names are camelCase.

## Summary

| Method | Endpoint | Role | Success | Possible errors |
|---|---|---|---|---|
| POST | `/api/auth/login` | Anonymous | 200 | 400, 401, 403 |
| GET | `/api/auth/me` | Faculty or Student | 200 | 401 |
| GET | `/api/grades` | Faculty | 200 | 401, 403 |
| GET | `/api/grades/{id}` | Faculty (any record) / Student (own record only) | 200 | 401, 403, 404 |
| POST | `/api/grades` | Faculty | 201 + `Location` | 400, 401, 403, 409 |
| PUT | `/api/grades/{id}` | Faculty | 200 | 400, 401, 403, 404, 409 |
| DELETE | `/api/grades/{id}` | Faculty | 204 | 401, 403, 404 |
| GET | `/api/student/grades` | Student | 200 | 401, 403 |
| GET | `/api/students` | Faculty | 200 | 401, 403 |
| GET | `/api/subjects` | Faculty | 200 | 401, 403 |

Every endpoint can also return **500** with a generic ProblemDetails body. Stack traces are never returned.

---

## Authentication

### POST `/api/auth/login`

Checks the email and password with ASP.NET Core Identity and returns a signed JWT.

**Request**
```json
{
  "email": "andrea.garcia@example.com",
  "password": "Demo@12345"
}
```

**200 OK**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ...",
  "tokenType": "Bearer",
  "expiresAtUtc": "2026-11-14T08:41:42.1256807Z",
  "user": {
    "userId": "6892d232-abe5-4afe-b37f-5b00bd068308",
    "email": "andrea.garcia@example.com",
    "fullName": "Andrea Garcia",
    "role": "Faculty",
    "studentId": null,
    "studentNumber": null,
    "facultyId": 1,
    "facultyNumber": "FAC-001"
  }
}
```

The token contains these claims: `nameidentifier` (Identity user id), `name`, `emailaddress`, `role`,
and `student_id` (students) or `faculty_id` (faculty). It expires after `Jwt:ExpiresMinutes` (default 120).

**401 Unauthorized** — wrong email or password (the message is the same for both, on purpose)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Login failed",
  "status": 401,
  "detail": "Invalid email or password.",
  "traceId": "00-65e56443ab9404ec255b98900dbf9a32-65df903295d98031-00"
}
```

Other responses:
- **401** `"Account locked"` after 5 wrong passwords (the lock lasts 5 minutes).
- **403** when the account is not linked to an active student or faculty record.
- **400** when `email` or `password` is missing or the email is not valid.

### GET `/api/auth/me`

Returns the signed-in user's profile. Used for the student dashboard header.

**200 OK** (student)
```json
{
  "userId": "0af5e4b4-84ca-47c8-8022-fa97cf8dd815",
  "email": "juan.delacruz@example.com",
  "fullName": "Juan Dela Cruz",
  "role": "Student",
  "studentId": 1,
  "studentNumber": "2026-0001",
  "facultyId": null,
  "facultyNumber": null
}
```

---

## Grades (Faculty)

### Grade record (response shape for every grade endpoint)
```json
{
  "gradeId": 1,
  "studentId": 1,
  "studentNumber": "2026-0001",
  "studentName": "Juan Dela Cruz",
  "subjectCode": "WEB101",
  "subjectName": "Web Systems and Technologies",
  "facultyId": 1,
  "facultyName": "Andrea Garcia",
  "grade": 98,
  "remarks": "CMS",
  "semester": "1st Semester",
  "schoolYear": "2026-2027",
  "datePosted": "2026-11-14T06:33:47Z",
  "dateModified": null
}
```
`remarks` is always computed by the server. `datePosted` and `dateModified` are UTC.

### GET `/api/grades`

Lists all grade records. Every query parameter is optional:

| Parameter | Example | Meaning |
|---|---|---|
| `studentId` | `2` | Only this student's records |
| `subjectCode` | `WEB101` | Exact subject code (not case-sensitive) |
| `semester` | `1st Semester` | Exact semester |
| `schoolYear` | `2026-2027` | Exact school year |
| `search` | `Maria` | Matches student number, first or last name, full name, subject code or name, or remark |

Example: `GET /api/grades?semester=1st%20Semester&search=WEB`

**200 OK** — an array of grade records (an empty array when nothing matches).
**403** if the caller is a student.

### GET `/api/grades/{id}`

- **Faculty:** returns any record, or **404** if it does not exist.
- **Student:** returns the record only if it belongs to the student in the token. Another student's record
  returns **404**, so its existence is hidden.

**404 Not Found**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not found",
  "status": 404,
  "detail": "Grade record 3 was not found.",
  "traceId": "00-8c3571ba5fd3fa17a7590ff7fdc3293c-36c8fedd5dd5ac9b-00"
}
```

### POST `/api/grades`

**Request** — there is **no `remarks` field and no `facultyId` field**. If a client sends them, they are ignored.
```json
{
  "studentId": 3,
  "subjectCode": "MTH101",
  "subjectName": "College Algebra",
  "grade": 78,
  "semester": "1st Semester",
  "schoolYear": "2026-2027"
}
```

**201 Created**, with the header `Location: https://localhost:7101/api/grades/7`
```json
{
  "gradeId": 7,
  "studentId": 3,
  "studentNumber": "2026-0003",
  "studentName": "Pedro Reyes",
  "subjectCode": "MTH101",
  "subjectName": "College Algebra",
  "facultyId": 1,
  "facultyName": "Andrea Garcia",
  "grade": 78,
  "remarks": "EP",
  "semester": "1st Semester",
  "schoolYear": "2026-2027",
  "datePosted": "2026-11-14T06:36:12Z",
  "dateModified": null
}
```

What the server sets itself:
- `facultyId` comes from the signed-in faculty member's token.
- `datePosted` is UTC now.
- `remarks` comes from `RemarksCalculator`.
- `subjectCode` is trimmed and upper-cased.

**Validation rules (400 Bad Request)**

| Field | Rule |
|---|---|
| `studentId` | Required; the student must exist and be active |
| `subjectCode` | Required, 2–30 characters: letters, numbers, spaces, hyphens |
| `subjectName` | Required, 3–200 characters |
| `grade` | Required **whole number from 0 to 100**. Rejected: `105`, `-1`, `87.5`, `"abc"`, `"90"` (a string), `null`, missing |
| `semester` | `1st Semester`, `2nd Semester` or `Summer` |
| `schoolYear` | `YYYY-YYYY` with consecutive years, for example `2026-2027` |

**400** for `"grade": 87.5` or `"grade": "abc"`
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Grade must be a whole number from 0 to 100.",
  "instance": "/api/grades",
  "errors": {
    "grade": [ "Grade must be a whole number from 0 to 100." ]
  },
  "traceId": "00-…"
}
```

**400** when several fields are invalid
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Semester must be one of: 1st Semester, 2nd Semester, Summer. School year must use the format YYYY-YYYY with consecutive years (for example 2026-2027). Subject name must be 3 to 200 characters.",
  "instance": "/api/grades",
  "errors": {
    "semester": [ "Semester must be one of: 1st Semester, 2nd Semester, Summer." ],
    "schoolYear": [ "School year must use the format YYYY-YYYY with consecutive years (for example 2026-2027)." ],
    "subjectName": [ "Subject name must be 3 to 200 characters." ]
  },
  "traceId": "00-…"
}
```

**409 Conflict** — the same student, subject, semester and school year already exists
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Duplicate grade record",
  "status": 409,
  "detail": "Student 2026-0001 already has a grade for WEB101 in 1st Semester, school year 2026-2027. Edit the existing record instead of adding a new one.",
  "traceId": "00-…"
}
```

### PUT `/api/grades/{id}`

Same body and validation as POST. The remark is **recomputed on every update**. The original `facultyId`
(the faculty member who posted the grade) and `datePosted` are kept, and `dateModified` is set to UTC now.

**Request**
```json
{
  "studentId": 3,
  "subjectCode": "MTH101",
  "subjectName": "College Algebra",
  "grade": 96,
  "semester": "1st Semester",
  "schoolYear": "2026-2027"
}
```
**200 OK** — the updated record, with `"grade": 96`, `"remarks": "CMS"` and `"dateModified": "2026-11-14T06:40:03Z"`.
Other responses: **404** when the id does not exist, **409** when the change would duplicate another record,
**400** for invalid fields.

### DELETE `/api/grades/{id}`

**204 No Content** on success (empty body). **404** when the record does not exist.

---

## Student

### GET `/api/student/grades`

Returns **only the signed-in student's** grades. The StudentId comes from the token's `student_id` claim.
A `?studentId=` query value or a request body is ignored.

**200 OK**
```json
[
  {
    "gradeId": 2,
    "studentId": 1,
    "studentNumber": "2026-0001",
    "studentName": "Juan Dela Cruz",
    "subjectCode": "NET101",
    "subjectName": "Networking Fundamentals",
    "facultyId": 1,
    "facultyName": "Andrea Garcia",
    "grade": 92,
    "remarks": "SP",
    "semester": "1st Semester",
    "schoolYear": "2026-2027",
    "datePosted": "2026-11-14T06:33:47Z",
    "dateModified": null
  }
]
```
**403** if the caller is faculty.

---

## Lookups (Faculty)

### GET `/api/students`
Active students, used for the dropdown in the grade form.
```json
[
  { "studentId": 1, "studentNumber": "2026-0001", "fullName": "Juan Dela Cruz", "email": "juan.delacruz@example.com" }
]
```

### GET `/api/subjects`
Distinct `subjectCode` / `subjectName` pairs already used in `StudentGrades`, for autocomplete.
```json
[
  { "subjectCode": "NET101", "subjectName": "Networking Fundamentals" },
  { "subjectCode": "WEB101", "subjectName": "Web Systems and Technologies" }
]
```

---

## Authentication and authorization errors

**401 Unauthorized** (no token, invalid token, or expired token)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication is required. Sign in and send the header 'Authorization: Bearer <token>'.",
  "instance": "/api/grades",
  "traceId": "00-…"
}
```
For an expired token the detail is `"Your session has expired. Please sign in again."`

**403 Forbidden** (signed in, but the role is not allowed)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to perform this action.",
  "instance": "/api/grades",
  "traceId": "00-…"
}
```

**500 Internal Server Error**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "An unexpected error occurred.",
  "status": 500,
  "detail": "The server could not complete your request. Please try again later.",
  "traceId": "00-…"
}
```

## Remarks table

| Grade | Remarks |
|---|---|
| 95–100 | CMS |
| 90–94 | SP |
| 85–89 | SAP |
| 80–84 | GP |
| 75–79 | EP |
| 0–74 | BEP/FAILED |
