using Newtonsoft.Json;
using Octovisor.Client.Exceptions;
using Octovisor.Messages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Octovisor.Client
{
    public class OctoClient : BaseClient
    {
        private readonly string _ProcessName;
        private readonly Dictionary<string, RemoteProcess> _RemoteProcesses;

        /// <summary>
        /// Instanciates a new OctoClient
        /// </summary>
        /// <param name="config">A configuration object</param>
        public OctoClient(Config config) : base(config)
        {
            this._ProcessName = config.ProcessName;
            this._RemoteProcesses = new Dictionary<string, RemoteProcess>();
        }

        /// <summary>
        /// Gets the list of all currently available processes
        /// </summary>
        public List<RemoteProcess> AvailableProcesses { get => this._RemoteProcesses.Select(x => x.Value).ToList(); }

        /// <summary>
        /// Gets an object representing a remote process
        /// </summary>
        /// <param name="processname">The name of the remote process</param>
        /// <returns>The object representing the remote process</returns>
        public RemoteProcess UseProcess(string processname)
        {
            RemoteProcess process = null;
            if (!this._RemoteProcesses.ContainsKey(processname))
            {
                process = new RemoteProcess(this, processname);
                this._RemoteProcesses.Add(processname, process);
            }
            else
            {
                process = this._RemoteProcesses[processname];
            }

            return process;
        }

        internal void DisposeOf(string processname)
        {
            if (this._RemoteProcesses.ContainsKey(processname))
                this._RemoteProcesses.Remove(processname);
            else
                throw new UnknownRemoteProcessException();
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
