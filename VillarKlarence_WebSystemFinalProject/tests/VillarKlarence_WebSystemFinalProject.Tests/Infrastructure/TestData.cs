using Microsoft.EntityFrameworkCore;
using VillarKlarence_WebSystemFinalProject.Api.Data;
using VillarKlarence_WebSystemFinalProject.Api.Models;

namespace VillarKlarence_WebSystemFinalProject.Tests.Infrastructure;

/// <summary>Academic seed data equivalent to Database/StudentGradeManagementDB.sql (plus one inactive student).</summary>
public static class TestData
{
    public const string DemoPassword = "Demo@12345";

    public const int JuanId = 1, MariaId = 2, PedroId = 3, InactiveStudentId = 4;
    public const int AndreaId = 1, MichaelId = 2;

    /// <summary>Grade ids from the seed: Juan 1-2, Maria 3-4, Pedro 5-6.</summary>
    public const int JuanWeb101GradeId = 1, MariaWeb101GradeId = 3;

    public static readonly DateTime SeedPostedUtc = new(2026, 8, 15, 8, 0, 0, DateTimeKind.Utc);

    public static AppDbContext CreateInMemoryContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? "grades-" + Guid.NewGuid())
            .Options;
        var db = new AppDbContext(options);
        SeedAcademicData(db);
        return db;
    }

    public static void SeedAcademicData(AppDbContext db)
    {
        if (db.Students.Any()) return;

        db.Students.AddRange(
            new Student { StudentId = JuanId, StudentNumber = "2026-0001", FirstName = "Juan", LastName = "Dela Cruz", Email = "juan.delacruz@example.com", IsActive = true },
            new Student { StudentId = MariaId, StudentNumber = "2026-0002", FirstName = "Maria", LastName = "Santos", Email = "maria.santos@example.com", IsActive = true },
            new Student { StudentId = PedroId, StudentNumber = "2026-0003", FirstName = "Pedro", LastName = "Reyes", Email = "pedro.reyes@example.com", IsActive = true },
            new Student { StudentId = InactiveStudentId, StudentNumber = "2026-0099", FirstName = "Ina", LastName = "Active", Email = "inactive@example.com", IsActive = false });

        db.Faculty.AddRange(
            new Faculty { FacultyId = AndreaId, FacultyNumber = "FAC-001", FirstName = "Andrea", LastName = "Garcia", Email = "andrea.garcia@example.com", IsActive = true },
            new Faculty { FacultyId = MichaelId, FacultyNumber = "FAC-002", FirstName = "Michael", LastName = "Ramos", Email = "michael.ramos@example.com", IsActive = true });

        var id = 1;
        foreach (var (studentId, grade, code, name, remarks) in new[]
                 {
                     (JuanId, 98, "WEB101", "Web Systems and Technologies", "CMS"),
                     (JuanId, 92, "NET101", "Networking Fundamentals", "SP"),
                     (MariaId, 87, "WEB101", "Web Systems and Technologies", "SAP"),
                     (MariaId, 82, "NET101", "Networking Fundamentals", "GP"),
                     (PedroId, 76, "WEB101", "Web Systems and Technologies", "EP"),
                     (PedroId, 68, "NET101", "Networking Fundamentals", "BEP/FAILED")
                 })
        {
            db.StudentGrades.Add(new StudentGrade
            {
                GradeId = id++,
                StudentId = studentId,
                SubjectCode = code,
                SubjectName = name,
                FacultyId = AndreaId,
                Grade = grade,
                Remarks = remarks,
                Semester = "1st Semester",
                SchoolYear = "2026-2027",
                DatePosted = SeedPostedUtc
            });
        }

        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}
