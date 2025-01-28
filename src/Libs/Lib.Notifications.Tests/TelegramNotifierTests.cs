using System.Reflection;
using Lib.Application.Abstractions;

namespace Lib.Notifications.Tests
{
    public class TelegramNotifierTests : DependencyInjectionFixture
    {
        private readonly INotifier _notifier;

        public TelegramNotifierTests()
        {
            _notifier = GetService<INotifier>();
        }

        [Fact]
        public async void NotifyInfo_Success()
        {
            await _notifier.NotifyInfo("test message", "desc");
        }

        [Fact]
        public async void NotifyError_Success()
        {
            try
            {
                var x = 0;
                var y = 10 / x;
            }
            catch (Exception ex)
            {
                await _notifier.NotifyError($"{DateTime.UtcNow}: {Assembly.GetExecutingAssembly().GetName().Name}", ex);
            }
        }
    }
}