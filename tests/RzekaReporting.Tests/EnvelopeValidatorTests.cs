using System.Text;
using RzekaReporting.Functions.Ingest;

namespace RzekaReporting.Tests;

public class EnvelopeValidatorTests
{
    static EnvelopeResult Validate(string json) => EnvelopeValidator.Validate(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Accepts_the_contract_sample()
    {
        byte[] sample = File.ReadAllBytes(Path.Combine("contract", "crash-report.v1.sample.json"));

        EnvelopeResult result = EnvelopeValidator.Validate(sample);

        Assert.True(result.IsValid, result.Error);
        Assert.Equal(Guid.Parse("3f2a9c1e-7b4d-4e8a-9f10-2c5d6e7f8a90"), result.ReportId);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{ "reportId": "3f2a9c1e-7b4d-4e8a-9f10-2c5d6e7f8a90" }""")]
    [InlineData("""{ "schemaVersion": "1", "reportId": "3f2a9c1e-7b4d-4e8a-9f10-2c5d6e7f8a90" }""")]
    [InlineData("""{ "schemaVersion": 2, "reportId": "3f2a9c1e-7b4d-4e8a-9f10-2c5d6e7f8a90" }""")]
    [InlineData("""{ "schemaVersion": 1 }""")]
    [InlineData("""{ "schemaVersion": 1, "reportId": 42 }""")]
    [InlineData("""{ "schemaVersion": 1, "reportId": "nope" }""")]
    [InlineData("""{ "schemaVersion": 1, "reportId": "00000000-0000-0000-0000-000000000000" }""")]
    public void Rejects_bad_envelopes(string json)
    {
        EnvelopeResult result = Validate(json);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Rejects_oversized_body()
    {
        var body = new byte[EnvelopeValidator.MaxBodyBytes + 1];

        Assert.False(EnvelopeValidator.Validate(body).IsValid);
    }
}
