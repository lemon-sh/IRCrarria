using Tomlyn;
using Tomlyn.Model;

namespace IRCrarria
{
    public class Config
    {
        public string Hostname { get; }
        public int Port { get; }
        public bool UseSsl { get; }

        public string Username { get; }
        public string Nickname { get; }
        public string Channel { get; }
        public string Prefix { get; }

        public Config(string configText)
        {
            var document = Toml.Parse(configText).ToModel();
            var hosttable = (TomlTable) document["host"];
            Hostname = (string) hosttable["hostname"];
            Port = (int)(long) hosttable["port"]; 
            UseSsl = (bool) hosttable["ssl"];
            var irctable = (TomlTable) document["irc"];
            Username = (string) irctable["username"];
            Nickname = (string) irctable["nickname"];
            Channel = (string) irctable["channel"];
            Prefix = (string) irctable["prefix"];
        }
    }
}