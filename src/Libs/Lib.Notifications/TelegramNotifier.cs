using Lib.Application.Abstractions;

namespace Lib.Notifications
{
    public class TelegramNotifier : INotifier
    {
        public TelegramNotifier(HttpClient httpClient, string pathUrl)
        {
        }

        public void Notify(string title, string description, object data)
        {
            throw new NotImplementedException();
        }
    }
}