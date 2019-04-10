using Newtonsoft.Json;
using System;
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

        public async Task TestPayload<T>(string identifier, string target, T value)
        {
            if (value is object obj)
                await this.WriteObjectAsync(identifier, target, obj);
            else if(typeof(T).IsValueType)
                await this.WriteValueAsync(identifier, target, (ValueType)value);
        }

        internal async Task WriteObjectAsync<T>(string identifier, string target, T obj) where T : class
        {
            string payload = JsonConvert.SerializeObject(obj);
            await this.SendAsync(this.MessageFactory.CreateMessage(identifier, this._ProcessName, target, payload));
        }

        internal async Task WriteValueAsync<T>(string identifier, string target, T value) where T : struct
        {
            string data = value.ToString();
            await this.SendAsync(this.MessageFactory.CreateMessage(identifier, this._ProcessName, target, data));
        }
    }
}
