using RestSharp;
using SACModuleBase;
using SACModuleBase.Enums;
using SACModuleBase.Enums.Captcha;
using SACModuleBase.Models;
using SACModuleBase.Models.Capcha;
using SteamAccCreator.Models;
using System;
using System.Linq;
using System.Threading;

namespace SteamAccCreator.Web.Captcha.Handlers
{
    public class RuCaptchaHandler : ISACHandlerCaptcha, ISACHandlerReCaptcha
    {
        public static Uri Domain = new Uri(Program.UseRuCaptchaDomain ? "http://rucaptcha.com" : "http://2captcha.com");

        public bool ModuleEnabled { get; set; } = true;
        public void ModuleInitialize(SACInitialize initialize) { /* It will not be called inside creator */ }

        private RuCaptchaConfig Config { get; }
        private RestClient HttpClient;

        public RuCaptchaHandler(Configuration configuration)
        {
            Config = configuration.Captcha.RuCaptcha;
            HttpClient = new RestClient(Domain);
            HttpClient.AddDefaultParameter("key", Config.ApiKey);
            HttpClient.AddDefaultParameter("json", 0);
        }

        public CaptchaResponse Solve(CaptchaRequest captcha)
        {
            var requestQueueId = new RestRequest("in.php", Method.POST);
            requestQueueId.AddParameter("soft_id", 2370);
            requestQueueId.AddParameter("body", $"data:image/jpg;base64,{captcha.CaptchaImage}");
            requestQueueId.AddParameter("method", "base64");
            AddProxy(requestQueueId, captcha.Proxy);

            return GetSolution(requestQueueId, false);
        }

        public CaptchaResponse Solve(ReCaptchaRequest captcha)
        {
            var requestQueueId = new RestRequest("in.php", Method.POST);
            requestQueueId.AddParameter("soft_id", 2370);
            requestQueueId.AddParameter("googlekey", captcha.SiteKey);
            requestQueueId.AddParameter("method", "userrecaptcha");
            requestQueueId.AddParameter("pageurl", captcha.Url);
            AddProxy(requestQueueId, captcha.Proxy);

            return GetSolution(requestQueueId, true);
        }

        private void AddProxy(IRestRequest request, Proxy proxy)
        {
            if (proxy == null)
                return;

            if (!Config.TransferProxy)
                return;

            request.AddParameter("proxy", $"{proxy.Host}:{proxy.Port}");
            request.AddParameter("proxytype", (proxy.Type == ProxyType.Unknown) ? "HTTP" : proxy.Type.ToString().ToUpper());
        }

        private CaptchaResponse GetSolution(IRestRequest queueRequest, bool isRecaptcha)
        {
            var responseQueueId = HttpClient.Execute(queueRequest);
            if (!responseQueueId.IsSuccessful)
                return new CaptchaResponse(CaptchaStatus.RetryAvailable, ErrorMessages.RuCaptcha.REQUEST_FAILED);

            var queueAndStatus = responseQueueId.Content.Split(new[] { '|' }, StringSplitOptions.None);
            var status = queueAndStatus?.FirstOrDefault();
            if (string.IsNullOrEmpty(status))
                status = ErrorMessages.UNKNOWN_ERROR;

            Logger.Debug($"TwoCaptcha/RuCaptcha ID response: {status}\n" +
                $"Plain:\n" +
                $"============\n" +
                $"{responseQueueId.Content}\n" +
                $"============");

            switch (status)
            {
                case "OK":
                    break;
                case "ERROR_NO_SLOT_AVAILABLE":
                    Thread.Sleep(6000);
                    return new CaptchaResponse(CaptchaStatus.RetryAvailable, status);
                default:
                    return new CaptchaResponse(CaptchaStatus.Failed, status);
            }
            var id = queueAndStatus.ElementAtOrDefault(1);
            if (string.IsNullOrEmpty(id))
                return new CaptchaResponse(CaptchaStatus.Failed, ErrorMessages.RuCaptcha.QUEUE_ID_EMPTY);

            Logger.Debug($"TwoCaptcha/RuCaptcha ID: {id}");

            Thread.Sleep(TimeSpan.FromSeconds(20));

            var retryCount = isRecaptcha ? 10 : 3;
            for (int i = 0; (Program.EndlessTwoCaptcha) ? true : i < retryCount; i++)
            {
                Logger.Debug($"TwoCaptcha/RuCaptcha requesting solution... Try {i + 1}{(Program.EndlessTwoCaptcha ? "" : $" of {retryCount}")}");

                var solutionRequest = new RestRequest("res.php", Method.POST);
                solutionRequest.AddParameter("action", "get");
                solutionRequest.AddParameter("id", id);
                var solutionResponse = HttpClient.Execute(solutionRequest);
                if (!solutionResponse.IsSuccessful)
                {
                    Logger.Debug("TwoCaptcha/RuCaptcha requesting solution was failed. Retrying...");
                    // we will retry automatically if there is HTTP error.
                    i--;
                    continue;
                }

                var solutionAndStatus = solutionResponse.Content.Split(new[] { '|' }, StringSplitOptions.None);
                status = solutionAndStatus?.FirstOrDefault();
                if (string.IsNullOrEmpty(status))
                    status = ErrorMessages.UNKNOWN_ERROR;

                Logger.Debug($"TwoCaptcha/RuCaptcha solving status: {status}\n" +
                    $"Plain response:\n" +
                    $"============\n" +
                    $"{solutionResponse.Content}\n" +
                    $"============");

                switch (status)
                {
                    case "OK":
                        {
                            var _solution = new CaptchaResponse(solutionAndStatus.ElementAtOrDefault(1) ?? "", id);
                            Logger.Debug($"TwoCaptcha/RuCaptcha solution: {_solution.Solution}");
                            return _solution;
                        }
                    case "CAPCHA_NOT_READY":
                    case "ERROR_NO_SLOT_AVAILABLE":
                        Thread.Sleep(6000);
                        continue;
                    default:
                        return new CaptchaResponse(CaptchaStatus.RetryAvailable, status);
                }
            }

            return new CaptchaResponse(CaptchaStatus.Failed, ErrorMessages.UNKNOWN_ERROR);
        }
    }
}
