using Newtonsoft.Json;
using Octovisor.Models;
using System.Threading.Tasks;

namespace Octovisor.Client
{
    public class OctoClient : BaseClient
    {
        private readonly string _ProcessName;

        public OctoClient(Config config) : base(config)
        {
            this._ProcessName = config.ProcessName;
        }

        public async Task WriteObjectAsync<T>(string identifier, string target, T obj) where T : class
        {
            string payload = JsonConvert.SerializeObject(obj);
            Message msg = this.MessageFactory.CreateMessage(identifier, this._ProcessName, target, payload);
            await this.SendAsync(msg);
        }

        public async Task WriteValueAsync<T>(string identifier, string target, T value) where T : struct
        {
            string data = value.ToString();
            Message msg = this.MessageFactory.CreateMessage(identifier, this._ProcessName, target, data);
            await this.SendAsync(msg);
        }
    }
}
