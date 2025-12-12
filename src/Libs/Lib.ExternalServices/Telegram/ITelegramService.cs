using Lib.ExternalServices.Telegram.Models;
using Refit;

namespace Lib.ExternalServices.Telegram
{
    public interface ITelegramService
    {
        [Post("/{botToken}/sendMessage")]
        Task<TelegramMessageResponse> SendMessage(string botToken, TelegramMessage request);

        [Post("/{botToken}/sendMessage")]
        Task<TelegramMessageResponse> SendTopicMessage(string botToken, TelegramTopicMessage request);
    }
}