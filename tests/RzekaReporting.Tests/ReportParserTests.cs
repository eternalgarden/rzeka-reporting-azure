using System.Text;
using System.Text.Json.Nodes;
using RzekaReporting.Functions.Process;

namespace RzekaReporting.Tests;

public class ReportParserTests
{
    static readonly string Sample = File.ReadAllText(
        Path.Combine("contract", "crash-report.v1.sample.json")
    );

    static ParseResult Parse(string json) => ReportParser.Parse(Encoding.UTF8.GetBytes(json));

    // The sample with one change applied, e.g. a field removed or replaced.
    static string SampleWith(Action<JsonObject> change)
    {
        JsonObject root = JsonNode.Parse(Sample)!.AsObject();
        change(root);
        return root.ToJsonString();
    }

    [Fact]
    public void Contract_sample_parses()
    {
        ParseResult result = Parse(Sample);

        Assert.True(result.IsValid, result.Error);
        Assert.Equal(Guid.Parse("3f2a9c1e-7b4d-4e8a-9f10-2c5d6e7f8a90"), result.Report!.ReportId);
        Assert.Equal("sanctuary", result.Report.App.Name);
        Assert.Equal("Looming of DamageTaken into HealthState", result.Report.Failure.Spell.Title);
        Assert.Equal("System.InvalidOperationException", result.Report.Failure.Exception.Type);
    }

    [Fact]
    public void Unknown_fields_are_ignored()
    {
        // A newer client may send more than this backend knows about.
        Assert.True(Parse(SampleWith(r => r["somethingNew"] = 42)).IsValid);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Not_a_report_object_is_malformed(string json)
    {
        ParseResult result = Parse(json);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData("app")]
    [InlineData("failure")]
    public void Missing_required_parts_are_malformed(string field)
    {
        ParseResult result = Parse(SampleWith(r => r.Remove(field)));

        Assert.False(result.IsValid);
        Assert.Contains(field, result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Null_exception_type_is_malformed()
    {
        ParseResult result = Parse(
            SampleWith(r => r["failure"]!["exception"]!["type"] = null)
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Blank_exception_type_is_malformed()
    {
        ParseResult result = Parse(SampleWith(r => r["failure"]!["exception"]!["type"] = " "));

        Assert.Equal("Empty failure.exception.type.", result.Error);
    }

    [Fact]
    public void Unknown_schema_version_is_malformed()
    {
        ParseResult result = Parse(SampleWith(r => r["schemaVersion"] = 2));

        Assert.Equal("Unsupported schemaVersion 2.", result.Error);
    }

    [Fact]
    public void Empty_report_id_is_malformed()
    {
        ParseResult result = Parse(SampleWith(r => r["reportId"] = Guid.Empty.ToString()));

        Assert.Equal("Empty reportId.", result.Error);
    }

    [Fact]
    public void Optional_fields_may_be_null()
    {
        ParseResult result = Parse(
            SampleWith(r =>
            {
                r["failure"]!["spell"]!["ownerLabel"] = null;
                r["failure"]!["exception"]!["message"] = null;
                r["failure"]!["exception"]!["stackTrace"] = null;
            })
        );

        Assert.True(result.IsValid, result.Error);
    }
}
