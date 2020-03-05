using RestSharp;
using SACModuleBase;
using SACModuleBase.Enums.Captcha;
using SACModuleBase.Models;
using SACModuleBase.Models.Capcha;
using SteamAccCreator.Models;
using System;
using System.Text.RegularExpressions;

namespace SteamAccCreator.Web.Captcha.Handlers
{
    public class CaptchaSolutionsHandler : ISACHandlerCaptcha, ISACHandlerReCaptcha
    {
        public static Uri Domain = new Uri("http://api.captchasolutions.com/");

        public bool ModuleEnabled { get; set; } = true;
        public void ModuleInitialize(SACInitialize initialize) { /* It will not be called inside creator */ }

        private CaptchaSolutionsConfig Config;
        private RestClient HttpClient;

        public CaptchaSolutionsHandler(Configuration configuration)
        {
            Config = configuration.Captcha.CaptchaSolutions;
            HttpClient = new RestClient(Domain)
            {
                Timeout = (int)TimeSpan.FromSeconds(40).TotalMilliseconds,
                UserAgent = Defaults.Web.USER_AGENT
            };
            HttpClient.AddDefaultParameter("key", Config.ApiKey);
            HttpClient.AddDefaultParameter("secret", Config.ApiSecret);
            HttpClient.AddDefaultParameter("out", "txt");
        }

        public CaptchaResponse Solve(CaptchaRequest captcha)
        {
            var request = new RestRequest("solve", Method.POST);
            request.AddParameter("p", "base64");
            request.AddParameter("captcha", $"data:image/jpg;base64,{captcha.CaptchaImage}");

            return Solve(request);
        }

        public CaptchaResponse Solve(ReCaptchaRequest captcha)
        {
            var request = new RestRequest("solve", Method.POST);
            request.AddParameter("p", "nocaptcha");
            request.AddParameter("googlekey", captcha.SiteKey);
            request.AddParameter("pageurl", captcha.Url);

            return Solve(request);
        }

        private CaptchaResponse Solve(IRestRequest captcha)
        {
            var response = HttpClient.Execute(captcha);
            if (!response.IsSuccessful)
                return new CaptchaResponse(CaptchaStatus.RetryAvailable, ErrorMessages.CaptchaSolutions.REQUEST_FAILED);

            if (Regex.IsMatch(response.Content ?? "", @"Error:\s(.+)", RegexOptions.IgnoreCase))
            {
                Logger.Warn($"Captchasolutions error:\n{response.Content}\n====== END ======");
                return new CaptchaResponse(CaptchaStatus.RetryAvailable, response.Content);
            }

            var solution = Regex.Replace(response.Content ?? "", @"\t|\n|\r", "");
            Logger.Debug($"CaptchaSolutions: {solution}");
            return new CaptchaResponse(solution);
        }
    }
}
