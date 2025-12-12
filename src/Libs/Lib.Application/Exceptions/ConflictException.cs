using System.Text.Json;

namespace Lib.Application.Exceptions
{
    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(JsonSerializer.Serialize(new { message })) { }
        public ConflictException(object message) : base(JsonSerializer.Serialize(message)) { }
    }
}
