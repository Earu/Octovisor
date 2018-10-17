namespace Octovisor.Client.Models
{
    public class RemoteProcess
    {
        internal readonly OctovisorClient Client;

        public string Name { get; private set; }

        internal RemoteProcess(OctovisorClient client, string name)
        {
            this.Client = client;
            this.Name = name;
        }

        public MessageListener<T> ListenToMessage<T>(string identifier)
            => new MessageListener<T>(this, identifier);
    }
}
