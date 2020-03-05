namespace SACModuleBase.Models.Capcha
{
    public class ReCaptchaRequest
    {
        /// <summary>
        /// ReCaptcha page URL
        /// </summary>
        public string Url { get; }
        /// <summary>
        /// ReCaptcha site-key
        /// </summary>
        public string SiteKey { get; }

        public Proxy Proxy { get; }

        public ReCaptchaRequest(string siteKey, string url, Proxy proxy)
        {
            Url = url;
            SiteKey = siteKey;
            Proxy = proxy;
        }
    }
}
