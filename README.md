<h1 align="center">IRCrarria</h1>
A very simple IRC<->Terraria chat bridge for TShock. Tested with Mono 6.12 on Arch Linux and .NET Framework 4.6.1 on Windows (using TShock 4.5.5).

[<h3 align="center">Download link (1.2.0)</h3>](https://files.catbox.moe/70d6vl.zip)
<p align="center">Pre-releases are available as GitHub Releases</p>

**Important: After updating, make sure that your config file structure matches the config shown below to avoid errors! (it changes sometimes)**
<h2 align="center">Installation and configuration</h2>

1. Copy the assemblies `IRCrarria.dll` and `Tomlyn.dll` to the *ServerPlugins* directory in your TShock install
2. Create `ircrarria.toml` in your TShock configuration directory (`<tshock installation dir>/tshock`) with the following contents:
```toml
[host]
hostname = "<IRC server hostname>"
port = 6697
ssl = true  # change to 'true' if you need TLS
skip_cert_validation = false  # DANGEROUS! Use only when absolutely required.

[irc]
username = "ircrarria"
nickname = "ircrarria"
channel = "#terraria"
prefix = "t!"  # IRC command prefix

# OPTIONAL: specify *raw* IRC commands to run after the bot registers
connect_commands = [
  "PRIVMSG NickServ :identify topsecretpwd"
]

# OPTIONAL: Additional server info that will be shown when the 'serverinfo' command is used
[server_details]
"Server name" = "Lemon's Terraria Server"
"IP & Port" = "127.0.0.1:7777"
"this server is" = "very cool"
```
3. Done (remember to adjust the config file to your needs)
