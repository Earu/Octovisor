using System.IO;
using System.Linq;
using YamlDotNet.Serialization;

namespace Octovisor.Client
{
    /// <summary>
    /// The Octovisor configuration to be used with the OctoClient
    /// </summary>
    public class OctoConfig
    {
        private string InternalProcessName;

        public OctoConfig()
        {
            this.Token = string.Empty;
            this.InternalProcessName = string.Empty;
            this.Port = -1;
            this.Address = string.Empty;
            this.MessageFinalizer = '\0';
            this.BufferSize = 256;
            this.Timeout = 5000;
            this.CompressionThreshold = 300;
        }

        /// <summary>
        /// The name that Octovisor will use for your application
        /// </summary>
        public string ProcessName
        {
            get => this.InternalProcessName;
            set => this.InternalProcessName = value.Length > 255 ? value.Substring(0, 255) : value;
        }

        /// <summary>
        /// The token to use to authenticate to the Octovisor server
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// The port to connect to for the Octovisor server
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// The address to connect to for the Octovisor server
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// The string that will be used to segment Octovisor messages
        /// This needs to be the same as the server, it is strongly advised to keep it set to "\0"
        /// </summary>
        [YamlIgnore]
        public char MessageFinalizer { get; }

        /// <summary>
        /// The size of the buffer used to read from socket
        /// </summary>
        public int BufferSize { get; set; }

        /// <summary>
        /// The maximum time to wait to get responses
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// The minimum amount of bytes for data to be compressed
        /// </summary>
        public int CompressionThreshold { get; set; }

        /// <summary>
        /// Determines if this configuration instance is valid
        /// </summary>
        public bool IsValid()
        {
            string[] props = { this.Token, this.ProcessName, this.Address };
            if (props.Any(string.IsNullOrWhiteSpace))
                return false;

            return this.Port >= 1 && this.BufferSize >= 1 && this.Timeout >= 1 && this.CompressionThreshold >= 1;
        }

        /// <summary>
        /// Creates a config object based on a yaml file
        /// </summary>
        /// <param name="path">The path to the yaml config file</param>
        /// <returns>A config object</returns>
        public static OctoConfig FromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException();

            FileInfo fileInfo = new FileInfo(path);
            if (fileInfo.Extension != ".yaml")
                throw new FileLoadException("Expected a file of yaml format");

            using (StreamReader reader = fileInfo.OpenText())
            {
                string yaml = reader.ReadToEnd();
                Deserializer deserializer = new Deserializer();
                return deserializer.Deserialize<OctoConfig>(yaml);
            }
        }
    }
}
