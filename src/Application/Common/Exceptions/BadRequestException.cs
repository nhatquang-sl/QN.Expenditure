using System.Text.Json;

namespace Application.Common.Exceptions
{
    public class BadRequestException : Exception
    {
        public BadRequestException(string message) : base(JsonSerializer.Serialize(new { message })) { }
        public BadRequestException(object message) : base(JsonSerializer.Serialize(message)) { }
    }
}
