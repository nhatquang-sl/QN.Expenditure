using System.Text.Json;

namespace Lib.Application.Exceptions
{
    public class BadRequestException : Exception
    {
        public BadRequestException(string message) : base(JsonSerializer.Serialize(new { message })) { }
        public BadRequestException(object message) : base(JsonSerializer.Serialize(message)) { }
    }
}
