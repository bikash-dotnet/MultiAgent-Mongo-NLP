namespace Gateway.Reports;

public sealed class ReportsOptions
{
    public const string SectionName = "Reports";

    public bool DemoFallbackEnabled { get; set; } = true;

    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string User { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string From { get; set; } = "no-reply@enterprise.com";
}
