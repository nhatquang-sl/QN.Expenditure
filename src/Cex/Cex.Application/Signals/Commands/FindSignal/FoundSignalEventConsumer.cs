using Cex.Domain.Enums;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Cex.Application.Signals.Commands.FindSignal;

public partial class FoundSignalEventConsumer(ILogger<FoundSignalEventConsumer> logger) : IConsumer<FoundSignalEvent>
{
    public Task Consume(ConsumeContext<FoundSignalEvent> context)
    {
        LogFoundSignalEventReceivedSignaltypeForSymbol(context.Message.SignalType, context.Message.Symbol);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Found signal event received: {SignalType} for {Symbol}")]
    partial void LogFoundSignalEventReceivedSignaltypeForSymbol(SignalType signalType, string symbol);
}