namespace Octovisor.Models
{
    public class ServerConfig
    {
        public ServerConfig()
        {
            this.Token = string.Empty;
            this.ServerPort = -1;
            this.ServerAddress = string.Empty;
            this.MaximumProcesses = 255;
        }

        public string Token { get; set; }
        public int ServerPort { get; set; }
        public string ServerAddress { get; set; }
        public int MaximumProcesses { get; set; }

        //Ugly but cba to create properties for everything
        public bool IsValid()
        {
            bool valid = !string.IsNullOrWhiteSpace(this.Token);
            valid = this.ServerPort >= 1;
            valid = !string.IsNullOrWhiteSpace(this.ServerAddress);

            return valid;
        }
    }
}
