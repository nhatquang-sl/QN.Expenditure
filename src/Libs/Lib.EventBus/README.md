# Lib.EventBus

Infrastructure library for async message publishing and consuming via RabbitMQ, built on **MassTransit 8** (open-source).

> **Note:** MassTransit 9.x is a commercial product. This project pins to `8.5.10` for both `MassTransit` and `MassTransit.RabbitMQ`.

---

## Packages

| Package | Version |
|---|---|
| `MassTransit` | 8.5.10 |
| `MassTransit.RabbitMQ` | 8.5.10 |

Versions are centrally managed in `Directory.Packages.props` at the solution root.

---

## Architecture

```
Lib.Application
└── Abstractions/IEventBus.cs          ← publish abstraction (no MassTransit dependency)

Lib.EventBus
├── RabbitMqConfig.cs                  ← strongly-typed config bound from appsettings
├── RabbitMqEventBus.cs                ← IEventBus implementation using IPublishEndpoint
└── DependencyInjection.cs             ← AddLibEventBusServices() extension method
```

`IEventBus` lives in `Lib.Application` so domain/application layers can depend on the abstraction without pulling in MassTransit. `Lib.EventBus` is only referenced by the infrastructure/host layer.

---

## Configuration

Add a `RabbitMq` section to `appsettings.json`:

```json
{
  "RabbitMq": {
    "Host": "amqp://localhost:5672",
    "Username": "guest",
    "Password": "guest"
  }
}
```

`RabbitMqConfig` is bound from `configuration.GetSection("RabbitMq")` and registered as a singleton.

---

## Registration

Call `AddLibEventBusServices` in `Program.cs`. The optional `configureBus` action is the hook for registering consumers:

```csharp
builder.Services.AddLibEventBusServices(builder.Configuration,
    busConfig =>
    {
        busConfig.AddConsumer<FoundSignalEventConsumer>();
        // register additional consumers here
    });
```

Internally this call:

1. Binds `RabbitMqConfig` from configuration.
2. Calls `AddMassTransit`, sets **kebab-case endpoint name formatting**, and invokes your `configureBus` action.
3. Configures RabbitMQ transport using `UsingRabbitMq` with host/credentials from `RabbitMqConfig`.
4. Calls `cfg.ConfigureEndpoints(context)` — MassTransit automatically creates one queue per registered consumer, named after the consumer class in kebab-case (e.g. `FoundSignalEventConsumer` → `found-signal-event-consumer`).
5. Registers `RabbitMqEventBus` as `IEventBus` (transient).

---

## Publishing an event

Inject `IEventBus` and call `PublishAsync`. The message type is the routing key — any consumer registered for that type will receive it.

```csharp
public class FindSignalCommandHandler(IEventBus eventBus, ...)
{
    public async Task<Unit> Handle(FindSignalCommand request, CancellationToken cancellationToken)
    {
        // ... business logic ...

        await eventBus.PublishAsync(new FoundSignalEvent(signal), cancellationToken);

        return Unit.Value;
    }
}
```

`PublishAsync` wraps `IPublishEndpoint.Publish` and swallows exceptions with an error log, so a broker outage does not crash the caller.

---

## Consuming an event

Implement `IConsumer<TMessage>` from `MassTransit`:

```csharp
using MassTransit;

namespace Cex.Application.Signals.Commands.FindSignal;

public partial class FoundSignalEventConsumer(ILogger<FoundSignalEventConsumer> logger)
    : IConsumer<FoundSignalEvent>
{
    public Task Consume(ConsumeContext<FoundSignalEvent> context)
    {
        // context.Message is the strongly-typed FoundSignalEvent
        LogFoundSignalEventReceived(context.Message.SignalType, context.Message.Symbol);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Found signal event received: {SignalType} for {Symbol}")]
    partial void LogFoundSignalEventReceived(SignalType signalType, string symbol);
}
```

Then register it in `Program.cs` via the `configureBus` action (see Registration above).

**Consumer placement:** Consumer classes live inside the Application layer alongside the command/query they react to, not in `Lib.EventBus`. They import `MassTransit` directly because `Cex.Application` already references the `MassTransit` package for the `IConsumer<T>` interface.

---

## Message contracts

Message types are plain C# classes. Keep them in the Application layer next to the feature that produces them:

```csharp
// Cex.Application/Signals/Commands/FindSignal/FoundSignalEvent.cs
public class FoundSignalEvent(Signal signal)
{
    public int SignalId { get; set; } = signal.Id;
    public string Symbol { get; set; } = signal.Symbol;
    public string Interval { get; set; } = signal.Interval;
    public SignalType SignalType { get; set; } = signal.SignalType;
    public DateTime DetectedAt { get; set; } = signal.DetectedAt;
    // ...
}
```

---

## Endpoint naming

`SetKebabCaseEndpointNameFormatter()` is applied globally. MassTransit derives the queue name from the consumer class name:

| Consumer class | Queue name |
|---|---|
| `FoundSignalEventConsumer` | `found-signal-event-consumer` |
| `OrderCreatedConsumer` | `order-created-consumer` |

---

## Adding a new event

1. **Define the message contract** — plain class in the Application layer next to the producing command.
2. **Implement the consumer** — `IConsumer<YourEvent>` in the same feature folder.
3. **Register the consumer** — `busConfig.AddConsumer<YourConsumer>()` in `Program.cs`.
4. **Publish** — inject `IEventBus`, call `await eventBus.PublishAsync(new YourEvent(...), cancellationToken)`.
