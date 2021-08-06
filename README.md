## IRCrarria
A very simple and modern IRC<->Terraria bridge for TShock. Tested with Mono 6.12 and .NET Framework 4.5 (TShock 4.5.5).
### Installation and configuration
1. Copy the assemblies `IRCrarria.dll`, `IrcDotNet.dll` and `Tomlyn.dll` to the *ServerPlugins* directory in your TShock install
2. Create `ircrarria.toml` in your TShock configuration directory (`<tshock installation dir>/tshock`) with the following contents:
```toml
[host]
hostname = "<IRC server hostname>"
port = 6667 # IRC port
ssl = false # change to 'true' if you need TLS

[irc]
# customize to your needs
username = "ircrarria"
nickname = "ircrarria"
channel = "#main" # IRCrarria can only use one IRC channel at a time
playing_command = "t!playing" # IRC command for listing players online
```
3. done lmao
