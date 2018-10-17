using Newtonsoft.Json;

namespace Octovisor.Client
{
    public class OctovisorConfig
    {
        public string ProcessName { get; set; } = string.Empty;

        public int ServerPort { get; set; } = -1;

        public string ServerAddress { get; set; } = string.Empty;

        internal bool IsValid {
            get => !string.IsNullOrWhiteSpace(this.ProcessName) 
                || this.ServerPort <= 0 
                || !string.IsNullOrWhiteSpace(this.ServerAddress);
        }
    }
}
