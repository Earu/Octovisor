using Octovisor.Messages;
using Octovisor.Server.ClientStates;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octovisor.Server
{
    internal class Dispatcher
    {
        private readonly Logger Logger;
        private readonly MessageFactory Factory;
        private readonly string Token;
        private readonly ConcurrentDictionary<string, BaseClientState> States;

        internal Dispatcher(Logger logger)
        {
            this.Logger = logger;
            this.Factory = new MessageFactory();
            this.Token = Config.Instance.Token;
            this.States = new ConcurrentDictionary<string, BaseClientState>();
        }

        internal string GetProcessesData(string origin)
        {
            List<RemoteProcessData> res = new List<RemoteProcessData>();
            foreach (KeyValuePair<string, BaseClientState> state in this.States)
            {
                string name = state.Key;
                if (!name.Equals(origin))
                    res.Add(new RemoteProcessData(state.Key));
            }

            return MessageSerializer.Serialize(res);
        }

        internal async Task HandleMessageAsync(BaseClientState state, Message msg)
        {
            switch (msg.Identifier)
            {
                case MessageConstants.REGISTER_IDENTIFIER:
                    await this.RegisterProcessAsync(state, msg.OriginName, msg.Data);
                    break;
                case MessageConstants.TERMINATE_IDENTIFIER:
                    await this.TerminateProcessAsync(msg.OriginName, msg.Data);
                    break;
                case MessageConstants.REQUEST_PROCESSES_INFO_IDENTIFIER:
                    string processesData = this.GetProcessesData(msg.OriginName);
                    await this.AnswerMessageAsync(state, msg, processesData);
                    break;
                default:
                    await this.DispatchMessageAsync(state, msg);
                    break;
            }
        }

        internal async Task HandleMessagesAsync(BaseClientState state, IEnumerable<Message> msgs)
        {
            foreach (Message msg in msgs)
                await this.HandleMessageAsync(state, msg);
        }

        private async Task RegisterProcessAsync(BaseClientState state, string name, string token)
        {
            ProcessUpdateData data;
            if (string.IsNullOrWhiteSpace(token) || token != this.Token)
            {
                this.Logger.Warning($"Attempt to register a remote process ({name}) with an invalid token ({token}).");
                data = new ProcessUpdateData(false, name);
            }
            else if (this.States.Count >= Config.Instance.MaxProcesses)
            {
                this.Logger.Warning($"Could not register a remote process ({name}). Exceeding the maximum amount of remote processes.");
                data = new ProcessUpdateData(false, name);
            }
            else
            {
                if (this.States.ContainsKey(name))
                {
                    this.Logger.Nice("Process", ConsoleColor.Yellow, $"Overriding a remote process ({name})");
                    this.TerminateProcess(name);
                }

                state.Name = name;
                this.States.AddOrUpdate(name, state, (_, __) => state);
                state.Register();
                this.Logger.Nice("Process", ConsoleColor.Magenta, $"Registering new remote process | {name}");
                data = new ProcessUpdateData(true, name);
            }

            await this.BroadcastMessageAsync(MessageConstants.REGISTER_IDENTIFIER, data.Serialize());
        }

        private async Task TerminateProcessAsync(string name, string token)
        {
            ProcessUpdateData data;
            if (token != this.Token)
            {
                this.Logger.Warning($"Attempt to terminate a remote process ({name}) with an invalid token.");

                data = new ProcessUpdateData(false, name);
                await this.BroadcastMessageAsync(MessageConstants.TERMINATE_IDENTIFIER, data.Serialize());
            }
            else if (!this.States.ContainsKey(name))
            {
                this.Logger.Warning($"Attempt to terminate a non-existing remote process ({name}). Discarding.");

                data = new ProcessUpdateData(false, name);
                await this.BroadcastMessageAsync(MessageConstants.TERMINATE_IDENTIFIER, data.Serialize());
            }
            else
            {
                data = new ProcessUpdateData(true, name);
                await this.BroadcastMessageAsync(MessageConstants.TERMINATE_IDENTIFIER, data.Serialize());

                BaseClientState state = this.States[name];
                state.Dispose();
                this.States.Remove(name, out BaseClientState _);

                this.Logger.Nice("Process", ConsoleColor.Magenta, $"Terminating remote process | {name}");
            }
        }

        // When client closes brutally
        internal void TerminateProcess(string name)
        {
            if (this.States.ContainsKey(name))
            {
                BaseClientState state = this.States[name];
                state.Dispose();
                this.States.Remove(name, out BaseClientState _);

                this.Logger.Nice("Process", ConsoleColor.Magenta, $"Terminating remote process | {state.Name}");
            }
        }

        private async Task SendAsync(BaseClientState state, Message msg)
        {
            try
            {
                await state.SendAsync(msg);
            }
            catch(Exception ex)
            {
                this.TerminateProcess(state.Name);

                ProcessUpdateData endData = new ProcessUpdateData(true, state.Name);
                await this.BroadcastMessageAsync(MessageConstants.TERMINATE_IDENTIFIER, endData.Serialize());
            }
        }

        private async Task ForwardMessageAsync(Message msg)
        {
            string tail;
            BaseClientState state = this.States[msg.TargetName];
            msg.Status = msg.Status != MessageStatus.Unknown ? msg.Status : MessageStatus.Success;
            switch (msg.Type)
            {
                case MessageType.Request:
                    tail = $"| (ID: {msg.Identifier}) {msg.OriginName} -> {msg.TargetName}";
                    this.Logger.Nice("Message", ConsoleColor.Gray, $"Forwarded request {tail}");
                    break;
                case MessageType.Response:
                    tail = $"| (ID: {msg.Identifier}) {msg.OriginName} -> {msg.TargetName}";
                    this.Logger.Nice("Message", ConsoleColor.Gray, $"Forwarded response {tail}");
                    break;
                case MessageType.Unknown:
                    tail = $"| (ID: {msg.Identifier}) {msg.OriginName} ?? {msg.TargetName}";
                    this.Logger.Nice("Message", ConsoleColor.Yellow, $"Forwarded unknown message type {msg.Length} bytes {tail}");
                    break;
            }

            await this.SendAsync(state, msg);
        }

        internal async Task AnswerMessageAsync(BaseClientState state, Message msg, string data = null, MessageStatus status = MessageStatus.Success)
        {
            Message replyMsg = this.Factory.CreateMessageResponse(msg, data, status);
            await this.SendAsync(state, replyMsg);
        }

        internal async Task BroadcastMessageAsync(string identifier, string data)
        {
            foreach (KeyValuePair<string, BaseClientState> state in this.States)
            {
                Message broadcastMsg = this.Factory.CreateMessage(identifier, MessageConstants.SERVER_PROCESS_NAME, state.Value.Name,
                    data, MessageType.Response, MessageStatus.Success);
                await this.SendAsync(state.Value, broadcastMsg);
            }
        }

        internal async Task DispatchMessageAsync(BaseClientState state, Message msg)
        {
            if (msg.IsMalformed)
            {
                if (state is TCPSocketClientState tcpState)
                    this.Logger.Warning($"Received a malformed message from \'{tcpState.RemoteEndPoint}\'\n{msg.Data}");
                else
                    this.Logger.Warning($"Received a malformed message\n{msg.Data}");

                return;
            }

            if (this.States.ContainsKey(msg.TargetName) && this.States.ContainsKey(msg.OriginName))
            {
                await this.ForwardMessageAsync(msg);
            }
            else if (this.States.ContainsKey(msg.OriginName) && !this.States.ContainsKey(msg.OriginName))
            {
                this.Logger.Warning($"{msg.OriginName} tried to forward {msg.Length} bytes to unknown remote process {msg.TargetName}");
                await this.AnswerMessageAsync(state, msg, null, MessageStatus.ProcessNotFound);
            }
        }
    }
}
