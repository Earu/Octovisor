using Newtonsoft.Json;

namespace Octovisor.Messages
{
    public static class MessageSerializer
    {
        public static string Serialize(object obj)
            => JsonConvert.SerializeObject(obj);

        public static T Deserialize<T>(string json)
            => JsonConvert.DeserializeObject<T>(json);
    }
}
