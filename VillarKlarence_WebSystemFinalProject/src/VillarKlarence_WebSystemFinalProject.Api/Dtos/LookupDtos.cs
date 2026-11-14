namespace VillarKlarence_WebSystemFinalProject.Api.Dtos;

/// <summary>GET /api/students item (student dropdown in the faculty grade form).</summary>
public sealed record StudentOptionResponse(int StudentId, string StudentNumber, string FullName, string Email);

/// <summary>GET /api/subjects item (subject autocomplete suggestions).</summary>
public sealed record SubjectOptionResponse(string SubjectCode, string SubjectName);
