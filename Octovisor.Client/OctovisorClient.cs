﻿using Octovisor.Client.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Octovisor.Client
{
    public class OctovisorClient
    {
        public event Action<Exception> OnError;
        public event Action<string> Log;

        private readonly ManualResetEvent OnConnectDone;
        private readonly ManualResetEvent OnSendDone;
        private readonly ManualResetEvent OnReceiveDone;

        private Socket Client;

        private readonly OctovisorConfig Config;

        public bool IsConnected { get; private set; }

        public OctovisorClient(OctovisorConfig config)
        {
            if (!config.IsValid)
                throw new Exception("Invalid Octovisor config");
                
            this.Config        = config;
            this.OnConnectDone = new ManualResetEvent(false);
            this.OnSendDone    = new ManualResetEvent(false);
            this.OnReceiveDone = new ManualResetEvent(false);

            this.Connect();
        }

        private void CallErrorEvent(Exception e) => this.OnError?.Invoke(e);
        private void CallLogEvent(string log) => this.Log?.Invoke(log);

        /// <summary>
        /// This is called into the constructor so do not use it again unless you get disconnected
        /// </summary>
        public void Connect()
        {
            Thread thread = new Thread(this.InternalConnect);
            thread.Start();
        }

        private void InternalConnect()
        {
            try
            {
                IPHostEntry hostinfo = Dns.GetHostEntry(this.Config.ServerAddress);
                IPAddress ipadr = hostinfo.AddressList[0];
                IPEndPoint endpoint = new IPEndPoint(ipadr, this.Config.ServerPort);

                this.Client = new Socket(ipadr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                this.Client.BeginConnect(endpoint, new AsyncCallback(this.ConnectCallback), this.Client);
                this.OnConnectDone.WaitOne();

                this.IsConnected = true;

                this.RegisterOnServer();
            }
            catch(Exception e)
            {
                this.IsConnected = false;
                this.CallErrorEvent(e);
            }

            this.StartReceiving();
        }

        private void RegisterOnServer()
        {
            this.Send(new ProcessMessage
            {
                OriginName = this.Config.ProcessName,
                TargetName = "SERVER",
                MessageIdentifier = "INTERNAL_OCTOVISOR_PROCESS_INIT",
                Data = null,
                Status = ProcessMessageStatus.OK,
            });

            this.CallLogEvent("Registering on server");
        }

        private void EndOnServer()
        {
            this.Send(new ProcessMessage
            {
                OriginName = this.Config.ProcessName,
                TargetName = "SERVER",
                MessageIdentifier = "INTERNAL_OCTOVISOR_PROCESS_END",
                Data = null,
                Status = ProcessMessageStatus.OK,
            });

            this.CallLogEvent("Ending on server");
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndConnect(ar);

                this.OnConnectDone.Set();
            }
            catch(Exception e)
            {
                this.CallErrorEvent(e);
            }
        }

        private void StartReceiving()
        {
            if (!this.IsConnected) return;

            this.CallLogEvent("Beginning receiving from server");

            try
            {
                StateObject state = new StateObject(this.Client);

                this.Client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(this.ReceiveCallback), state);
            }
            catch(Exception e) 
            {
                this.CallErrorEvent(e);
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.WorkSocket;

                int bytesread = client.EndReceive(ar);

                if (bytesread > 0)
                {
                    string data = Encoding.UTF8.GetString(state.Buffer, 0, bytesread);
                    this.CallLogEvent($"Received {bytesread} bytes.\nData: {data}");

                    client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(this.ReceiveCallback), state);
                }
                else
                {
                    this.OnReceiveDone.Set();
                }
            }
            catch (Exception e)
            {
                this.CallErrorEvent(e);
            }
        }

        private void Send(ProcessMessage msg,Action<IAsyncResult> callback=null)
        {
            if (!this.IsConnected) return;

            string data = msg.Serialize();
            byte[] bytedata = Encoding.UTF8.GetBytes(data);

            this.CallLogEvent($"Sending {data.Length} bytes.\nData: {data}");
            this.Client.BeginSend(bytedata, 0, bytedata.Length, 0, new AsyncCallback(res => {
                this.SendCallback(res);
                callback?.Invoke(res);
            }), this.Client);
            this.OnSendDone.WaitOne();
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndSend(ar);

                this.OnSendDone.Set();
            }
            catch(Exception e)
            {
                this.CallErrorEvent(e);
            }
        }

        public void SendGarbage(string proc,string msg)
        {
            this.Send(new ProcessMessage
            {
                ID = 666,
                TargetName = proc,
                MessageIdentifier = msg,
                OriginName = this.Config.ProcessName,
                Data = null,
                Status = ProcessMessageStatus.OK,
            });
        }
    }
}
