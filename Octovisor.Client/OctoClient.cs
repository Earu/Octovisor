using Octovisor.Client.Exceptions;
using Octovisor.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        /// <summary>
        /// Fired whenever we receive the full list of available processes
        /// </summary>
        public event Action<List<RemoteProcess>> ProcessesFetched;

        private readonly Dictionary<string, RemoteProcess> Processes;
        private readonly Dictionary<string, Func<Message, string>> TransmissionHandlers;
        private readonly Dictionary<int, TaskCompletionSource<string>> TransmissionTCSs;
        private readonly int Timeout;

        /// <summary>
        /// Instanciates a new OctoClient based on the config object passsed
        /// </summary>
        /// <param name="config">The config object</param>
        public OctoClient(OctoConfig config) : base(config)
        {
            this.ProcessName = config.ProcessName;
            this.ServerAddress = config.Address;
            this.ServerPort = config.Port;
            this.Processes = new Dictionary<string, RemoteProcess>();
            this.TransmissionHandlers = new Dictionary<string, Func<Message, string>>();
            this.TransmissionTCSs = new Dictionary<int, TaskCompletionSource<string>>();
            this.Timeout = config.Timeout;

            this.ProcessUpdate += this.OnProcessUpdate;
            this.ProcessesInfoReceived += this.OnProcessesInfoReceived;
            this.MessageRequestReceived += this.OnMessageRequestReceived;
            this.MessageResponseReceived += this.OnMessageResponseReceived;
            this.Disconnected += this.OnClientDisconnected;
        }

        private Task OnClientDisconnected()
        {
            this.Processes.Clear();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets whether or not this client instance is connected to the Octovisor server
        /// </summary>
        public bool IsConnected => this.IsRegisteredInternal; 

        /// <summary>
        /// Gets the list of all currently available processes
        /// </summary>
        public List<RemoteProcess> AvailableProcesses => this.Processes.Select(x => x.Value).ToList(); 

        /// <summary>
        /// Gets the amount of available registered processes
        /// </summary>
        public int AvailableProcessesCount  => this.Processes.Count; 

        /// <summary>
        /// The name under which the client is registered on the Server
        /// </summary>
        public string ProcessName { get; }

        /// <summary>
        /// The address of the Octovisor server this client is targetting
        /// </summary>
        public string ServerAddress { get; }

        /// <summary>
        /// The port of the Octovisor server this client is targetting
        /// </summary>
        public int ServerPort { get; }

        private void OnMessageResponseReceived(Message msg)
        {
            this.LogEvent(LogSeverity.Debug, $"Received response message | ID: {msg.Identifier}");
            if (!this.TransmissionTCSs.ContainsKey(msg.ID)) return;

            TaskCompletionSource<string> tcs = this.TransmissionTCSs[msg.ID];
            if (!tcs.Task.IsCompleted)
            {
                if (msg.HasException)
                    tcs.SetException(new Exception(msg.Error));
                else
                    tcs.SetResult(msg.Data);
            }
        }

        private string OnMessageRequestReceived(Message msg)
        {
            this.LogEvent(LogSeverity.Debug, $"Received request message | ID: {msg.Identifier}");
            if (msg.HasException) return null;

            if (this.TransmissionHandlers.ContainsKey(msg.Identifier))
                return this.TransmissionHandlers[msg.Identifier](msg);

            return null;
        }

        private void OnProcessesInfoReceived(List<RemoteProcessData> data)
        {
            data = data ?? new List<RemoteProcessData>(); //nothing ?
            this.Processes.Clear();
            foreach (RemoteProcessData procData in data)
                this.Processes.Add(procData.Name, new RemoteProcess(this, procData.Name));
            this.ProcessesFetched?.Invoke(this.AvailableProcesses);
        }

        private void OnProcessUpdate(ProcessUpdateData updateData, bool isRegisterUpdate)
        {
            bool procExists = this.Processes.ContainsKey(updateData.Name);
            if (isRegisterUpdate)
            {
                if (!procExists)
                {
                    RemoteProcess proc = new RemoteProcess(this, updateData.Name);
                    this.Processes.Add(updateData.Name, proc);
                    this.ProcessRegistered?.Invoke(proc);
                }
            }
            else
            {
                if (procExists)
                {
                    RemoteProcess proc = this.Processes[updateData.Name];
                    this.Processes.Remove(updateData.Name);
                    this.ProcessEnded?.Invoke(proc);
                }
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
            if (this.TransmissionHandlers.ContainsKey(identifier))
                throw new NonUniqueIdentifierException(identifier);

            this.TransmissionHandlers.Add(identifier, msg =>
            {
                T data = msg.GetData<T>();
                RemoteProcess proc = this.GetProcess(msg.OriginName);
                TResult result = handler(proc, data);

                return MessageSerializer.SerializeData(result);
            });
        }

        /// <summary>
        /// Registers an event handler that will be fired whenever the transmission for the specified identifier is received
        /// </summary>
        /// <typeparam name="T">The data type we expect to receive</typeparam>
        /// <param name="identifier">The transmission identifier</param>
        /// <param name="handler">The handler to be called when receiving a transmission</param>
        public void OnTransmission<T>(string identifier, Action<RemoteProcess, T> handler)
        {
            if (this.TransmissionHandlers.ContainsKey(identifier))
                throw new NonUniqueIdentifierException(identifier);

            this.TransmissionHandlers.Add(identifier, msg =>
            {
                T data = msg.GetData<T>();
                RemoteProcess proc = this.GetProcess(msg.OriginName);
                handler(proc, data);

                return null;
            });
        }

        /// <summary>
        /// Registers an event handler that will be fired whenever the transmission for the specified identifier is received
        /// </summary>
        /// <param name="identifier">The transmission identifier</param>
        /// <param name="handler">The handler to be called when receiving a transmission</param>
        public void OnTransmission(string identifier, Action<RemoteProcess> handler)
        {
            if (this.TransmissionHandlers.ContainsKey(identifier))
                throw new NonUniqueIdentifierException(identifier);

            this.TransmissionHandlers.Add(identifier, msg =>
            {
                RemoteProcess proc = this.GetProcess(msg.OriginName);
                handler(proc);

                return null;
            });
        }

        /// <summary>
        /// Registers an event handler that will be fired whenever the transmission for the specified identifier is received
        /// </summary>
        /// <param name="identifier">The transmission identifier</param>
        /// <param name="handler">The handler to be called when receiving a transmission</param>
        public void OnTransmission(string identifier, Action handler)
        {
            if (this.TransmissionHandlers.ContainsKey(identifier))
                throw new NonUniqueIdentifierException(identifier);

            this.TransmissionHandlers.Add(identifier, _ =>
            {
                handler();

                return null;
            });
        }

        /// <summary>
        /// Gets whether the specified name corresponds to a valid remote process or not
        /// </summary>
        /// <param name="processName">The name of the remote process to check for</param>
        public bool IsValidProcess(string processName)
            => this.Processes.ContainsKey(processName);

        /// <summary>
        /// Checks whether the remote process with the specified name is available
        /// </summary>
        /// <param name="processName">The name of the remote process to check for</param>
        /// <returns>Is the remote proces available</returns>
        public bool IsProcessAvailable(string processName)
            => this.Processes.ContainsKey(processName);

        /// <summary>
        /// Gets an object representing a remote process
        /// </summary>
        /// <param name="processName">The name of the remote process</param>
        /// <returns>The object representing the remote process</returns>
        public RemoteProcess GetProcess(string processName)
        {
            if (this.IsProcessAvailable(processName))
                return this.Processes[processName];
            else
                throw new UnknownRemoteProcessException(processName);
        }

        /// <summary>
        /// Tries to get an object representing a remote process
        /// </summary>
        /// <param name="processName">The name of the remote process</param>
        /// <param name="remoteProcess">The object representing the remote process</param>
        /// <returns>Was getting the process object successful</returns>
        public bool TryGetProcess(string processName, out RemoteProcess remoteProcess)
        {
            if (this.IsProcessAvailable(processName))
            {
                remoteProcess = this.Processes[processName];
                return true;
            }
            else
            {
                remoteProcess = null;
                return false;
            }
        }

        internal async Task<T> HandleTransmissionResultAsync<T>(int id)
        {
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            CancellationTokenSource cts = new CancellationTokenSource(this.Timeout);
            cts.Token.Register(() =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.SetException(new TimeOutException());
            });

            this.TransmissionTCSs.Add(id, tcs);
            string result = await tcs.Task;
            this.TransmissionTCSs.Remove(id);

            return result == null ? default : MessageSerializer.DeserializeData<T>(result);
        }

        internal async Task<TResult> TransmitObjectAsync<T, TResult>(string identifier, string target, T obj) where T : class
        {
            string payload = MessageSerializer.SerializeData(obj);
            Message msg = this.MessageFactory.CreateMessageRequest(identifier, this.ProcessName, target, payload);
            Task<TResult> t = this.HandleTransmissionResultAsync<TResult>(msg.ID);
            await this.SendAsync(msg);

            return await t;
        }

        internal async Task TransmitObjectAsync<T>(string identifier, string target, T obj) where T : class
        {
            string payload = MessageSerializer.SerializeData(obj);
            Message msg = this.MessageFactory.CreateMessageRequest(identifier, this.ProcessName, target, payload);
            await this.SendAsync(msg);
        }

        internal async Task<TResult> TransmitValueAsync<T, TResult>(string identifier, string target, T value) where T : struct
        {
            string data = value.ToString();
            Message msg = this.MessageFactory.CreateMessageRequest(identifier, this.ProcessName, target, data);
            Task<TResult> t = this.HandleTransmissionResultAsync<TResult>(msg.ID);
            await this.SendAsync(msg);

            return await t;
        }

        internal async Task TransmitValueAsync<T>(string identifier, string target, T value) where T : struct
        {
            string data = value.ToString();
            Message msg = this.MessageFactory.CreateMessageRequest(identifier, this.ProcessName, target, data);
            await this.SendAsync(msg);
        }

        /// <summary>
        /// Transmits an object to every available remote process
        /// </summary>
        /// <typeparam name="T">The type of the object to transmit</typeparam>
        /// <param name="identifier">The identifier to use when transmiting the object</param>
        /// <param name="obj">The object to transmit</param>
        public async Task BroadcastObjectAsync<T>(string identifier, T obj) where T : class
        {
            List<Task> tasks = new List<Task>();
            foreach(KeyValuePair<string, RemoteProcess> proc in this.Processes)
                tasks.Add(this.TransmitObjectAsync(identifier, proc.Key, obj));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Transmits a value to every available remote process
        /// </summary>
        /// <typeparam name="T">The type of the value to transmit</typeparam>
        /// <param name="identifier">The identifier to use when transmiting the value</param>
        /// <param name="value">The value to transmit</param>
        public async Task BroadcastValueAsync<T>(string identifier, T value) where T : struct
        {
            List<Task> tasks = new List<Task>();
            foreach (KeyValuePair<string, RemoteProcess> proc in this.Processes)
                tasks.Add(this.TransmitValueAsync(identifier, proc.Key, value));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Transmits an identifier to every available remote process
        /// </summary>
        /// <param name="identifier">The identifier to use</param>
        public async Task BroadcastAsync(string identifier)
        {
            List<Task> tasks = new List<Task>();
            foreach (KeyValuePair<string, RemoteProcess> proc in this.Processes)
                tasks.Add(this.TransmitObjectAsync<object>(identifier, proc.Key, null));

            await Task.WhenAll(tasks);
        }
    }
}
