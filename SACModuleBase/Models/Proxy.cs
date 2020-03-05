using SACModuleBase.Enums;

namespace SACModuleBase.Models
{
    public class Proxy
    {
        public string Host { get; }
        public int Port { get; }
        public ProxyType Type { get; }

        public string UserName { get; }
        public string Password { get; }

        public Proxy(string host, int port, ProxyType type): this(host, port, type, null, null) { }
        public Proxy(string host, int port, ProxyType type, string userName, string password)
        {
            Host = host;
            Port = port;
            Type = type;
            UserName = userName;
            Password = password;
        }
    }
}
