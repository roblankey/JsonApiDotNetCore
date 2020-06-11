using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCore.Serialization.Server
{
    /// <summary>
    /// Deserializer used internally in JsonApiDotNetCore to deserialize requests.
    /// </summary>
    public interface IJsonApiDeserializer
    {
        /// <summary>
        /// Deserializes JSON in to a <see cref="Document"/> and constructs entities
        /// from <see cref="ExposableData{T}.Data"/>.
        /// </summary>
        /// <param name="body">The JSON to be deserialized</param>
        /// <returns>The entities constructed from the content</returns>
        object Deserialize(string body);
    }
}
