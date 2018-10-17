using Newtonsoft.Json;

namespace Octovisor.Client.Models
{
    public class MessageListener<T>
    {
        private RemoteProcess Process { get; set; }
        private string MessageIdentifier { get; set; }

        internal MessageListener(RemoteProcess process, string midentifier)
        {
            this.Process = process;
            this.MessageIdentifier = midentifier;
        }

        public bool Write(T data)
        {
            try
            {
                ProcessMessage msg = new ProcessMessage
                {
                    Data = JsonConvert.SerializeObject(data),
                    MessageIdentifier = this.MessageIdentifier,
                    OriginName = this.Process.Client.Config.ProcessName,
                    TargetName = this.Process.Name
                };

                this.Process.Client.Send(JsonConvert.SerializeObject(msg));

                return true;
            }
            catch
            {
                return false;
            }

        }

        public T Read()
        {
            try
            {
                string json = this.Process.Client.Receive();
                ProcessMessage msg = ProcessMessage.Deserialize(json);

                return JsonConvert.DeserializeObject<T>(msg.Data);
            }
            catch
            {
                return default(T);
            }
        }
    }
}
