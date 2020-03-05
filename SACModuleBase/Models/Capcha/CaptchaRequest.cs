using System.Net;

namespace SACModuleBase.Models.Capcha
{
    public class CaptchaRequest
    {
        /// <summary>
        /// Base64 image
        /// </summary>
        public string CaptchaImage { get; private set; }
        public Proxy Proxy { get; }

        public CaptchaRequest(string captchaImage, Proxy proxy)
        {
            CaptchaImage = captchaImage;
            Proxy = proxy;
        }
    }
}
