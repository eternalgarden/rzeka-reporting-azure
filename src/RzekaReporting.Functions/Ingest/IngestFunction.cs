using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace RzekaReporting.Functions.Ingest;

public sealed class IngestFunction(ILogger<IngestFunction> logger)
{
    [Function("Ingest")]
    public async Task<IngestResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "reports")] HttpRequest request
    )
    {
        if (request.ContentLength > EnvelopeValidator.MaxBodyBytes)
            return IngestResult.Reject(StatusCodes.Status413PayloadTooLarge, "Report too large.");

        // ContentLength is absent for chunked uploads, so the read is capped as well.
        // TODO why do we add 1
        byte[] body = await ReadCappedAsync(request.Body, EnvelopeValidator.MaxBodyBytes + 1);

        // TODO what could make body fail the envelope validator?
        EnvelopeResult envelope = EnvelopeValidator.Validate(body);
        if (!envelope.IsValid)
        {
            logger.LogWarning("Rejected report: {Reason}", envelope.Error);
            return IngestResult.Reject(StatusCodes.Status400BadRequest, envelope.Error!);
        }

        // TODO add the check if logging is disabled to avoid potentially expensive action?
        logger.LogInformation("Accepted report {ReportId}", envelope.ReportId);
        return new IngestResult
        {
            QueueMessage = System.Text.Encoding.UTF8.GetString(body),
            HttpResponse = new AcceptedResult(),
        };
    }

    static async Task<byte[]> ReadCappedAsync(Stream stream, int maxBytes)
    {
        var buffer = new byte[maxBytes];
        int total = 0;
        int read;
        while (total < maxBytes && (read = await stream.ReadAsync(buffer.AsMemory(total))) > 0)
            total += read;
        return buffer[..total];
    }
}

public sealed class IngestResult
{
    // TODO so here we state our target service bus queue?
    // TODO what are the other allowed Connection types?
    [ServiceBusOutput("crash-reports", Connection = "ServiceBusConnection")]
    public string? QueueMessage { get; init; }

    [HttpResult]
    public required IActionResult HttpResponse { get; init; }

    // ObjectResult implements IActionResult
    public static IngestResult Reject(int statusCode, string error) =>
        new() { HttpResponse = new ObjectResult(new { error }) { StatusCode = statusCode } };
}
