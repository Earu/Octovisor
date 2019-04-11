using Octovisor.Client.Exceptions;
using System;
using System.Threading.Tasks;

namespace Octovisor.Client
{
    public class RemoteProcess : IDisposable
    {
        private readonly OctoClient _Client;

        internal RemoteProcess(OctoClient client, string name)
        {
            this._Client = client;
            this.Name = name;
        }

        /// <summary>
        /// The name of the process
        /// </summary>
        public string Name { get; private set; }

        private void VerifyClientState()
        {
            //if (!this._Client.IsConnected)
            //    throw new UnconnectedException();

            //if (!this._Client.IsRegistered)
            //    throw new UnregisteredException();
        }

        /// <summary>
        /// Transmits an object to the remote process
        /// </summary>
        /// <param name="identifier">The identifier to use when transmiting the object</param>
        /// <param name="obj">The object to transmit</param>
        public async Task TransmitObjectAsync<T>(string identifier, T obj) where T : class
        {
            this.VerifyClientState();

            await this._Client.TransmitObjectAsync(identifier, this.Name, obj);
        }

        /// <summary>
        /// Transmits a non-object value (ValueType) to the remote process 
        /// </summary>
        /// <param name="identifier">The identifier to use when transmiting the object</param>
        /// <param name="value">The value to transmit</param>
        public async Task TransmitValueAsync<T>(string identifier, T value) where T : struct
        {
            this.VerifyClientState();

            await this._Client.TransmitValueAsync(identifier, this.Name, value);
        }

        /// <summary>
        /// Awaits to receive an object using a specified identifier
        /// </summary>
        /// <param name="identifier">The identifier to use to check inbound objects</param>
        /// <returns>An instance of the awaited object type</returns>
        public async Task<T> ReceiveObjectAsync<T>(string identifier) where T : class
        {
            this.VerifyClientState();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Awaits to receive a value using a specified identifier
        /// </summary>
        /// <param name="identifier">The identifier to use to check inbound values</param>
        /// <returns>An instance of the awaited value type</returns>
        public async Task<T> ReceiveValueAsync<T>(string identifier) where T : struct
        {
            this.VerifyClientState();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Frees all the resources used by this instance
        /// </summary>
        public void Dispose()
            => this._Client.DisposeOf(this.Name);
    }
}
