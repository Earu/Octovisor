using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

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

        [JsonProperty(PropertyName = "compressed_data")]
        public byte[] CompressedData { get; set; } = new byte[0];

        [JsonProperty(PropertyName = "error")]
        public string Error { get; set; }

        [JsonProperty(PropertyName = "type")]
        public MessageType Type { get; set; }

        [JsonProperty(PropertyName = "status")]
        public MessageStatus Status { get; set; }

        [JsonProperty(PropertyName = "compressed")]
        public bool IsCompressed { get; set; } = false;

        [JsonIgnore]
        public int DataLength { get => this.Data != null ? this.Data.Length : 0; }

        [JsonIgnore]
        public bool IsMalformed { get => this.Status == MessageStatus.MalformedMessageError; }

        [JsonIgnore]
        public bool HasException { get => this.Status != MessageStatus.Success && this.Status != MessageStatus.Unknown; }

        public void CompressData()
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(this.Data);
            using (MemoryStream memory = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(memory, CompressionMode.Compress, true))
                    gzip.Write(dataBytes, 0, dataBytes.Length);

                this.CompressedData = memory.ToArray();
                this.Data = null;
            }

            this.IsCompressed = true;
        }

        public void DecompressData()
        {
            byte[] dataBytes = this.CompressedData;
            using (MemoryStream memory = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(memory, CompressionMode.Decompress, true))
                    gzip.Read(dataBytes, 0, dataBytes.Length);

                this.Data = Encoding.UTF8.GetString(memory.ToArray());
                Array.Clear(this.CompressedData, 0, this.CompressedData.Length);
            }

            this.IsCompressed = false;
        }

        public T GetData<T>()
        {
            if (this.HasException)
                throw new Exception(this.Error);

            try
            {
                if (this.IsCompressed)
                    this.DecompressData();

                return MessageSerializer.DeserializeData<T>(this.Data);
            }
            catch
            {
                return default(T);
            }
        }

        public static Message Deserialize(string json)
        {
            try
            {
                return MessageSerializer.Deserialize(json);
            }
            catch (Exception ex)
            {
                return new Message
                {
                    ID = -1,
                    OriginName = "UNKNOWN_ORIGIN",
                    TargetName = "UNKNOWN_TARGET",
                    Identifier = "UNKNOWN",
                    Data = json,
                    Error = ex.Message,
                    Type = MessageType.Unknown,
                    Status = MessageStatus.MalformedMessageError,
                };
            }
        }

        public string Serialize()
            => MessageSerializer.Serialize(this);

        // for debug purposes
        public override string ToString()
        {
            return $@"Message [ 
    ID: {this.ID}, 
    Identifier: {this.Identifier}, 
    Origin: {this.OriginName}, 
    Target: {this.TargetName},
    Data: {(string.IsNullOrWhiteSpace(this.Data) ? "none" : this.Data)},
    Error: {(string.IsNullOrWhiteSpace(this.Error) ? "none" : this.Error)},
    Type: {this.Type},
    Status: {this.Status},
    IsCompressed: {this.IsCompressed},
            ]";
        }
    }
}
