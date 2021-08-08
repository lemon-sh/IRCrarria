<h1 align="center">IRCrarria</h1>
A very simple IRC<->Terraria chat bridge for TShock. Tested with Mono 6.12 on Arch Linux and .NET Framework 4.6 on Windows (using TShock 4.5.5).

[<h3 align="center">Download link (1.0.0)</h3>](https://files.catbox.moe/9sufi0.zip)
**Important: After updating, make sure that your config file structure matches the config shown below to avoid errors! (it changes sometimes)**
<h2 align="center">Installation and configuration</h2>

1. Copy the assemblies `IRCrarria.dll`, `IrcDotNet.dll` and `Tomlyn.dll` to the *ServerPlugins* directory in your TShock install
2. Create `ircrarria.toml` in your TShock configuration directory (`<tshock installation dir>/tshock`) with the following contents:
```toml
[host]
hostname = "<IRC server hostname>"
port = 6667
ssl = false # important! change to 'true' if you need TLS

[irc]
# customize to your needs
username = "ircrarria"
nickname = "ircrarria"
channel = "#terraria"
prefix = "t!" # IRC command prefix
```
3. Done
