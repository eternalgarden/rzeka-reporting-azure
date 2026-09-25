using System.Text.Json;

namespace RzekaReporting.Functions.Ingest;

internal static class EnvelopeValidator
{
    public const int MaxBodyBytes = 64 * 1024;

    static readonly int[] SupportedSchemaVersions = [1];

    public static EnvelopeResult Validate(ReadOnlySpan<byte> body)
    {
        if (body.Length > MaxBodyBytes)
            return EnvelopeResult.Reject($"Report exceeds {MaxBodyBytes} bytes.");

        JsonDocument document;
        try
        {
            var reader = new Utf8JsonReader(body);
            document = JsonDocument.ParseValue(ref reader);
        }
        catch (JsonException)
        {
            return EnvelopeResult.Reject("Body is not valid JSON.");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return EnvelopeResult.Reject("Body must be a JSON object.");

            if (
                !root.TryGetProperty("schemaVersion", out JsonElement version)
                // TryGetInt32 / TryGetGuid throw on the wrong kind instead of returning false.
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out int schemaVersion)
            )
                return EnvelopeResult.Reject("Missing or non-integer 'schemaVersion'.");

            if (!SupportedSchemaVersions.Contains(schemaVersion))
                return EnvelopeResult.Reject(
                    $"Unsupported schemaVersion {schemaVersion}. Supported: {string.Join(", ", SupportedSchemaVersions)}."
                );

            if (
                !root.TryGetProperty("reportId", out JsonElement id)
                || id.ValueKind != JsonValueKind.String
                || !id.TryGetGuid(out Guid reportId)
                || reportId == Guid.Empty
            )
                return EnvelopeResult.Reject("Missing or invalid 'reportId' (expected a GUID).");

            return EnvelopeResult.Accept(reportId);
        }
    }
}

internal readonly record struct EnvelopeResult(bool IsValid, Guid ReportId, string? Error)
{
    public static EnvelopeResult Accept(Guid reportId) => new(true, reportId, null);

    public static EnvelopeResult Reject(string error) => new(false, Guid.Empty, error);
}
