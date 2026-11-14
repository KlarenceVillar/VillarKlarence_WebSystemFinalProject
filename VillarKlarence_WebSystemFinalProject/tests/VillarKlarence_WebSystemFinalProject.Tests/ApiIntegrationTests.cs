using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VillarKlarence_WebSystemFinalProject.Tests.Infrastructure;

namespace VillarKlarence_WebSystemFinalProject.Tests;

/// <summary>
/// End-to-end checks through the real HTTP pipeline (routing, JWT, roles, model validation, ProblemDetails).
/// These mirror the Postman collection.
/// </summary>
public sealed class ApiIntegrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Faculty = "andrea.garcia@example.com";
    private const string Juan = "juan.delacruz@example.com";

    private async Task<HttpClient> ClientAsync(string? email = null)
    {
        var client = factory.CreateClient();
        if (email is not null)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestData.DemoPassword });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static string GradeJson(string gradeLiteral, string code, int studentId = 3, string extra = "") =>
        $$"""{"studentId":{{studentId}},"subjectCode":"{{code}}","subjectName":"Integration Subject","grade":{{gradeLiteral}},"semester":"2nd Semester","schoolYear":"2026-2027"{{extra}}}""";

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    // ---------------------------------------------------------------- auth

    [Fact]
    public async Task Login_Faculty_ReturnsTokenAndProfile()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = Faculty, password = TestData.DemoPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.Equal("Faculty", body.GetProperty("user").GetProperty("role").GetString());
        Assert.Equal("FAC-001", body.GetProperty("user").GetProperty("facultyNumber").GetString());
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401ProblemDetails()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "pedro.reyes@example.com", password = "Wrong@12345" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Invalid email or password.", (await BodyAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Me_Student_ReturnsNameAndStudentNumber()
    {
        var client = await ClientAsync(Juan);
        var body = await BodyAsync(await client.GetAsync("/api/auth/me"));

        Assert.Equal("Juan Dela Cruz", body.GetProperty("fullName").GetString());
        Assert.Equal("2026-0001", body.GetProperty("studentNumber").GetString());
        Assert.Equal("Student", body.GetProperty("role").GetString());
    }

    [Theory]
    [InlineData("GET", "/api/grades")]
    [InlineData("GET", "/api/student/grades")]
    [InlineData("POST", "/api/grades")]
    [InlineData("GET", "/api/auth/me")]
    public async Task NoToken_Returns401Json(string method, string url)
    {
        var client = factory.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = method == "POST" ? Json(GradeJson("90", "NOAUTH1")) : null
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(401, (await BodyAsync(response)).GetProperty("status").GetInt32());
    }

    // ---------------------------------------------------------------- faculty CRUD

    [Fact]
    public async Task Faculty_FullCrudLifecycle()
    {
        var client = await ClientAsync(Faculty);

        var create = await client.PostAsync("/api/grades", Json(GradeJson("78", "CRUD101")));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        var created = await BodyAsync(create);
        var id = created.GetProperty("gradeId").GetInt32();
        Assert.Equal("EP", created.GetProperty("remarks").GetString());
        Assert.Equal(1, created.GetProperty("facultyId").GetInt32());

        var get = await client.GetAsync($"/api/grades/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var update = await client.PutAsync($"/api/grades/{id}", Json(GradeJson("96", "CRUD101")));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await BodyAsync(update);
        Assert.Equal("CMS", updated.GetProperty("remarks").GetString());
        Assert.NotEqual(JsonValueKind.Null, updated.GetProperty("dateModified").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/grades/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/grades/{id}")).StatusCode);
    }

    // ---------------------------------------------------------------- validation

    [Theory]
    [InlineData("105")]
    [InlineData("-1")]
    [InlineData("87.5")]
    [InlineData("\"abc\"")]
    [InlineData("\"90\"")]
    [InlineData("null")]
    [InlineData("true")]
    public async Task InvalidGrade_Returns400WithClearMessage(string gradeLiteral)
    {
        var client = await ClientAsync(Faculty);
        var response = await client.PostAsync("/api/grades", Json(GradeJson(gradeLiteral, "VAL101")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await BodyAsync(response);
        var message = body.GetProperty("errors").GetProperty("grade")[0].GetString();
        Assert.Contains("Grade must be a whole number from 0 to 100.", message);
    }

    [Fact]
    public async Task MissingGrade_Returns400()
    {
        var client = await ClientAsync(Faculty);
        const string json = """{"studentId":3,"subjectCode":"VAL102","subjectName":"No Grade","semester":"1st Semester","schoolYear":"2026-2027"}""";

        var response = await client.PostAsync("/api/grades", Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Grade is required", (await BodyAsync(response)).GetProperty("errors").GetProperty("grade")[0].GetString());
    }

    [Fact]
    public async Task BadSemesterSchoolYearAndStudent_Return400()
    {
        var client = await ClientAsync(Faculty);
        const string json = """{"studentId":999,"subjectCode":"VAL103","subjectName":"Bad Term","grade":90,"semester":"Fall","schoolYear":"2026-2030"}""";
        var body = await BodyAsync(await client.PostAsync("/api/grades", Json(json)));

        Assert.True(body.GetProperty("errors").TryGetProperty("semester", out _));
        Assert.True(body.GetProperty("errors").TryGetProperty("schoolYear", out _));

        var unknownStudent = await client.PostAsync("/api/grades", Json(GradeJson("90", "VAL104", studentId: 999)));
        Assert.Equal(HttpStatusCode.BadRequest, unknownStudent.StatusCode);
        Assert.True((await BodyAsync(unknownStudent)).GetProperty("errors").TryGetProperty("studentId", out _));
    }

    [Fact]
    public async Task EmptyBody_Returns400()
    {
        var client = await ClientAsync(Faculty);
        var response = await client.PostAsync("/api/grades", Json(""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_Returns409()
    {
        var client = await ClientAsync(Faculty);
        const string json = """{"studentId":1,"subjectCode":"WEB101","subjectName":"Web Systems and Technologies","grade":90,"semester":"1st Semester","schoolYear":"2026-2027"}""";

        var response = await client.PostAsync("/api/grades", Json(json));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("already has a grade", (await BodyAsync(response)).GetProperty("detail").GetString());
    }

    // ---------------------------------------------------------------- tampering

    [Fact]
    public async Task Tampering_ClientRemarksAreIgnored_OnCreateAndUpdate()
    {
        var client = await ClientAsync(Faculty);

        var create = await client.PostAsync("/api/grades",
            Json(GradeJson("70", "TAMP101", extra: ""","remarks":"CMS","facultyId":2""")));
        var created = await BodyAsync(create);
        Assert.Equal("BEP/FAILED", created.GetProperty("remarks").GetString());
        Assert.Equal(1, created.GetProperty("facultyId").GetInt32());
        var id = created.GetProperty("gradeId").GetInt32();

        var update = await client.PutAsync($"/api/grades/{id}",
            Json(GradeJson("91", "TAMP101", extra: ""","remarks":"BEP/FAILED" """)));
        Assert.Equal("SP", (await BodyAsync(update)).GetProperty("remarks").GetString());

        await client.DeleteAsync($"/api/grades/{id}");
    }

    // ---------------------------------------------------------------- student access & ownership

    [Fact]
    public async Task Student_GetsOnlyOwnGrades_EvenWithStudentIdQuery()
    {
        var client = await ClientAsync(Juan);

        foreach (var url in new[] { "/api/student/grades", "/api/student/grades?studentId=2" })
        {
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var grades = (await BodyAsync(response)).EnumerateArray().ToList();
            Assert.NotEmpty(grades);
            Assert.All(grades, g => Assert.Equal("2026-0001", g.GetProperty("studentNumber").GetString()));
        }
    }

    [Fact]
    public async Task Student_GetAnotherStudentsGradeById_Returns404()
    {
        var client = await ClientAsync(Juan);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/grades/{TestData.MariaWeb101GradeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/grades/{TestData.JuanWeb101GradeId}")).StatusCode);
    }

    [Fact]
    public async Task Student_CannotUseFacultyEndpoints_Returns403()
    {
        var client = await ClientAsync(Juan);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/grades")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/students")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/grades", Json(GradeJson("99", "HACK101", 1)))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync($"/api/grades/{TestData.JuanWeb101GradeId}", Json(GradeJson("100", "WEB101", 1)))).StatusCode);

        var delete = await client.DeleteAsync($"/api/grades/{TestData.JuanWeb101GradeId}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal("application/problem+json", delete.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Faculty_CannotUseStudentEndpoint_Returns403()
    {
        var client = await ClientAsync(Faculty);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/student/grades")).StatusCode);
    }

    [Fact]
    public async Task Faculty_LookupEndpoints_Work()
    {
        var client = await ClientAsync(Faculty);

        var students = (await BodyAsync(await client.GetAsync("/api/students"))).EnumerateArray().ToList();
        Assert.Equal(3, students.Count); // inactive student excluded

        var subjects = (await BodyAsync(await client.GetAsync("/api/subjects"))).EnumerateArray()
            .Select(s => s.GetProperty("subjectCode").GetString()).ToList();
        Assert.Contains("WEB101", subjects);
        Assert.Contains("NET101", subjects);
    }

    [Fact]
    public async Task TamperedToken_Returns401()
    {
        var client = await ClientAsync(Juan);
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/student/grades")).StatusCode);
    }
}
