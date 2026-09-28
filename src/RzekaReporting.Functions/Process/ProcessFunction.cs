using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RzekaReporting.Functions.Database;

namespace RzekaReporting.Functions.Process;

/*
 * Notes.
 *
 * Functions runtime finds every method marked with [Function] (below), and when the message
 * arrives it creates this class and calls that method. "ProcessFunction" is simply our name,
 * any class name works, there's no requirements on that.
 *
 * We are also using here a "primary constructor" from C# 12, this class definition is a candy
 * for: public ProcessFunction(ILogger<ProcessFunction> logger) { _logger = logger; }
 * and 'logger' is usable everywhere in the class.
 *
 * The runtime passes the logger in as an injected dependency when it creates the class.
 *
 * Q. What other things if any can we expect for Functions runtime to inject for us?
 * A. Anything registered in Program.cs (builder.Services.Add…), plus a few built-ins such as
 *    ILogger<T> and IConfiguration (the app settings). Our next one: the database context,
 *    which we'll register in Program.cs and ask for here the same way as the logger.
 *
 */
public sealed class ProcessFunction(ILogger<ProcessFunction> logger, ReportStore store)
{
    const int PreviewLength = 100;

    // "Process" is our own name for this function, it isn't a reference to some system
    // within Azure's Functions. It is what you see in the portal (Functions » Process)
    // and in logs.
    [Function("Process")]
    public async Task Run(
        // `message`: the attribute says "call me when a message arrives on the queue
        // 'crash-reports'
        //  – using the connection string stored in the setting ServiceBusConnection,
        //  – and don't complete messages for me".
        //
        // The runtime then passes the received message in.
        //
        // `messageActions`: the runtime also passes this in; it's how we tell Service Bus
        // what to do with the message (complete it, dead-letter it…).
        [ServiceBusTrigger(
            queueName: "crash-reports",
            Connection = "ServiceBusConnection",
            AutoCompleteMessages = false
        )] ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions
    )
    {
        // The report JSON exactly as the client sent it, e.g. (shortened):
        //
        // {
        //   "schemaVersion": 1,
        //   "reportId": "3f2a9c1e-…",
        //   "occurredAt": "2026-09-25T16:10:09+00:00",
        //   "app": { "name": "crash-report-demo", "version": "1.0.0" },
        //   "failure": {
        //     "spell": { "title": "Looming of DamageTaken into HealthChanged", … },
        //     "exception": { "type": "System.InvalidOperationException", … }
        //   },
        //   "triggers": [ … ],
        //   "chain": { … },
        //   "breadcrumbs": [ … ]
        // }
        string body = message.Body.ToString();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                message: "Received message {MessageId} (delivery {DeliveryCount}, {Bytes} bytes): {Preview}",
                message.MessageId,
                message.DeliveryCount,
                // ToMemory(): Body holds raw bytes. It gives a view of those bytes without copying
                // them, and .Length is the byte count.
                // (ToString() above decodes them as text.)
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
            // So ServiceBusMessageActions is one of the classes i will need to study in depth?
            // A: Not in depth. Knowing its main methods is enough:
            //    Complete (done, delete it), DeadLetter (broken, park it),
            //    Abandon (give it back now so it's retried).
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
        logger.LogInformation(
            message: "Report {ReportId}: {Outcome}",
            report.ReportId,
            outcome
        );

        // So after this call message will get through peek-lock?
        // A: Yes. While we worked, the message was locked (hidden from other receivers) but
        //    still in the queue. Complete tells Service Bus "done, delete it". Without this call
        //    the lock would run out after 1 minute and the message would come back.
        await messageActions.CompleteMessageAsync(message: message);
    }
}
