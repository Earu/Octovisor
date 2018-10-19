using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Octovisor.Client.Models
{
    public class MessageListener<T>
    {
        private static ulong CurrentMsgId = 0;

        private RemoteProcess Process { get; set; }
        private string MessageIdentifier { get; set; }

        internal MessageListener(RemoteProcess process, string midentifier)
        {
            this.Process = process;
            this.MessageIdentifier = midentifier;
        }

        public async Task<T> Listen()
        {
            ProcessMessage msg = new ProcessMessage
            {
                ID = ++CurrentMsgId,
                MessageIdentifier = this.MessageIdentifier,
                OriginName = this.Process.Client.Config.ProcessName,
                TargetName = this.Process.Name,
                Data = null,
                Status = ProcessMessageStatus.OK
            };
            this.Process.Client.Send(msg);
            TaskCompletionSource<ProcessMessage> tcs = 
                this.Process.Client.GetTCS(this.Process.Name,msg.ID);

            try
            {
                msg = await tcs.Task;
                return JsonConvert.DeserializeObject<T>(msg.Data);
            }
            catch
            {
                return default(T);
            }
        }

    }
}
