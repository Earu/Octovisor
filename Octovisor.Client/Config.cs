namespace Octovisor.Client
{
    public class Config
    {
        private string _ProcessName;

        public Config()
        {
            this.Token = string.Empty;
            this._ProcessName = string.Empty;
            this.Port = -1;
            this.Address = string.Empty;
            this.MessageFinalizer = "\0";
            this.BufferSize = 255;
        }

        public string ProcessName
        {
            get => this._ProcessName;
            set
            {
                this._ProcessName = value.Length > 255 ? value.Substring(0, 255) : value;
            } 
        }

        public string Token { get; set; }
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
