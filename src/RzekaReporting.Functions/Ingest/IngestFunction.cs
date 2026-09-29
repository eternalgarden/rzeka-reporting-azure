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

        byte[] body = await ReadCappedAsync(request.Body, EnvelopeValidator.MaxBodyBytes + 1);

        EnvelopeResult envelope = EnvelopeValidator.Validate(body);
        if (!envelope.IsValid)
        {
            logger.LogWarning("Rejected report: {Reason}", envelope.Error);
            return IngestResult.Reject(StatusCodes.Status400BadRequest, envelope.Error!);
        }

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