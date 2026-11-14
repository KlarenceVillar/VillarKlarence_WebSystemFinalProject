# Postman Test Results

Project: **UST Angelicum College – Student Grade Management System**  
Collection: `VillarKlarence_GradeSystem.postman_collection.json`  
Environment: `VillarKlarence_GradeSystem.postman_environment.json` (baseUrl `https://localhost:7101`)

| Item | Value |
|---|---|
| Tested by | Klarence Villar |
| Date tested | |
| API started with | `dotnet run` (https profile) |
| Database | StudentGradeManagementDB (LocalDB / SQLEXPRESS) |
| Collection Runner result | ___ / 29 requests passed, ___ assertions passed |

How to fill this in: run the requests in order (or use **Run collection**), write the status code Postman shows in
**Actual Status**, take a screenshot of each response (status code and body visible), and save it in `Screenshots/`
with the file name from the last column.

## 00 Auth

Sign in with ASP.NET Core Identity and receive a JWT. Run this folder first: it stores facultyToken and studentToken.

| # | Test | Method | Endpoint | Role | Expected Status | Actual Status | Screenshot file |
|---|---|---|---|---|---|---|---|
| 00.1 | Login Faculty (Andrea Garcia) | POST | `/api/auth/login` | Anonymous (no token) | 200 OK | | `00-01-login-faculty-andrea-garcia.png` |
| 00.2 | Login Student (Juan Dela Cruz) | POST | `/api/auth/login` | Anonymous (no token) | 200 OK | | `00-02-login-student-juan-dela-cruz.png` |
| 00.3 | Login with wrong password (401) | POST | `/api/auth/login` | Anonymous (no token) | 401 Unauthorized | | `00-03-login-with-wrong-password-401.png` |

## 01 Faculty CRUD

Faculty creates, reads, updates and deletes a grade. Remarks are computed by the server.

| # | Test | Method | Endpoint | Role | Expected Status | Actual Status | Screenshot file |
|---|---|---|---|---|---|---|---|
| 01.1 | GET all grades (Faculty) | GET | `/api/grades` | Faculty | 200 OK | | `01-01-get-all-grades-faculty.png` |
| 01.2 | GET one grade (Faculty) | GET | `/api/grades/{{juanGradeId}}` | Faculty | 200 OK | | `01-02-get-one-grade-faculty.png` |
| 01.3 | POST valid grade 78 -> EP (201) | POST | `/api/grades` | Faculty | 201 Created | | `01-03-post-valid-grade-78-ep-201.png` |
| 01.4 | PUT change grade 78 -> 96 (EP -> CMS) | PUT | `/api/grades/{{createdGradeId}}` | Faculty | 200 OK | | `01-04-put-change-grade-78-96-ep-cms.png` |
| 01.5 | DELETE grade (204) | DELETE | `/api/grades/{{createdGradeId}}` | Faculty | 204 No Content | | `01-05-delete-grade-204.png` |
| 01.6 | GET deleted grade (404) | GET | `/api/grades/{{createdGradeId}}` | Faculty | 404 Not Found | | `01-06-get-deleted-grade-404.png` |

## 02 Validation

Invalid grades are rejected with 400 and a clear message. Duplicate student/subject/term is rejected with 409.

| # | Test | Method | Endpoint | Role | Expected Status | Actual Status | Screenshot file |
|---|---|---|---|---|---|---|---|
| 02.1 | POST grade 105 (400) | POST | `/api/grades` | Faculty | 400 Bad Request | | `02-01-post-grade-105-400.png` |
| 02.2 | POST grade -1 (400) | POST | `/api/grades` | Faculty | 400 Bad Request | | `02-02-post-grade-1-400.png` |
| 02.3 | POST grade 87.5 (400) | POST | `/api/grades` | Faculty | 400 Bad Request | | `02-03-post-grade-87-5-400.png` |
| 02.4 | POST grade "abc" (400) | POST | `/api/grades` | Faculty | 400 Bad Request | | `02-04-post-grade-abc-400.png` |
| 02.5 | POST missing grade (400) | POST | `/api/grades` | Faculty | 400 Bad Request | | `02-05-post-missing-grade-400.png` |
| 02.6 | POST duplicate student/subject/term (409) | POST | `/api/grades` | Faculty | 409 Conflict | | `02-06-post-duplicate-student-subject-term-409.png` |

## 03 Tampering

Client-supplied remarks (and facultyId) are ignored; the server always recomputes the remark.

| # | Test | Method | Endpoint | Role | Expected Status | Actual Status | Screenshot file |
|---|---|---|---|---|---|---|---|
| 03.1 | POST grade 70 with "remarks": "CMS" -> saved BEP/FAILED | POST | `/api/grades` | Faculty | 201 Created | | `03-01-post-grade-70-with-remarks-cms-saved-bep-failed.png` |
| 03.2 | PUT grade 91 with "remarks": "BEP/FAILED" -> saved SP | PUT | `/api/grades/{{tamperGradeId}}` | Faculty | 200 OK | | `03-02-put-grade-91-with-remarks-bep-failed-saved-sp.png` |
| 03.3 | Cleanup: DELETE tampering test grade (204) | DELETE | `/api/grades/{{tamperGradeId}}` | Faculty | 204 No Content | | `03-03-cleanup-delete-tampering-test-grade-204.png` |

## 04 Student read

A student reads only their own grades. The StudentId comes from the token.

| # | Test | Method | Endpoint | Role | Expected Status | Actual Status | Screenshot file |
|---|---|---|---|---|---|---|---|
| 04.1 | GET /api/auth/me (Student) | GET | `/api/auth/me` | Student (Juan) | 200 OK | | `04-01-get-api-auth-me-student.png` |
| 04.2 | GET /api/student/grades (Juan only) | GET | `/api/student/grades` | Student (Juan) | 200 OK | | `04-02-get-api-student-grades-juan-only.png` |

## 05 Ownership

A student cannot read other students' records, even by changing the URL.

| # | Test | Method | Endpoint | Role | Expected Status | Actual Status | Screenshot file |
|---|---|---|---|---|---|---|---|
| 05.1 | GET /api/student/grades?studentId=2 (still only Juan's) | GET | `/api/student/grades?studentId=2` | Student (Juan) | 200 OK | | `05-01-get-api-student-grades-studentid-2-still-only-ju.png` |
| 05.2 | GET /api/grades/{Maria's gradeId} as Juan (404) | GET | `/api/grades/{{mariaGradeId}}` | Student (Juan) | 404 Not Found | | `05-02-get-api-grades-maria-s-gradeid-as-juan-404.png` |
| 05.3 | GET /api/grades as Student (403) | GET | `/api/grades` | Student (Juan) | 403 Forbidden | | `05-03-get-api-grades-as-student-403.png` |

## 06 Student write restriction

Students cannot create, update or delete grades (the API returns 403 even if the UI were bypassed).

| # | Test | Method | Endpoint | Role | Expected Status | Actual Status | Screenshot file |
|---|---|---|---|---|---|---|---|
| 06.1 | POST /api/grades as Student (403) | POST | `/api/grades` | Student (Juan) | 403 Forbidden | | `06-01-post-api-grades-as-student-403.png` |
| 06.2 | PUT /api/grades/{id} as Student (403) | PUT | `/api/grades/{{juanGradeId}}` | Student (Juan) | 403 Forbidden | | `06-02-put-api-grades-id-as-student-403.png` |
| 06.3 | DELETE /api/grades/{id} as Student (403) | DELETE | `/api/grades/{{juanGradeId}}` | Student (Juan) | 403 Forbidden | | `06-03-delete-api-grades-id-as-student-403.png` |

## 07 No auth

Requests without a token are rejected with 401.

| # | Test | Method | Endpoint | Role | Expected Status | Actual Status | Screenshot file |
|---|---|---|---|---|---|---|---|
| 07.1 | GET /api/grades without token (401) | GET | `/api/grades` | Anonymous (no token) | 401 Unauthorized | | `07-01-get-api-grades-without-token-401.png` |
| 07.2 | GET /api/student/grades without token (401) | GET | `/api/student/grades` | Anonymous (no token) | 401 Unauthorized | | `07-02-get-api-student-grades-without-token-401.png` |
| 07.3 | POST /api/grades without token (401) | POST | `/api/grades` | Anonymous (no token) | 401 Unauthorized | | `07-03-post-api-grades-without-token-401.png` |

## Collection Runner summary

| Method | Endpoint | Role | Expected Status | Actual Status | Screenshot file |
|---|---|---|---|---|---|
| (all) | Collection Runner, whole collection | Faculty / Student / Anonymous | All tests pass | | `99-collection-runner-summary.png` |

## Notes

- 
