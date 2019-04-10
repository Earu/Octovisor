using Newtonsoft.Json;
using Octovisor.Messages;
using System.Threading.Tasks;

namespace Octovisor.Client
{
    public class OctoClient : BaseClient
    {
        private readonly string _ProcessName;

        /// <summary>
        /// Instanciates a new OctoClient
        /// </summary>
        /// <param name="config">A configuration object</param>
        public OctoClient(Config config) : base(config)
        {
            this._ProcessName = config.ProcessName;
        }

        /// <summary>
        /// Gets an object representing a remote process
        /// </summary>
        /// <param name="processname">The name of the remote process</param>
        /// <returns>The object representing the remote process</returns>
        public RemoteProcess Use(string processname)
        {
            RemoteProcess process = new RemoteProcess(this, processname);
            return process;
        }

        internal async Task TransmitObjectAsync<T>(string identifier, string target, T obj) where T : class
        {
            string payload = JsonConvert.SerializeObject(obj);
            Message msg = this.MessageFactory.CreateMessage(identifier, this._ProcessName, target, payload);
            await this.SendAsync(msg);
        }

        internal async Task TransmitValueAsync<T>(string identifier, string target, T value) where T : struct
        {
            string data = value.ToString();
            Message msg = this.MessageFactory.CreateMessage(identifier, this._ProcessName, target, data);
            await this.SendAsync(msg);
        }
    }
}
