using Newtonsoft.Json;
using RestSharp;
using System;
using System.Net;
using System.Threading;

namespace SteamAccCreator.Web.Steam
{
    public class SteamWebClient
    {
        public static bool DisableLegitDelay = false;

        internal RestClient HttpClient;
        public CookieContainer CookieContainer
        {
            get => HttpClient.CookieContainer;
            private set => HttpClient.CookieContainer = value;
        }

        public string SessionId
        {
            get
            {
                var cookies = CookieContainer.GetCookies(new Uri(SteamDefaultUrls.STORE_BASE));
                foreach (Cookie cookie in cookies)
                {
                    if (cookie.Name.ToLower() != "sessionid")
                        continue;

                    return cookie.Value;
                }

                return string.Empty;
            }
        }
        public long? SteamId { get; internal set; }

        public Join.SteamJoin Join { get; }
        public TwoFactor.SteamTwoFactor TwoFactor { get; }
        public Account.AccountManage Account { get; }

        public SteamWebClient() : this(client: null) { }
        public SteamWebClient(RestClient client)
        {
            HttpClient = client ?? new RestClient();
            HttpClient.CookieContainer = HttpClient.CookieContainer ?? new CookieContainer(); // create new container if null
            HttpClient.AddDefaultHeader("Accept-Language", "en-US,en;q=0.5");

            Join = new Join.SteamJoin(this);
            TwoFactor = new TwoFactor.SteamTwoFactor(this);
            Account = new Account.AccountManage(this);
        }

        /// <summary>
        /// Execute using internal HTTP client
        /// </summary>
        public IRestResponse Execute(IRestRequest request)
            => HttpClient.Execute(request);

        /// <summary>
        /// Execute using internal HTTP client and deserialize to object from JSON
        /// </summary>
        /// <typeparam name="T">Type needed</typeparam>
        public SteamResponse<T> Execute<T>(IRestRequest request)
            => Execute<T>(request, null);
        /// <summary>
        /// Execute using internal HTTP client and deserialize to object from JSON
        /// </summary>
        /// <typeparam name="T">Type needed</typeparam>
        public SteamResponse<T> Execute<T>(IRestRequest request, JsonSerializerSettings jsonSerializerSettings)
        {
            var response = Execute(request);
            Logger.Trace($"--/HTTP/--:\n" +
                $"{nameof(request)}.{nameof(request.Resource)}: {request.Resource}\n" +
                $"{nameof(response)}.{nameof(response.IsSuccessful)}: {((response.IsSuccessful) ? "true" : "false")}\n" +
                $"{nameof(response)}.{nameof(response.Content)}:\n=========\n{response.Content}\n=========");

            try
            {
                var outObject = JsonConvert.DeserializeObject<T>(response.Content, jsonSerializerSettings);
                return new SteamResponse<T>(outObject, response);
            }
            catch(Exception ex)
            {
                return new SteamResponse<T>(ex);
            }
        }

        internal SteamResponse<Models.SteamCaptcha> GetCaptcha(string captchaRefreshUrl, int count = 1)
        {
            var request = new RestRequest(captchaRefreshUrl, Method.POST);
            request.AddParameter("count", count);

            return Execute<Models.SteamCaptcha>(request);
        }

        public SteamResponse<byte[]> DownloadCaptchaImage(Models.SteamCaptcha captcha)
        {
            if (captcha.Type == 2)
                return new SteamResponse<byte[]>(new Exception("It's google captcha. This method can download only image captchas!"));

            var request = new RestRequest(SteamDefaultUrls.RENDER_CAPTCHA, Method.GET);
            request.AddParameter("gid", captcha.Gid);

            try
            {
                var data = HttpClient.DownloadData(request, true);
                return new SteamResponse<byte[]>(data, new RestResponse() { ResponseStatus = ResponseStatus.Completed, StatusCode = HttpStatusCode.OK });
            }
            catch (Exception ex)
            {
                return new SteamResponse<byte[]>(ex);
            }
        }

        public void CopyCookiesForCommunity()
        {
            var storeCookies = CookieContainer.GetCookies(new Uri(SteamDefaultUrls.STORE_BASE));
            CookieContainer.Add(new Uri(SteamDefaultUrls.COMMUNITY_BASE), storeCookies);
        }

        /// <summary>
        /// <seealso cref="Thread.Sleep(int)"/>
        /// </summary>
        internal void LegitDelay()
        {
            if (DisableLegitDelay)
                Thread.Sleep(300); // anyway will be good to add little delay between requests
            else
                Thread.Sleep(Utility.GetRandomNumber(700, 3500));
        }
    }
}
