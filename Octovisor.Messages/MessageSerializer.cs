using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.IO;

namespace Octovisor.Messages
{
    public static class MessageSerializer
    {
        public static string Serialize(Message msg)
        {
            using (MemoryStream memory = new MemoryStream())
            using (BsonDataWriter writer = new BsonDataWriter(memory))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(writer, msg);
                return Convert.ToBase64String(memory.ToArray());
            }
        }

        public static string SerializeData<T>(T data)
            => JsonConvert.SerializeObject(data);

        public static Message Deserialize(string base64String)
        {
            byte[] data = Convert.FromBase64String(base64String);
            using (MemoryStream memory = new MemoryStream(data))
            using (BsonDataReader reader = new BsonDataReader(memory))
            {
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<Message>(reader);
            }
        }

        public static T DeserializeData<T>(string data)
            => JsonConvert.DeserializeObject<T>(data);
    }
}
