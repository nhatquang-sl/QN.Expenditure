
using Cex.Application.Candle.Commands.SyncCandles;
using Cex.Application.Common.Configs;
using Cex.Application.Config.Commands.RefreshUserToken;
using Cex.Application.Config.Queries.GetUserToken;
using Cex.Infrastructure;
using Lib.ExternalServices.Cex;
using MediatR;
using Microsoft.Extensions.Options;
using Serilog;
using SocketIO.Core;
using SocketIOClient;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebAPI.HostedServices
{
    public class ListenCexWebsocketService(IConfiguration configuration, IOptions<CexConfig> cexConfig) : BackgroundService
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly CexConfig _cexConfig = cexConfig.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var services = new ServiceCollection();
            services.AddSingleton(_configuration);
            services.AddCexInfrastructureServices(_configuration);
            var serviceProvider = services.BuildServiceProvider();

            await Task.Factory.StartNew(async () =>
            {
                var logger = new LoggerConfiguration().ReadFrom.Configuration(_configuration).CreateLogger();
                logger.Information("Start ListenCexWebsocketService started at {startAt}", DateTime.UtcNow);

                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    var (accessToken, refreshToken) = await mediator.Send(new GetUserTokenQuery());

                    (accessToken, refreshToken) = await mediator.Send(new RefreshUserTokenCommand(accessToken, refreshToken));
                    var cexUser = CexUtils.DecodeToken(accessToken);

                    var socket = new SocketIOClient.SocketIO(_cexConfig.SocketEndpoint, new SocketIOOptions
                    {
                        // Configuring transports and reconnection options
                        Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
                        ReconnectionDelay = 500,
                        ReconnectionDelayMax = 10000,
                        Query = new Dictionary<string, string>
                        {
                            { "uid", cexUser.UserId.ToString() },
                            { "ssid", refreshToken }
                        },
                        EIO = EngineIO.V3

                    });

                    // Handle the 'connect' event
                    socket.OnConnected += (sender, e) =>
                    {
                        Console.WriteLine("Connected to the server");
                        socket.EmitAsync(_cexConfig.SocketCexPriceSubscribe); // Send a message to the server
                    };

                    // Handle connection errors and other socket events
                    socket.OnError += (sender, e) => Console.WriteLine($"Connection error: {e}");
                    socket.OnReconnectAttempt += (sender, e) => Console.WriteLine($"Reconnecting attempt {e}");
                    socket.OnReconnected += (sender, e) => Console.WriteLine("Reconnected to the server");
                    socket.OnReconnectError += (sender, e) => Console.WriteLine($"Reconnection error: {e}");
                    socket.OnReconnectFailed += (sender, e) => Console.WriteLine("Reconnection failed");
                    socket.On(_cexConfig.SocketCexPrice, async (res) =>
                    {
                        try
                        {
                            var serviceProvider = services.BuildServiceProvider();
                            using (var scope = serviceProvider.CreateScope())
                            {
                                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                                var cexPrice = res.GetValue<CexPrice>(0);
                                Console.WriteLine(JsonSerializer.Serialize(cexPrice));

                                // TRADE session
                                if (cexPrice.IsBetSession && cexPrice.Order == 25)
                                {
                                    await mediator.Send(new SyncCandlesCommand(refreshToken));
                                }

                                // WAIT session
                                if (!cexPrice.IsBetSession && cexPrice.Order == 25)
                                {
                                    (accessToken, refreshToken) = await mediator.Send(new RefreshUserTokenCommand(accessToken, refreshToken));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                        }
                    });

                    // Connect to the server
                    await socket.ConnectAsync();
                }
                catch (Exception exception)
                {
                    var ex = exception;
                    while (ex != null)
                    {
                        logger.Error("ListenCexWebsocketService - {message}\n{StackTrace}", ex.Message, ex.StackTrace);
                        ex = ex.InnerException;
                    }
                }
            });

        }
    }

    public class CexPrice
    {
        [JsonPropertyName("lowPrice")]
        public decimal LowPrice { get; set; }

        [JsonPropertyName("session")]
        public long Session { get; set; }

        [JsonPropertyName("isBetSession")]
        public bool IsBetSession { get; set; }

        [JsonPropertyName("highPrice")]
        public decimal HighPrice { get; set; }

        [JsonPropertyName("openPrice")]
        public decimal OpenPrice { get; set; }

        [JsonPropertyName("closePrice")]
        public decimal ClosePrice { get; set; }

        [JsonPropertyName("baseVolume")]
        public decimal BaseVolume { get; set; }

        [JsonPropertyName("createDateTime")]
        public long CreateDateTime { get; set; }

        [JsonPropertyName("order")]
        public int Order { get; set; }
    }
}
