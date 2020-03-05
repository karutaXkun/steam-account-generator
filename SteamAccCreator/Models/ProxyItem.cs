using Newtonsoft.Json;
using SACModuleBase.Models;
using SteamAccCreator.Enums;
using System;
using System.Net;
using System.Text.RegularExpressions;
using Yove.Proxy;

using modEnums = SACModuleBase.Enums;

namespace SteamAccCreator.Models
{
    [Serializable]
    public class ProxyItem
    {
        public static int ProxyRwTimeout = (int)TimeSpan.FromSeconds(15).TotalMilliseconds;

        public bool Enabled { get; set; } = false;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 0;
        public modEnums.ProxyType ProxyType { get; set; } = modEnums.ProxyType.Unknown;
        [JsonIgnore]
        public ProxyStatus Status { get; set; } = ProxyStatus.Unknown;

        public string UserName { get; set; }
        public string Password { get; set; }

        public ProxyItem() { }
        public ProxyItem(string plain)
        {
            var match = Regex.Match(plain, @"((http[s]?|socks[45]?)\:\/\/)?(([^:]+)\:([^@]+)\@)?([^:]+)\:(\d+)[\/$]?", RegexOptions.IgnoreCase);
            if (!match.Success)
                throw new Exception("Cannot parse this proxy!");

            Enabled = true;

            UserName = match.Groups[4].Value;
            Password = match.Groups[5].Value;

            Host = match.Groups[6].Value;
            Port = int.Parse(match.Groups[7].Value);
            if (Port < 0) Port = 0;
            if (Port > 65535) Port = 65535;

            var type = match.Groups[2].Value.ToLower();
            switch (type)
            {
                case "http":
                    ProxyType = modEnums.ProxyType.Http;
                    break;
                case "https":
                    ProxyType = modEnums.ProxyType.Https;
                    break;
                case "socks4":
                    ProxyType = modEnums.ProxyType.Socks4;
                    break;
                case "socks":
                case "socks5":
                    ProxyType = modEnums.ProxyType.Socks5;
                    break;
            }
        }
        public ProxyItem(string host, int port, modEnums.ProxyType type)
            : this(host, port, type, null, null) { }
        public ProxyItem(string host, int port, modEnums.ProxyType type, string user, string password)
        {
            Enabled = true;
            Host = host;
            Port = port;
            ProxyType = type;
            UserName = user;
            Password = password;
        }

        public IWebProxy ToWebProxy()
        {
            var credentials = default(ICredentials);
            if (!string.IsNullOrEmpty(UserName) &&
                !string.IsNullOrEmpty(Password))
            {
                credentials = new NetworkCredential(UserName, Password);
            }

            if (Port < 0) Port = 0;
            if (Port > 65535) Port = 65535;

            switch (ProxyType)
            {
                case modEnums.ProxyType.Socks4:
                    return new ProxyClient(Host, Port, Yove.Proxy.ProxyType.Socks4) { Credentials = credentials, ReadWriteTimeOut = ProxyRwTimeout };
                case modEnums.ProxyType.Socks5:
                    return new ProxyClient(Host, Port, Yove.Proxy.ProxyType.Socks5) { Credentials = credentials, ReadWriteTimeOut = ProxyRwTimeout };
                case modEnums.ProxyType.Http:
                case modEnums.ProxyType.Https:
                case modEnums.ProxyType.Unknown:
                default:
                    return new WebProxy(Host, Port) { Credentials = credentials };
            }
        }

        public Proxy ToModuleProxy()
            => new Proxy(Host, Port, ProxyType, UserName, Password);

        public override string ToString()
            => $"{ProxyType.ToString().ToLower()}://{Host}:{Port}";

        public override bool Equals(object obj)
            => Equals(this, obj);

        public override int GetHashCode()
            => $"{ProxyType.ToString().ToLower()}://{UserName}:{Password}@{Host}:{Port}/".GetHashCode();

        public static bool operator ==(ProxyItem a, ProxyItem b)
        {
            if (Equals(a, null) && Equals(b, null))
                return true;

            if (Equals(a, null) || Equals(b, null))
                return false;

            return a.ProxyType == b.ProxyType &&
                a.Host?.ToLower() == b.Host?.ToLower() &&
                a.Port == b.Port &&
                a.UserName?.ToLower() == b.UserName?.ToLower() &&
                a.Password?.ToLower() == b.Password?.ToLower();
        }

        public static bool operator !=(ProxyItem a, ProxyItem b)
            => !(a == b);
    }
}
