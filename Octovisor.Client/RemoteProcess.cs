using Octovisor.Client.Exceptions;
using System;
using System.Threading.Tasks;

namespace Octovisor.Client
{
    /// <summary>
    /// The representation of a remote process
    /// </summary>
    public sealed class RemoteProcess
    {
        private readonly OctoClient Client;

        internal RemoteProcess(OctoClient client, string name)
        {
            this.Client = client;
            this.Name = name;
        }

        /// <summary>
        /// The name of the process
        /// </summary>
        public string Name { get; private set; }

        private void ValidateClientState()
        {
            if (!this.Client.IsConnectedInternal || !this.Client.IsRegisteredInternal)
                throw new NotConnectedException();
        }

        private void ValidateIdentifier(string identifier)
        {
            if (identifier.StartsWith("INTERNAL_OCTOVISOR", StringComparison.InvariantCulture))
                throw new ReservedIdentifierException(identifier);
        }

        private void ValidateTransmission(string identifier)
        {
            this.ValidateClientState();
            this.ValidateIdentifier(identifier);
        }

        /// <summary>
        /// Transmits an identifier on its own with no additional passed data to the remote process
        /// </summary>
        /// <typeparam name="T">The type of the object/value we are expecting to receive</typeparam>
        /// <param name="identifier">The identifier to transmit</param>
        /// <returns>An instance of the expected object/value type</returns>
        public async Task<T> TransmitAsync<T>(string identifier)
        {
            this.ValidateTransmission(identifier);

            return await this.Client.TransmitObjectAsync<object, T>(identifier, this.Name, null);
        }

        /// <summary>
        /// Transmits an identifier on its own with no additional passed data to the remote process
        /// </summary>
        /// <param name="identifier">The identifier to transmit</param>
        public async Task TransmitAsync(string identifier)
        {
            this.ValidateTransmission(identifier);

            await this.Client.TransmitObjectAsync<object>(identifier, this.Name, null);
        }

        /// <summary>
        /// Transmits an object to the remote process
        /// </summary>
        /// <typeparam name="T">The type of the object we are transmiting</typeparam>
        /// <typeparam name="TResult">The type of the object/value we are expecting to receive</typeparam>
        /// <param name="identifier">The identifier to use when transmiting the object</param>
        /// <param name="obj">The object to transmit</param>
        /// <returns>An instance of the expected object/value type</returns>
        public async Task<TResult> TransmitObjectAsync<T, TResult>(string identifier, T obj) where T : class
        {
            this.ValidateTransmission(identifier);

            return await this.Client.TransmitObjectAsync<T, TResult>(identifier, this.Name, obj);
        }

        /// <summary>
        /// Transmits an object to the remote process
        /// </summary>
        /// <typeparam name="T">The type of the object we are transmiting</typeparam>
        /// <param name="identifier">The identifier to use when transmiting the object</param>
        /// <param name="obj">The object to transmit</param>
        public async Task TransmitObjectAsync<T>(string identifier, T obj) where T : class
        {
            this.ValidateTransmission(identifier);

            await this.Client.TransmitObjectAsync(identifier, this.Name, obj);
        }

        /// <summary>
        /// Transmits a non-object value (ValueType) to the remote process 
        /// </summary>
        /// <typeparam name="T">The type of the value we are transmitting</typeparam>
        /// <typeparam name="TResult">The type of the object/value we are expecting to receive</typeparam>
        /// <param name="identifier">The identifier to use when transmiting the value</param>
        /// <param name="value">The value to transmit</param>
        /// <returns>An instance of the expected object/value type</returns>
        public async Task<TResult> TransmitValueAsync<T, TResult>(string identifier, T value) where T : struct
        {
            this.ValidateTransmission(identifier);

            return await this.Client.TransmitValueAsync<T, TResult>(identifier, this.Name, value);
        }

        /// <summary>
        /// Transmits a non-object value (ValueType) to the remote process 
        /// </summary>
        /// <param name="identifier">The identifier to use when transmiting the object</param>
        /// <param name="value">The value to transmit</param>
        public async Task TransmitValueAsync<T>(string identifier, T value) where T : struct
        {
            this.ValidateTransmission(identifier);

            await this.Client.TransmitValueAsync<T>(identifier, this.Name, value);
        }

        public override string ToString()
            => this.Name;
    }
}
