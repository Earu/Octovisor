namespace Octovisor.Client
{
    public class Config
    {
        public Config()
        {
            this.Token = string.Empty;
            this.ProcessName = string.Empty;
            this.Port = -1;
            this.Address = string.Empty;
            this.MessageFinalizer = "\0";
            this.BufferSize = 255;
        }

        public string Token { get; set; } 
        public string ProcessName { get; set; }
        public int Port { get; set; }
        public string Address { get; set; }
        public string MessageFinalizer { get; set; }
        public int BufferSize { get; set; }

        public bool IsValid()
        {
            string[] props = new string[] { this.Token, this.ProcessName, this.Address };
            foreach (string prop in props)
                if (string.IsNullOrWhiteSpace(prop))
                    return false;

            if (this.Port < 1)
                return false;

            return true;
        }
    }
}
