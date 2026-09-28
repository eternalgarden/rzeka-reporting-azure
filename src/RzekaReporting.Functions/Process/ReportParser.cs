using System.Text.Json;

namespace RzekaReporting.Functions.Process;

internal static class ReportParser
{
    static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        // Missing or null values for non-nullable properties fail parsing instead of
        // silently becoming null.
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    public static ParseResult Parse(ReadOnlySpan<byte> body)
    {
        ReportMessage? report;
        try
        {
            report = JsonSerializer.Deserialize<ReportMessage>(body, Options);
        }
        catch (JsonException ex)
        {
            return ParseResult.Malformed(ex.Message);
        }

        if (report is null)
            return ParseResult.Malformed("Body is JSON null.");
        if (report.SchemaVersion != 1)
            return ParseResult.Malformed($"Unsupported schemaVersion {report.SchemaVersion}.");
        if (report.ReportId == Guid.Empty)
            return ParseResult.Malformed("Empty reportId.");
        if (string.IsNullOrWhiteSpace(report.Failure.Spell.Title))
            return ParseResult.Malformed("Empty failure.spell.title.");
        if (string.IsNullOrWhiteSpace(report.Failure.Exception.Type))
            return ParseResult.Malformed("Empty failure.exception.type.");

        return ParseResult.Parsed(report);
    }
}

internal sealed record ParseResult(ReportMessage? Report, string? Error)
{
    public bool IsValid => Report is not null;

    public static ParseResult Parsed(ReportMessage report) => new(report, null);

    public static ParseResult Malformed(string error) => new(null, error);
}
