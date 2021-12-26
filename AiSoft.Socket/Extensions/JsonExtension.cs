using System;
using System.IO;
using ProtoBuf;

namespace AiSoft.Socket.Extensions
{
    internal static class JsonExtension
    {
        /// <summary>
        /// JSON序列化
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static byte[] JsonPBSerialize<T>(this T obj)
        {
            using (var memoryStream = new MemoryStream())
            {
                Serializer.Serialize<T>((Stream)memoryStream, obj);
                var buffer = new byte[memoryStream.Length];
                memoryStream.Position = 0L;
                memoryStream.Read(buffer, 0, buffer.Length);
                return buffer;
            }
        }

        /// <summary>
        /// JSON反序列化
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static T JsonPBDeserialize<T>(this byte[] data)
        {
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.Write(data, 0, data.Length);
                memoryStream.Position = 0L;
                return Serializer.Deserialize<T>((Stream)memoryStream);
            }
        }
    }
}