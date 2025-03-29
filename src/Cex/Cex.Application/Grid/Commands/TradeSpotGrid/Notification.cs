using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;
using Refit;

namespace Cex.Application.Grid.Commands.TradeSpotGrid
{
    public record PlaceOrderNotification
        : INotification
    {
        public readonly Exception? Exception;
        public readonly SpotGrid Grid;
        public readonly PlaceOrderRequest? PlaceOrder;

        public PlaceOrderNotification(SpotGrid grid, PlaceOrderRequest placeOrder)
        {
            Grid = grid;
            PlaceOrder = placeOrder;
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
            var order = notification.PlaceOrder;
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

            if (order != null)
            {
                message =
                    $"Bot {grid.Id}: Place {order.Side.ToUpper()} {order.Size} {symbols[0]} for {order.Price} ({grid.Symbol})";
                logTrace.LogInformation(message, order);
                await notifier.NotifyInfo(message, order, cancellationToken);
            }
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

            if (order != null)
            {
                message =
                    $"Bot {grid.Id}: Fill {order.Side.ToUpper()} {order.Size} {symbols[0]} for {order.Price} ({grid.Symbol})";
                logTrace.LogInformation(message, order);
                await notifier.NotifyInfo(message, order, cancellationToken);
            }
        }
    }
}