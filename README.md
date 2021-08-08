## IRCrarria
A very simple IRC<->Terraria chat bridge for TShock. Tested with Mono 6.12 on Arch Linux and .NET Framework 4.6 on Windows (using TShock 4.5.5).
##### [Download link](https://files.catbox.moe/9sufi0.zip)
### Installation and configuration
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
