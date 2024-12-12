using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lib.Application.Exceptions
{
    public class UnprocessableEntityException(UnprocessableEntity[] errors)
        : Exception(JsonSerializer.Serialize(errors));

    public class UnprocessableEntity
    {
        [JsonPropertyName("name")] public string Name { get; set; }

        [JsonPropertyName("errors")] public string[] Errors { get; set; } = [];
    }
}