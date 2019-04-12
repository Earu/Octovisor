using Newtonsoft.Json;

namespace Octovisor.Messages
{
    public class RemoteProcessData
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        public RemoteProcessData(string name)
        {
            this.Name = name;
        }
    }
}
