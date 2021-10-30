using System.Collections.Generic;
using System.Linq;
using Tomlyn;
using Tomlyn.Model;

namespace IRCrarria
{
    public class Config
    {
        public string Hostname { get; }
        public int Port { get; }
        public bool UseSsl { get; }
        public bool SkipCertValidation { get; }
        public string Username { get; }
        public string Nickname { get; }
        public string Channel { get; }
        public string Prefix { get; }
        public IEnumerable<KeyValuePair<string, object>> ExtraDetails { get; }
        public IEnumerable<string> ConnectCommands { get; }

        public Config(string configText)
        {
            var document = Toml.Parse(configText).ToModel();
            var hosttable = (TomlTable) document["host"];
            Hostname = (string) hosttable["hostname"];
            Port = (int)(long) hosttable["port"]; // yes this cast is required
            UseSsl = (bool) hosttable["ssl"];
            SkipCertValidation = (bool) hosttable["skip_cert_validation"];
            var irctable = (TomlTable) document["irc"];
            Username = (string) irctable["username"];
            Nickname = (string) irctable["nickname"];
            Channel = (string) irctable["channel"];
            Prefix = (string) irctable["prefix"];
            if (document.ContainsKey("server_details")) ExtraDetails = (TomlTable) document["server_details"];
            if (irctable.ContainsKey("connect_commands")) ConnectCommands = ((TomlArray) irctable["connect_commands"]).OfType<string>();
        }
    }
}