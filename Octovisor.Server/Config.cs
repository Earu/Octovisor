using Octovisor.Server.Properties;
using System;
using System.IO;
using YamlDotNet.Serialization;

namespace Octovisor.Server
{
    public class Config
    {
        [YamlIgnore]
        public const char MessageFinalizer = '\0';

        public string Token { get; set; }
        public int TCPSocketPort { get; set; }
        public int WebSocketPort { get; set; }
        public int MaxProcesses { get; set; }

        private bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(this.Token))
                return false;

            if (this.TCPSocketPort < 1 || this.WebSocketPort < 1 || this.MaxProcesses < 1)
                return false;

            return true;
        }

        public static Config Instance { get; private set; }

        public static void Initialize(string path)
        {
            string yaml = File.ReadAllText(path);
            Deserializer deserializer = new Deserializer();
            Config config;
            try
            {
                config = deserializer.Deserialize<Config>(yaml);
            }
            catch
            {
                throw new Exception(Resources.CouldNotParseConfig);
            }

            if (!config.IsValid())
                throw new Exception(Resources.InvalidConfig);

            Instance = config;
        }
    }
}
