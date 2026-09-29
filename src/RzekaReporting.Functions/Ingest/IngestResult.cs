using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace RzekaReporting.Functions.Ingest;

// Ingest's two outputs at once: the HTTP response, and (unless null) the queue message.
public sealed class IngestResult
{
    [ServiceBusOutput("crash-reports", Connection = "ServiceBusConnection")]
    public string? QueueMessage { get; init; }

    [HttpResult]
    public required IActionResult HttpResponse { get; init; }

    public static IngestResult Reject(int statusCode, string error) =>
        new() { HttpResponse = new ObjectResult(new { error }) { StatusCode = statusCode } };
}
