using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace RzekaReporting.Functions.Process;

public sealed class ProcessFunction(ILogger<ProcessFunction> logger)
{
    [Function("Process")]
    public void Run(
        [ServiceBusTrigger("crash-reports", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message
    )
    {
        logger.LogInformation(
            "Processing message {MessageId} (delivery {DeliveryCount}, {Bytes} bytes)",
            message.MessageId,
            message.DeliveryCount,
            message.Body.ToMemory().Length
        );
    }
}
