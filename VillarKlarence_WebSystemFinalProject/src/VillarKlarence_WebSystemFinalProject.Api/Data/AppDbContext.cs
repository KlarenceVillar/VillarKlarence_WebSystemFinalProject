using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VillarKlarence_WebSystemFinalProject.Api.Models;

namespace VillarKlarence_WebSystemFinalProject.Api.Data;

/// <summary>
/// One database (StudentGradeManagementDB) for both Identity and academic data.
/// The academic tables (Students, Faculty, StudentGrades) are created by the instructor's SQL script,
/// so they are mapped with ExcludeFromMigrations(): EF migrations only create the Identity tables
/// plus the AspNetUsers -> Students / Faculty foreign keys.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Faculty> Faculty => Set<Faculty>();
    public DbSet<StudentGrade> StudentGrades => Set<StudentGrade>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Student>(e =>
        {
            e.ToTable("Students", "dbo", t => t.ExcludeFromMigrations());
            e.HasKey(s => s.StudentId);
            e.Property(s => s.StudentId).ValueGeneratedOnAdd();
            e.Property(s => s.StudentNumber).HasMaxLength(30).IsRequired();
            e.Property(s => s.FirstName).HasMaxLength(100).IsRequired();
            e.Property(s => s.LastName).HasMaxLength(100).IsRequired();
            e.Property(s => s.Email).HasMaxLength(256).IsRequired();
            e.Property(s => s.IsActive).IsRequired();
            e.HasIndex(s => s.StudentNumber).IsUnique().HasDatabaseName("UQ_Students_StudentNumber");
            e.HasIndex(s => s.Email).IsUnique().HasDatabaseName("UQ_Students_Email");
        });

        builder.Entity<Faculty>(e =>
        {
            e.ToTable("Faculty", "dbo", t => t.ExcludeFromMigrations());
            e.HasKey(f => f.FacultyId);
            e.Property(f => f.FacultyId).ValueGeneratedOnAdd();
            e.Property(f => f.FacultyNumber).HasMaxLength(30).IsRequired();
            e.Property(f => f.FirstName).HasMaxLength(100).IsRequired();
            e.Property(f => f.LastName).HasMaxLength(100).IsRequired();
            e.Property(f => f.Email).HasMaxLength(256).IsRequired();
            e.Property(f => f.IsActive).IsRequired();
            e.HasIndex(f => f.FacultyNumber).IsUnique().HasDatabaseName("UQ_Faculty_FacultyNumber");
            e.HasIndex(f => f.Email).IsUnique().HasDatabaseName("UQ_Faculty_Email");
        });

        builder.Entity<StudentGrade>(e =>
        {
            e.ToTable("StudentGrades", "dbo", t => t.ExcludeFromMigrations());
            e.HasKey(g => g.GradeId);
            e.Property(g => g.GradeId).ValueGeneratedOnAdd();
            e.Property(g => g.SubjectCode).HasMaxLength(30).IsRequired();
            e.Property(g => g.SubjectName).HasMaxLength(200).IsRequired();
            e.Property(g => g.Remarks).HasMaxLength(30).IsRequired();
            e.Property(g => g.Semester).HasMaxLength(50).IsRequired();
            e.Property(g => g.SchoolYear).HasMaxLength(20).IsRequired();
            // Values are stored as UTC; mark them as UTC when read so JSON shows a trailing "Z".
            e.Property(g => g.DatePosted).HasColumnType("datetime2(0)")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            e.Property(g => g.DateModified).HasColumnType("datetime2(0)")
                .HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

            e.HasOne(g => g.Student).WithMany(s => s.Grades)
                .HasForeignKey(g => g.StudentId)
                .HasConstraintName("FK_StudentGrades_Students")
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(g => g.Faculty).WithMany(f => f.PostedGrades)
                .HasForeignKey(g => g.FacultyId)
                .HasConstraintName("FK_StudentGrades_Faculty")
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(g => new { g.StudentId, g.SubjectCode, g.Semester, g.SchoolYear })
                .IsUnique()
                .HasDatabaseName("UQ_StudentGrades_StudentSubjectTerm");
            e.HasIndex(g => g.FacultyId).HasDatabaseName("IX_StudentGrades_FacultyId");
        });

        builder.Entity<ApplicationUser>(e =>
        {
            // An account is either a Student or a Faculty member, never both.
            e.ToTable(t => t.HasCheckConstraint(
                "CK_AspNetUsers_SingleAcademicProfile",
                "[StudentId] IS NULL OR [FacultyId] IS NULL"));

            e.HasOne(u => u.Student).WithMany()
                .HasForeignKey(u => u.StudentId)
                .HasConstraintName("FK_AspNetUsers_Students_StudentId")
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(u => u.Faculty).WithMany()
                .HasForeignKey(u => u.FacultyId)
                .HasConstraintName("FK_AspNetUsers_Faculty_FacultyId")
                .OnDelete(DeleteBehavior.Restrict);

            // One login per student / faculty record (filtered unique index: NULLs allowed).
            e.HasIndex(u => u.StudentId).IsUnique();
            e.HasIndex(u => u.FacultyId).IsUnique();
        });
    }
}
