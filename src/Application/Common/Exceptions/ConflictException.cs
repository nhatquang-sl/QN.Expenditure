using System.Text.Json;

namespace Application.Common.Exceptions
{
    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
        public ConflictException(object message) : base(JsonSerializer.Serialize(new { message })) { }
    }
}
