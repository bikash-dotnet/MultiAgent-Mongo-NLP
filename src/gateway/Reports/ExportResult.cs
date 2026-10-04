namespace Gateway.Reports;

public sealed record ExportResult(string Format, byte[] Content, string ContentType, string FileName);
