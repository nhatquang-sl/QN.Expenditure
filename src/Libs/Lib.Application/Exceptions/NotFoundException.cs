using System.Text.Json;

namespace Lib.Application.Exceptions
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(JsonSerializer.Serialize(new { message })) { }
        public NotFoundException(object message) : base(JsonSerializer.Serialize(message)) { }
    }
}
