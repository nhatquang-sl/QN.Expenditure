using Lib.Application.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Lib.EventBus;

public sealed class RabbitMqEventBus(IPublishEndpoint publisher, ILogger<RabbitMqEventBus> logger) : IEventBus
{
    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            await publisher.Publish(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish message of type {MessageType}", typeof(T).FullName);
        }
    }
}