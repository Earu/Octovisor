using Octovisor.Server.Properties;
using System;
using System.IO;
using YamlDotNet.Serialization;

namespace Octovisor.Server
{
    public class Config
    {
        public string Token { get; set; }
        public int Port { get; set; }
        public int MaxProcesses { get; set; }
        public string MessageFinalizer { get; set; }

        private bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(this.Token))
                return false;

            if (this.Port < 1)
                return false;

            if (this.MaxProcesses < 1)
                return false;

            if (string.IsNullOrWhiteSpace(this.MessageFinalizer))
                return false;

            return true;
        }

        public static Config Instance { get; private set; }

        public static void Initialize(string path)
        {
            string yaml = File.ReadAllText(path);
            Deserializer deserializer = new Deserializer();
            Config config = null;
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
