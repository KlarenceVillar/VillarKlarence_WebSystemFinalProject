namespace VillarKlarence_WebSystemFinalProject.Web.Options;

/// <summary>
/// The ONE place the API address is configured (appsettings.json "ApiSettings:BaseUrl").
/// _Layout.cshtml renders it into window.APP_CONFIG for wwwroot/js/api.js.
/// </summary>
public sealed class ApiSettings
{
    public const string SectionName = "ApiSettings";

    public string BaseUrl { get; set; } = "https://localhost:7101";
}
