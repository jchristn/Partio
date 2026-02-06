namespace Partio.Core.Serialization
{
    using SwiftStack.Serialization;

    /// <summary>
    /// Custom JSON serializer for Partio using SerializationHelper.
    /// PascalCase output, WriteIndented for pretty printing, no JsonPropertyName attributes.
    /// </summary>
    public class PartioSerializer : ISerializer
    {
        private readonly SerializationHelper.Serializer _Serializer;

        /// <summary>
        /// Initializes a new instance of the PartioSerializer.
        /// </summary>
        public PartioSerializer()
        {
            _Serializer = new SerializationHelper.Serializer();
        }

        /// <summary>
        /// Deserialize a JSON string to an object instance.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>Deserialized object instance.</returns>
        public T DeserializeJson<T>(string json)
        {
            return _Serializer.DeserializeJson<T>(json);
        }

        /// <summary>
        /// Deserialize bytes containing JSON to an object instance.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="bytes">Bytes containing JSON.</param>
        /// <returns>Deserialized object instance.</returns>
        public T DeserializeJson<T>(byte[] bytes)
        {
            return _Serializer.DeserializeJson<T>(bytes);
        }

        /// <summary>
        /// Serialize an object instance to a JSON string.
        /// </summary>
        /// <param name="obj">Object instance.</param>
        /// <param name="pretty">True to enable pretty-print (WriteIndented).</param>
        /// <returns>JSON string in PascalCase.</returns>
        public string SerializeJson(object obj, bool pretty)
        {
            return _Serializer.SerializeJson(obj, pretty);
        }
    }
}
