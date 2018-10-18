using Newtonsoft.Json;
using System;

namespace Octovisor.Server.Models
{
    internal enum ProcessMessageStatus
    {
        OK                    = 0,
        ServerError           = 1,
        TargetError           = 2,
        NetworkError          = 3,
        MalformedMessageError = 4,
    }

    internal class ProcessMessage
    {
        [JsonProperty(PropertyName="origin")]
        internal string OriginName { get; set; }

        [JsonProperty(PropertyName="target")]
        internal string TargetName { get; set; }
            
        [JsonProperty(PropertyName="msg_identifier")]
        internal string MessageIdentifier { get; set; }

        [JsonProperty(PropertyName="data")]
        internal string Data { get; set; }

        [JsonProperty(PropertyName="status")]
        internal ProcessMessageStatus Status { get; set; }

        internal static ProcessMessage Deserialize(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<ProcessMessage>(json);
            }
            catch(Exception e)
            {
                return new ProcessMessage
                {
                    OriginName        = "UNKNOWN_ORIGIN",
                    TargetName        = "UNKNOWN_TARGET",
                    MessageIdentifier = "UNKNOWN",
                    Data              = e.ToString(),
                    Status            = ProcessMessageStatus.MalformedMessageError,
                };
            }
        }

        internal string Serialize()
            => JsonConvert.SerializeObject(this);
    }
}
