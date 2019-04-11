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
        private readonly string ProcessName;
        private readonly Dictionary<string, RemoteProcess> RemoteProcess;

        /// <summary>
        /// Instanciates a new OctoClient
        /// </summary>
        /// <param name="config">A configuration object</param>
        public OctoClient(Config config) : base(config)
        {
            this.ProcessName = config.ProcessName;
            this.RemoteProcess = new Dictionary<string, RemoteProcess>();
        }

        /// <summary>
        /// Gets the list of all currently available processes
        /// </summary>
        public List<RemoteProcess> AvailableProcesses { get => this.RemoteProcess.Select(x => x.Value).ToList(); }

        /// <summary>
        /// Gets an object representing a remote process
        /// </summary>
        /// <param name="processName">The name of the remote process</param>
        /// <returns>The object representing the remote process</returns>
        public RemoteProcess GetProcess(string processName)
        {
            RemoteProcess process = null;
            if (!this.RemoteProcess.ContainsKey(processName))
            {
                process = new RemoteProcess(this, processName);
                this.RemoteProcess.Add(processName, process);
            }
            else
            {
                process = this.RemoteProcess[processName];
            }

            return process;
        }

        internal void DisposeOf(string processName)
        {
            if (this.RemoteProcess.ContainsKey(processName))
                this.RemoteProcess.Remove(processName);
            else
                throw new UnknownRemoteProcessException();
        }

        internal async Task TransmitObjectAsync<T>(string identifier, string target, T obj) where T : class
        {
            string payload = JsonConvert.SerializeObject(obj);
            Message msg = this.MessageFactory.CreateMessage(identifier, this.ProcessName, target, payload);
            await this.SendAsync(msg);
        }

        internal async Task TransmitValueAsync<T>(string identifier, string target, T value) where T : struct
        {
            string data = value.ToString();
            Message msg = this.MessageFactory.CreateMessage(identifier, this.ProcessName, target, data);
            await this.SendAsync(msg);
        }
    }
}
