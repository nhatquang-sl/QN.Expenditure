using System.Text.Json;

namespace Application.Common.Exceptions
{
    public class BadRequestException : Exception
    {
        public BadRequestException(object message) : base(JsonSerializer.Serialize(new { message })) { }
    }
}
