namespace Octovisor.Client
{
    /// <summary>
    /// The Octovisor configuration to be used with the OctoClient
    /// </summary>
    public class Config
    {
        private string _ProcessName;

        public Config()
        {
            this.Token = string.Empty;
            this._ProcessName = string.Empty;
            this.Port = -1;
            this.Address = string.Empty;
            this.MessageFinalizer = '\0';
            this.BufferSize = 255;
            this.Timeout = 5000;
        }

        /// <summary>
        /// The name that Octovisor will use for your application
        /// </summary>
        public string ProcessName
        {
            get => this._ProcessName;
            set
            {
                this._ProcessName = value.Length > 255 ? value.Substring(0, 255) : value;
            } 
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
        public char MessageFinalizer { get; private set; }

        /// <summary>
        /// The size of the buffer used to read from socket
        /// </summary>
        public int BufferSize { get; set; }

        /// <summary>
        /// The maximum time to wait to get responses
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// Determines if this configuration instance is valid
        /// </summary>
        public bool IsValid()
        {
            string[] props = new string[] { this.Token, this.ProcessName, this.Address };
            foreach (string prop in props)
                if (string.IsNullOrWhiteSpace(prop))
                    return false;

            if (this.Port < 1 || this.BufferSize < 1 || this.Timeout < 1)
                return false;

            return true;
        }
    }
}
