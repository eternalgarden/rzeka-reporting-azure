using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RzekaReporting.Functions.Database;
using RzekaReporting.Functions.Database.Entities;

namespace RzekaReporting.Functions.Process;


public sealed class ProcessFunction(ILogger<ProcessFunction> logger, ReportStore store)
{
    const int PreviewLength = 100;

    [Function("Process")]
    public async Task Run(

        [ServiceBusTrigger(
            queueName: "crash-reports",
            Connection = "ServiceBusConnection",
            AutoCompleteMessages = false
        )] ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions
    )
    {
        string body = message.Body.ToString();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                message: "Received message {MessageId} (delivery {DeliveryCount}, {Bytes} bytes): {Preview}",
                message.MessageId,
                message.DeliveryCount,
                message.Body.ToMemory().Length,
                body[..Math.Min(PreviewLength, body.Length)]
            );
        }

        ParseResult parsed = ReportParser.Parse(body: message.Body.ToMemory().Span);
        if (!parsed.IsValid)
        {
            // Retrying can't fix a malformed message, so it goes straight to the dead-letter queue.
            logger.LogWarning(
                message: "Dead-lettering {MessageId}: {Reason}",
                message.MessageId,
                parsed.Error
            );
            await messageActions.DeadLetterMessageAsync(
                message: message,
                deadLetterReason: "MalformedReport",
                deadLetterErrorDescription: parsed.Error
            );
            return;
        }

        ReportMessage report = parsed.Report!;
        ReportSpell spell = report.Failure.Spell;
        ReportException exception = report.Failure.Exception;
        string canonical = Fingerprint.Canonical(
            school: spell.School,
            spellTitle: spell.Title,
            ownerType: spell.OwnerType,
            exceptionType: exception.Type,
            stackTrace: exception.StackTrace
        );
        string fingerprint = Fingerprint.Compute(
            school: spell.School,
            spellTitle: spell.Title,
            ownerType: spell.OwnerType,
            exceptionType: exception.Type,
            stackTrace: exception.StackTrace
        );

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                message: "Report {ReportId}: fingerprint {Fingerprint} from {Canonical}",
                report.ReportId,
                fingerprint,
                canonical
            );
        }

        SaveOutcome outcome = await store.SaveAsync(
            report: new CrashReport
            {
                ReportId = report.ReportId,
                Fingerprint = fingerprint,
                OccurredAt = report.OccurredAt.UtcDateTime,
                ReceivedAt = DateTime.UtcNow,
                AppName = report.App.Name,
                AppVersion = report.App.Version,
                ExceptionMessage = exception.Message,
                StackTrace = exception.StackTrace,
                Payload = body,
            },
            canonical: canonical
        );

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                message: "Report {ReportId}: {Outcome}",
                report.ReportId,
                outcome
            );
        }

        await messageActions.CompleteMessageAsync(message: message);
    }
}
