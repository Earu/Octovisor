using Newtonsoft.Json;

namespace Octovisor.Server
{
    internal class OctovisorMessage
    {
        [JsonProperty(PropertyName="origin")]
        internal string OriginName { get; set; }

        [JsonProperty(PropertyName="target")]
        internal string TargetName { get; set; }

        [JsonProperty(PropertyName="msg_identifier")]
        internal string MessageIdentifier { get; set; }

        [JsonProperty(PropertyName="origin_ipv6")]
        internal string IPV6 { get; set; }

        [JsonProperty(PropertyName="origin_port")]
        internal int Port { get; set; }

        [JsonProperty(PropertyName="data")]
        internal string Data { get; set; }

        [JsonIgnore]
        internal bool IsValid { get; private set; } = true;

        internal static OctovisorMessage Deserialize(string json)
        {
            try
            {
                var msg = JsonConvert.DeserializeObject<OctovisorMessage>(json);
                msg.IsValid = true;

                return msg;
            }
            catch
            {
                return new OctovisorMessage
                {
                    OriginName        = "UNKNOWN_ORIGIN",
                    TargetName        = "UNKNOWN_TARGET",
                    MessageIdentifier = "UNKNOWN",
                    IPV6              = "0000:0000:0000:0000:0000:0000:0000:0000",
                    Port              = -1,
                    Data              = string.Empty,
                    IsValid           = false,
                };
            }
        }
    }
}
