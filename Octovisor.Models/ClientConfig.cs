namespace Octovisor.Models
{
    public class ClientConfig
    {
        public ClientConfig()
        {
            this.Token = string.Empty;
            this.ProcessName = string.Empty;
            this.ServerPort = -1;
            this.ServerAddress = string.Empty;
        }

        public string Token { get; set; } 

        public string ProcessName { get; set; }

        public int ServerPort { get; set; } = -1;

        public string ServerAddress { get; set; }

        //Ugly but cba to create properties for everything
        public bool IsValid()
        {
            bool valid = !string.IsNullOrWhiteSpace(this.Token);
            valid = !string.IsNullOrWhiteSpace(this.ProcessName);
            valid = this.ServerPort >= 1;
            valid = !string.IsNullOrWhiteSpace(this.ServerAddress);

            return valid;
        }
    }
}
