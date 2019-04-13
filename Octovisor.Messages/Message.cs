using Newtonsoft.Json;
using System;

namespace Octovisor.Messages
{
    public enum MessageType
    {
        Unknown = 0,
        Request = 1,
        Response = 2,
    }

    public enum MessageStatus
    {
        Unknown = 0,
        Success = 1,
        ServerError = 2,
        TargetError = 3,
        NetworkError = 4,
        MalformedMessageError = 5,
        ProcessNotFound = 6,
        UnknownMessageIdentifier = 7,
    }

    public class Message
    {
        [JsonProperty(PropertyName = "id")]
        public int ID { get; set; }

        [JsonProperty(PropertyName = "origin")]
        public string OriginName { get; set; }

        [JsonProperty(PropertyName = "target")]
        public string TargetName { get; set; }

        [JsonProperty(PropertyName = "identifier")]
        public string Identifier { get; set; }

        [JsonProperty(PropertyName = "data")]
        public string Data { get; set; }

        [JsonProperty(PropertyName = "error")]
        public string Error { get; set; }

        [JsonProperty(PropertyName = "type")]
        public MessageType Type { get; set; }

        [JsonProperty(PropertyName = "status")]
        public MessageStatus Status { get; set; }

        [JsonIgnore]
        public int Length { get => this.Data != null ? this.Data.Length : 0; }

        [JsonIgnore]
        public bool IsMalformed { get => this.Status == MessageStatus.MalformedMessageError; }

        [JsonIgnore]
        public bool HasException { get => this.Status != MessageStatus.Success && this.Status != MessageStatus.Unknown; }

        public T GetData<T>()
        {
            if (this.HasException)
                throw new Exception(this.Error);

            try
            {
                return MessageSerializer.Deserialize<T>(this.Data);
            }
            catch
            {
                return default;
            }
        }

        public static Message Deserialize(string json)
        {
            try
            {
                return MessageSerializer.Deserialize<Message>(json);
            }
            catch (Exception ex)
            {
                return new Message
                {
                    ID = -1,
                    OriginName = "UNKNOWN_ORIGIN",
                    TargetName = "UNKNOWN_TARGET",
                    Identifier = "UNKNOWN",
                    Data = null,
                    Error = ex.Message,
                    Type = MessageType.Unknown,
                    Status = MessageStatus.MalformedMessageError,
                };
            }
        }

        public string Serialize()
            => MessageSerializer.Serialize(this);
    }
}
