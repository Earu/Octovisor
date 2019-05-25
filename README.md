[![Build status](https://ci.appveyor.com/api/projects/status/7c041re9phx9iuuq?svg=true)](https://ci.appveyor.com/project/Earu/octovisor)

<img src="https://repository-images.githubusercontent.com/153482218/02279580-6b38-11e9-957e-6747a4d5deba" width="25%"/>

Octovisor is a IPC/RPC library that allows you to **share objects and values easily between your different applications**. It is composed of a message server that will run on its own and a client API that will let you communicate with other processes using that API on the same server.

### Usage
Build the **Octovisor.Server project along with Octovisor.Messages** for your system, and make configuration file called config.yaml
in the same directory as Octovisor.Server.dll, make sure it is formatted like [this](https://github.com/Earu/Octovisor/blob/master/Octovisor.Server/config.yaml.example).

Once you have the server running you want to use the octovisor client API (**Octovisor.Client**), add it as reference in your project and follow the example below. Do note that your *Config* object must have the same **token and port** as the config file used for the server.

*NOTE: I DO NOT RECOMMEND USING OCTOVISOR IN PERFORMANCE CRITICAL AREAS AS IT RELIES ON NETWORK SPEED EVEN LOCALLY ([FOR NOW](https://github.com/Earu/Octovisor/projects/1#card-14947105)).*

### Example
Here we have a process that calls itself "Process2", and that replies with the string "bar" everytime it receives a transmission with the identifier "foo".
```csharp
Config config = new Config
{
    Token = "token",
    Address = "127.0.0.1",
    Port = 6558,
    ProcessName = "Process2",
};

OctoClient client = new OctoClient(config);
await client.ConnectAsync();
client.OnTransmission<string, string>("foo", (proc, data) => "bar");
```

This other process called "Process1" transmits long strings to our previous process and gets the appropriate answer.
```csharp
Config config = new Config
{
    Token = "token",
    Address = "127.0.0.1",
    Port = 6558,
    ProcessName = "Process1",
};

OctoClient client = new OctoClient(config);
await client.ConnectAsync();
if (client.TryGetProcess("Process2", out RemoteProcess proc))
{
    for (int i = 0; i < 10; i++)
    {
        string result = await proc.TransmitObjectAsync<string, string>("foo", new string('A', 10000));
        Console.WriteLine(result);
    }
}
```
output:
```
bar
bar
bar
etc...
```

### Debugging
Because its not always easy to work on process communication, Octovisor comes with a **debugger for windows users**. You can run your C# scripts at runtime to work with transmission handlers, or send arbitrary objects to other processes for example.

<img src="https://i.imgur.com/JB1civU.gif" width="50%"/>

### Current state
Currently Octovisor is still in its early development, I am always looking for help and feedback on my work, it helps me provide a quality experience, so if you believe you can help in any ways, please do.

### Future
Once the .NET version is free of bugs and necessary improvements I might start writing other implementations of the client in other well-known languages, starting with **JavaScript** to allow Octovisor to evolve through an extended eco-system.
