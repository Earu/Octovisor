﻿using Newtonsoft.Json;
using System;

namespace Octovisor.Client.Models
{
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

        [JsonIgnore]
        internal bool IsValid { get; private set; } = true;

        internal static ProcessMessage Deserialize(string json)
        {
            try
            {
                var msg = JsonConvert.DeserializeObject<ProcessMessage>(json);
                msg.IsValid = true;

                return msg;
            }
            catch(Exception e)
            {
                return new ProcessMessage {
                    OriginName        = "UNKNOWN_ORIGIN",
                    TargetName        = "UNKNOWN_TARGET",
                    MessageIdentifier = "UNKNOWN",
                    Data              = e.ToString(),
                    IsValid           = false,
                };
            }
        }

        internal string Serialize()
            => JsonConvert.SerializeObject(this);
    }
}
