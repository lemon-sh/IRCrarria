<h1 align="center">IRCrarria</h1>

A simple IRC<->Terraria chat bridge for TShock. Tested with .NET 6 and TShock 5.0.0.

<h2 align="center">Download</h2>

* Unstable: [IRCrarria pre-1.3](https://github.com/lemon-sh/IRCrarria/releases/tag/pre-1.3) (TShock 5 / Terraria 1.4.4)
* Stable: [IRCrarria 1.2](https://github.com/lemon-sh/IRCrarria/releases/tag/1.2.0) (TShock 4.5 / Terraria 1.4.3)

All releases (and pre-releases) are available [here](https://github.com/lemon-sh/IRCrarria/releases).

<h2 align="center">Installation and configuration</h2>

**Important: After updating, make sure that your config file structure matches the config shown below to avoid errors! (it changes sometimes)**
1. Unzip the archive to the *ServerPlugins* directory of your TShock install
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
3. Adjust the config to your needs
4. Done!
