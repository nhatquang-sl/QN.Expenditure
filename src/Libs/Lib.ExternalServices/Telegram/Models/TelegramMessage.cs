using System.Text.Json.Serialization;

namespace Lib.ExternalServices.Telegram.Models
{
    public class TelegramMessage
    {
        public TelegramMessage() { }
        public TelegramMessage(string chatId, string text)
        {
            this.ChatId = chatId;
            this.Text = text;
        }

        [JsonPropertyName("chat_id")]
        public string ChatId { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("parse_mode")]
        public string ParseMode => "HTML";
    }

    public class TelegramMessageResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public TelegramMessageResult Result { get; set; }
    }

    public class TelegramMessageResult
    {
        [JsonPropertyName("message_id")]
        public int MessageId { get; set; }

        [JsonPropertyName("sender_chat")]
        public TelegramMessageChat SenderChat { get; set; }

        [JsonPropertyName("chat")]
        public TelegramMessageChat Chat { get; set; }

        [JsonPropertyName("date")]
        public int Date { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("entities")]
        public TelegramMessageEntity[] Entities { get; set; }
    }

    public class TelegramMessageChat
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class TelegramMessageEntity
    {
        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("length")]
        public int Length { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }
}
