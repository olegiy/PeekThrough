using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GhostThrough
{
    internal static class JsonFileSerializer
    {
        private static readonly Encoding Utf8Encoding = new UTF8Encoding(false);

        public static T Deserialize<T>(string content) where T : class
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            var serializer = new DataContractJsonSerializer(typeof(T));

            using (var stream = new MemoryStream(Utf8Encoding.GetBytes(content)))
            {
                return serializer.ReadObject(stream) as T;
            }
        }

        public static string Serialize<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));

            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Utf8Encoding.GetString(stream.ToArray());
            }
        }
    }
}
