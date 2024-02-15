using MediatR;

namespace WebAPI.HostedServices
{
    public class SyncSpotOrdersService : IHostedService, IDisposable
    {
        private int executionCount = 0;
        private Timer? _timer = null;
        private readonly IMediator _mediator;

        public SyncSpotOrdersService(IMediator mediator)
        {
            _mediator = mediator;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {

            _timer = new Timer(DoWork, executionCount, TimeSpan.Zero,
                TimeSpan.FromSeconds(5));

            return Task.CompletedTask;

        }

        void DoWork(object? state)
        {
            var count = Interlocked.Increment(ref executionCount);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose() => _timer?.Dispose();
    }
}
