using Newtonsoft.Json;
using Octovisor.Client.Exceptions;
using Octovisor.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Octovisor.Client
{
    /// <summary>
    /// The octovisor client
    /// </summary>
    public class OctoClient : BaseClient
    {
        /// <summary>
        /// Fired whenever a new process becomes available
        /// </summary>
        public event Action<RemoteProcess> ProcessRegistered;

        /// <summary>
        /// Fired whenever a process goes down
        /// </summary>
        public event Action<RemoteProcess> ProcessEnded;

        private readonly string ProcessName;
        private readonly Dictionary<string, RemoteProcess> Processes;
        private readonly List<string> UsedIdentifiers;

        /// <summary>
        /// Instanciates a new OctoClient
        /// </summary>
        /// <param name="config">A configuration object</param>
        public OctoClient(Config config) : base(config)
        {
            this.ProcessName = config.ProcessName;
            this.Processes = new Dictionary<string, RemoteProcess>();
            this.UsedIdentifiers = new List<string>();

            this.ProcessUpdate += this.OnProcessUpdate;
        }

        private void OnProcessUpdate(ProcessUpdateData updateData, bool isRegisterUpdate)
        {
            if (!updateData.Accepted) return;

            if (isRegisterUpdate)
            {
                RemoteProcess proc = new RemoteProcess(this, updateData.Name);
                this.Processes.Add(updateData.Name, proc);
                this.ProcessRegistered?.Invoke(proc);
            }
            else
            {
                RemoteProcess proc = this.Processes[updateData.Name];
                this.Processes.Remove(updateData.Name);
                this.ProcessEnded?.Invoke(proc);
            }
        }

        /// <summary>
        /// Registers an event handler that will be fired whenever the transmission for the specified identifier is received
        /// </summary>
        /// <typeparam name="T">The data type we expect to receive</typeparam>
        /// <typeparam name="TResult">The data type we are sending back</typeparam>
        /// <param name="identifier">The transmission identifier</param>
        /// <param name="handler">The handler to be called when receiving a transmission</param>
        public void OnTransmission<T, TResult>(string identifier, Func<RemoteProcess, T, TResult> handler)
        {
            if (this.UsedIdentifiers.Contains(identifier))
                throw new Exception($"non-unique transmission handler for \'{identifier}\'");

            this.MessageReceived += msg =>
            {
                if (!msg.Identifier.Equals(identifier))
                    return null;

                T data = msg.GetData<T>();
                RemoteProcess proc = this.GetProcess(msg.OriginName);
                TResult result = handler(proc, data);

                return JsonConvert.SerializeObject(result);
            };

            this.UsedIdentifiers.Add(identifier);
        }

        /// <summary>
        /// Registers an event handler that will be fired whenever the transmission for the specified identifier is received
        /// </summary>
        /// <typeparam name="T">The data type we expect to receive</typeparam>
        /// <param name="identifier">The transmission identifier</param>
        /// <param name="handler">The handler to be called when receiving a transmission</param>
        public void OnTransmission<T>(string identifier, Action<RemoteProcess, T> handler)
        {
            if (this.UsedIdentifiers.Contains(identifier))
                throw new Exception($"non-unique transmission handler for \'{identifier}\'");

            this.MessageReceived += msg =>
            {
                if (!msg.Identifier.Equals(identifier))
                    return null;

                T data = msg.GetData<T>();
                RemoteProcess proc = this.GetProcess(msg.OriginName);
                handler(proc, data);

                return "uhh";
            };

            this.UsedIdentifiers.Add(identifier);
        }

        /// <summary>
        /// Gets the list of all currently available processes
        /// </summary>
        public List<RemoteProcess> AvailableProcesses { get => this.Processes.Select(x => x.Value).ToList(); }

        /// <summary>
        /// Gets whether the specified name corresponds to a valid remote process or not
        /// </summary>
        /// <param name="processName">The name of the remote process to check for</param>
        public bool IsValidProcess(string processName)
            => this.Processes.ContainsKey(processName);

        /// <summary>
        /// Gets an object representing a remote process
        /// </summary>
        /// <param name="processName">The name of the remote process</param>
        /// <returns>The object representing the remote process</returns>
        public RemoteProcess GetProcess(string processName)
        {
            if (this.Processes.ContainsKey(processName))
                return this.Processes[processName];
            else
                throw new UnknownRemoteProcessException(processName);
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
