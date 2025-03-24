using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Refit;

namespace Cex.Application.Grid.Commands.TradeSpotGrid
{
    public record PlaceOrderNotification
        : INotification
    {
        public readonly Exception? Exception;
        public readonly SpotGrid Grid;
        public readonly OrderRequest? Order;

        public PlaceOrderNotification(SpotGrid grid, OrderRequest order)
        {
            Grid = grid;
            Order = order;
        }

        public PlaceOrderNotification(SpotGrid grid, Exception exception)
        {
            Grid = grid;
            Exception = exception;
        }
    }

    public class PlaceOrderNotificationHandler(INotifier notifier, ILogTrace logTrace)
        : INotificationHandler<PlaceOrderNotification>
    {
        public async Task Handle(PlaceOrderNotification notification, CancellationToken cancellationToken)
        {
            var grid = notification.Grid;
            var order = notification.Order;
            const string quoteCurrency = "USDT";
            var symbols = new[] { grid.Symbol.Replace(quoteCurrency, ""), quoteCurrency };
            var message = "";

            if (notification.Exception != null)
            {
                message = notification.Exception.Message;
                if (notification.Exception is ApiException exception)
                {
                    message = exception.Content ?? exception.Message;
                }

                logTrace.LogError(message, notification.Exception);
                await notifier.NotifyError(message, notification.Exception, cancellationToken);
                return;
            }

            message =
                $"Bot {grid.Id}: {order.Side.ToUpper()} {order.Size} {symbols[0]} for {order.Price} ({grid.Symbol})";
            await notifier.NotifyInfo(message, order, cancellationToken);
            logTrace.LogInformation(message, order);
        }
    }

    public class FillOrderNotification : INotification
    {
        public readonly Exception? Exception;
        public readonly SpotGrid Grid;
        public readonly OrderDetails? Order;

        public FillOrderNotification(SpotGrid grid, OrderDetails order)
        {
            Grid = grid;
            Order = order;
        }

        public FillOrderNotification(SpotGrid grid, Exception exception)
        {
            Grid = grid;
            Exception = exception;
        }
    }

    public class FillOrderNotificationHandler(INotifier notifier, ILogTrace logTrace)
        : INotificationHandler<FillOrderNotification>
    {
        public async Task Handle(FillOrderNotification notification, CancellationToken cancellationToken)
        {
            var grid = notification.Grid;
            var order = notification.Order;
            const string quoteCurrency = "USDT";
            var symbols = new[] { grid.Symbol.Replace(quoteCurrency, ""), quoteCurrency };
            var message = "";

            if (notification.Exception != null)
            {
                message = notification.Exception.Message;
                if (notification.Exception is ApiException exception)
                {
                    message = exception.Content ?? exception.Message;
                }

                logTrace.LogError(message, notification.Exception);
                await notifier.NotifyError(message, notification.Exception, cancellationToken);
                return;
            }

            message =
                $"Bot {grid.Id}: {order.Side.ToUpper()} {order.Size} {symbols[0]} for {order.Price} ({grid.Symbol})";
            await notifier.NotifyInfo(message, order, cancellationToken);
            logTrace.LogInformation(message, order);
        }
    }
}