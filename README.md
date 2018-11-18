# Octovisor
A supervisor that makes sharing data and communication between processes easy.
Do keep in mind that this project is still in early development.

# How to contribute / test ?
I highly suggest you download the entire repository and open the .sln file in Visual Studio 2017 (with .NET Core 2.1).
Once that's done right click Octovisor.Tests.Server "debug", "debug instance" to setup the server.

For the client, there are two implementations, the lua one is the most complete.
To run the lua client you need [copas](https://github.com/keplerproject/copas), [dkjson](https://github.com/LuaDist/dkjson) and [luasocket](https://github.com/diegonehab/luasocket). Once that's done you can run it using Luajit or something similar.

For the C# client that is very incomplete for now, just run the Octovisor.Tests.Client project in Visual Studio 2017, and should be settled.

For contributions, develop how you like as long as folder like .vscode and .vs are added to the .gitignore, do pull request once you are finished.

# What's the big idea ?
Making applications be able to exchange data in real time wherever they are hosted without having the need to setup an API, and without having to code your own networking with sockets.
