### Octovisor
Octovisor is a message service that allows you to **share objects and values easily between your different .NET applications**. It is composed of a message server that will run on its own and a client API that will let you communicate with other processes using that API on the same server.

### Usage
Build the **Octovisor.Server project along with Octovisor.Messages** for your system, and make configuration file called config.yaml
in the same directory as Octovisor.Server.dll, make sure it is formatted like [this](https://github.com/Earu/Octovisor/blob/master/Octovisor.Server/config.yaml.example). 

Once you have the server running you want to use the octovisor client API (**Octovisor.Client**), add it as reference in your project and follow the example below. Do note that your *Config* object must have the same **token and port** as the config file used for the server.

*NOTE: I DO NOT RECOMMEND USING OCTOVISOR IN PERFORMANCE CRITICAL AREAS, EXPECT LATENCIES OF ~70MS TO TRANSMIT AN OBJECT OVER A NETWORK*

### Example
Here we have a process that calls itself "Process2", and that replies with the string "no u" everytime it receives a transmission with the identifier "meme".
```csharp
Config config = new Config
{
    Token = "you're cool",
    Address = "127.0.0.1",
    Port = 6558,
    ProcessName = "Process2",
};

OctoClient client = new OctoClient(config);
await client.ConnectAsync();
client.OnTransmission<string, string>("meme", (proc, data) => "no u");
```

This other process called "Process1" transmits long strings to our previous process and gets the appropriate answer.
```csharp
Config config = new Config
{
    Token = "you're cool",
    Address = "127.0.0.1",
    Port = 6558,
    ProcessName = "Process1",
};

OctoClient client = new OctoClient(config);
await client.ConnectAsync();
RemoteProcess proc = client.GetProcess("Process2");
for (int i = 0; i < 10; i++)
{
    string result = await proc.TransmitObjectAsync<string, string>("meme", new string('A', 10000));
    Console.WriteLine(result);
}
```
output:
```
no u
no u 
no u
etc...
```

### Current state
Currently Octovisor is still in its early development, I am always looking for help and feedback on my work, it helps me provide a quality experience, so if you believe you can help in any ways, please do.

### Future
Once the .NET version is free of bugs and necessary improvements I might start writing other implementations of the client in other well-known languages to allow Octovisor to evolve through an extended eco-system.
