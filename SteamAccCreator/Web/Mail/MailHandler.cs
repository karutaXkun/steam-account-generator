using Newtonsoft.Json;
using RestSharp;
using SACModuleBase;
using SACModuleBase.Models;
using SACModuleBase.Models.Mail;
using SteamAccCreator.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace SteamAccCreator.Web.Mail
{
    public class MailHandler : ISACHandlerMailBox
    {
        public static Uri MailboxUri = new Uri(Defaults.Mail.MAILBOX_ADDRESS);
        public static bool IsMailBoxCustom = false;
        public static int CheckUserMailVerifyCount = Defaults.Mail.COUNT_OF_CHECKS_MAIL_USER;
        public static int CheckRandomMailVerifyCount = Defaults.Mail.COUNT_OF_CHECKS_MAIL_AUTO;

        private static Regex ProviderTest = new Regex(@"^\@.+");

        private bool IsCustomProvider { get; }
        private string Provider;

        private RestClient HttpClient;

        public MailHandler(Configuration config)
        {
            IsCustomProvider = ProviderTest.IsMatch(config?.Mail?.Value ?? "");
            if (IsCustomProvider)
                Provider = config.Mail.Value;

            HttpClient = new RestClient(MailboxUri);
        }

        public bool ModuleEnabled { get; set; } = true;

        public MailBoxResponse GetMailBox(MailBoxRequest request)
        {
            if (!IsCustomProvider)
            {
                var provider = GetRandomProvider();
                if (provider == null)
                    return MailBoxResponse.Error(ErrorMessages.Mail.REQUEST_FAILED);

                if (!ProviderTest.IsMatch(provider))
                    return MailBoxResponse.Error(ErrorMessages.Mail.NO_FREE_DOMAIN);

                Provider = provider;
            }

            var mail = $"{request.Login.ToLower()}{Provider}";

            var checkMail = CheckMail((IsCustomProvider) ? mail : request.Login);
            if (!checkMail.Success)
                return MailBoxResponse.Error(checkMail.Comment);

            return new MailBoxResponse(email: $"{request.Login.ToLower()}{Provider}");
        }

        public IReadOnlyCollection<MailBoxMailItem> GetMails(MailBoxResponse response)
        {
            if (!response.Success)
                return new ReadOnlyCollection<MailBoxMailItem>(new MailBoxMailItem[0]);

            var request = new RestRequest((IsCustomProvider) ? "v2" : "", Method.GET);
            request.AddParameter("e", response.Email);
            var inboxResponse = HttpClient.Execute(request);
            if (!inboxResponse.IsSuccessful)
            {
                Logger.Warn($"Cannot communicate to inbox for \"{response.Email}\". HTTP code: {inboxResponse.StatusCode} / message: {inboxResponse.ErrorMessage}");
                return null;
            }

            try
            {
                var inboxMessages = JsonConvert.DeserializeObject<IEnumerable<string>>(inboxResponse.Content);
                return new ReadOnlyCollection<MailBoxMailItem>(
                    inboxMessages.Select(x => new MailBoxMailItem("noreply@steampowered.com", response.Email, "New Steam Account Email Verification", x)).ToList());
            }
            catch (JsonException jEx)
            {
                Logger.Error("Cannot deserialize response content!", jEx);
                return new ReadOnlyCollection<MailBoxMailItem>(new MailBoxMailItem[0]);
            }
            catch (Exception ex)
            {
                Logger.Error("Or something went wrong...", ex);
                return null;
            }
        }

        public void ModuleInitialize(SACInitialize initialize) { /* It will not be called inside creator */ }

        // ----------------- ONLY PRIVATE METHODS BELOW -----------------

        private string GetRandomProvider()
        {
            var request = new RestRequest(Method.GET);
            var response = HttpClient.Execute(request);
            if (!response.IsSuccessful)
                return null;

            if (!ProviderTest.IsMatch(response.Content ?? ""))
                return string.Empty;

            return response.Content;
        }

        private (bool Success, string Comment) CheckMail(string alias)
        {
            var request = new RestRequest((IsCustomProvider) ? "v2" : "", Method.GET);
            request.AddParameter("alias", alias);

            var response = HttpClient.Execute(request);
            if (!response.IsSuccessful)
                return (false, ErrorMessages.Mail.REQUEST_FAILED);

            var isOk = Regex.IsMatch(response.Content ?? "", @"^ok$", RegexOptions.IgnoreCase);
            if (isOk)
                return (true, response.Content);

            if (string.IsNullOrEmpty(response.Content))
                return (false, ErrorMessages.Mail.SERVICE_ERROR);

            return (false, response.Content);
        }
    }
}
