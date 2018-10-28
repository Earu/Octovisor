using Newtonsoft.Json;
using System;

namespace Octovisor.Models
{
    public enum MessageStatus
    {
        OK                    = 0,
        ServerError           = 1,
        TargetError           = 2,
        NetworkError          = 3,
        MalformedMessageError = 4,
    }

    public class Message
    {
        [JsonProperty(PropertyName = "id")]
        public ulong ID { get; set; }

        [JsonProperty(PropertyName = "origin")]
        public string OriginName { get; set; }

        [JsonProperty(PropertyName = "target")]
        public string TargetName { get; set; }

        [JsonProperty(PropertyName = "identifier")]
        public string Identifier { get; set; }

        [JsonProperty(PropertyName = "data")]
        public string Data { get; set; }

        [JsonProperty(PropertyName = "status")]
        public MessageStatus Status { get; set; }

        public static Message Deserialize(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<Message>(json);
            }
            catch (Exception e)
            {
                return new Message
                {
                    ID = 0,
                    OriginName = "UNKNOWN_ORIGIN",
                    TargetName = "UNKNOWN_TARGET",
                    Identifier = "UNKNOWN",
                    Data = e.ToString(),
                    Status = MessageStatus.MalformedMessageError,
                };
            }
        }

        public string Serialize()
            => JsonConvert.SerializeObject(this);
    }
}
