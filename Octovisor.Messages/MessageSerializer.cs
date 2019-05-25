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
#if !DEBUG //this is because its easier to log json than bson bytes
            using (MemoryStream memory = new MemoryStream())
            using (BsonDataWriter writer = new BsonDataWriter(memory))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(writer, msg);
                return Convert.ToBase64String(memory.ToArray());
            }
#else
            return JsonConvert.SerializeObject(msg);
#endif
        }

        public static string SerializeData<T>(T data)
            => JsonConvert.SerializeObject(data);

        public static Message Deserialize(string base64String)
        {
#if !DEBUG
            byte[] data = Convert.FromBase64String(base64String);
            using (MemoryStream memory = new MemoryStream(data))
            using (BsonDataReader reader = new BsonDataReader(memory))
            {
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<Message>(reader);
            }
#else
            return JsonConvert.DeserializeObject<Message>(base64String);
#endif
        }

        public static T DeserializeData<T>(string data)
            => JsonConvert.DeserializeObject<T>(data);
    }
}
