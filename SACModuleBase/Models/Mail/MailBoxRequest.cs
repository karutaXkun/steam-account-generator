using System.Net;

namespace SACModuleBase.Models.Mail
{
    public class MailBoxRequest
    {
        public string Login { get; private set; }
        public Proxy Proxy { get; private set; }

        public MailBoxRequest(string login, Proxy proxy)
        {
            Login = login;
            Proxy = proxy;
        }
    }
}
