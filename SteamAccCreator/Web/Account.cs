using RestSharp;
using SACModuleBase;
using SACModuleBase.Enums.Captcha;
using SACModuleBase.Models.Capcha;
using SteamAccCreator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using modModels = SACModuleBase.Models;

namespace SteamAccCreator.Web
{
    public class Account
    {
        public static bool DenyNeatOnline = false; // true for deny neat password online
        public static int StopIfCaptchaRequiredMoreThan = 3; // just stop this sh*t. it's endless captcha here
        public static int MaximumOfRetriesOfRegister = 3; // if any http errors
        public static Regex SteamMailConfirmationLink = new Regex("(https?:\\/\\/store\\.steampowered\\.com\\/account\\/newaccountverification\\?[^\"]+)",
            RegexOptions.IgnoreCase);
        public static Regex SteamGuardConfirmationLink = new Regex("(https?:\\/\\/store\\.steampowered\\.com\\/account\\/steamguarddisableverification\\?[^\"]+)",
            RegexOptions.IgnoreCase);
        public static int ConfirmMailLoopMax = 5; // max. of tries to confirm mail
        public static Regex SteamMailConfirmed = new Regex("class=\\\"newaccount_email_verified_text\\\"", RegexOptions.IgnoreCase);
        public static bool SkipSteamGuardDisable = false;
        public static Regex SteamGuardDisabled = new Regex("class=\\\"(email_verified_text\\serror|error\\semail_verified_text)\\\"", RegexOptions.IgnoreCase); // it will not match!
        public static Regex SteamGroupLink = new Regex(@"https?:\/\/steamcommunity\.com\/groups\/([^?#]+)", RegexOptions.IgnoreCase);

        public AccountCreateOptions Options { get; }
        public Configuration Config => Options?.Config;

        private string _Mail;
        private string _Login;
        private string _Password;

        private string _Status;

        public string Mail => _Mail ?? "Initializing...";
        public string Login => _Login ?? "Initializing...";
        public string Password => _Password ?? "Initializing...";
        public long? SteamIdLong => Steam?.SteamId;
        public string SteamId => SteamIdLong?.ToString() ?? "unknown";
        public string Status => _Status ?? "Initializing...";


        private bool IsValidToRegister = true;

        private Steam.SteamWebClient Steam;
        private RestClient HttpClient;

        private ISACHandlerMailBox HandlerMail;
        private ISACHandlerCaptcha HandlerImageCaptcha;
        private ISACHandlerReCaptcha HandlerGooleCaptcha;

        private modModels.Mail.MailBoxResponse MailBoxResponse;
        private bool IsMailInHandMode => MailBoxResponse == null;
        private int MailVerifyMaxRetry => (IsMailInHandMode)
            ? Web.Mail.MailHandler.CheckUserMailVerifyCount
            : Web.Mail.MailHandler.CheckRandomMailVerifyCount;

        public Account(AccountCreateOptions options)
        {
            Options = options;

            var handlerUserAgent = options?.HandlerUserAgent ?? new OfflineHandlers.UserAgentHandler();

            var userAgent = handlerUserAgent.GetUserAgent();

            HttpClient = new RestClient("https://127.0.0.1/") // cuz this ask base even if you put full link in request object
            {
                CookieContainer = new CookieContainer(),
                UserAgent = userAgent,
                FollowRedirects = true, // we will...
            };

            Steam = new Steam.SteamWebClient(HttpClient);

            if (Config.Login.Random)
                RandomizeLogin();
            else
                _Login = $"{Config.Login.Value}{options.AccountNumber}";

            HandlerMail = options.HandlerMailBox ?? new Mail.MailHandler(Config);
            var localCaptchaHandler = new Captcha.Handlers.LocalCaptchaHandler(options);
            HandlerImageCaptcha = options.HandlerImageCaptcha ?? localCaptchaHandler;
            HandlerGooleCaptcha = options.HandlerGoogleCaptcha ?? localCaptchaHandler;

            Debug($"User-Agent={userAgent}");
        }

        private void RandomizeLogin()
        {
            if (Config.Login.Neat)
            {
                var words = new List<string>();
                words.AddRange(Words.Adj.RandomElement(3).Select(x => x.ToTitleCase()));
                words.Add(Words.Animals.RandomElement());

                _Login = string.Join("", words)
                    + Utility.GetRandomNumberRng(9999).ToString("0000"); // avoid collisions
            }
            else
            {
                _Login = Utility.GetRandomString(18);
            }
        }

        private async Task RandomizePassword()
        {
            _Password = (Config.Password.Neat)
                ? await NeatPasswordOnline()
                : GetPasswordUsingRng();
        }
        private string GetPasswordUsingRng()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var data = new byte[18];
                rng.GetNonZeroBytes(data);
                return Convert.ToBase64String(data);
            }
        }
        private string NeatPasswordOffline()
        {
            var words = new List<string>();
            words.AddRange(Words.Adj.RandomElement(3).Select(x => x.ToTitleCase()));
            words.Add(Words.Animals.RandomElement());

            return string.Join("", words) + Utility.GetRandomString(2)
                + Utility.GetRandomNumberRng(9999).ToString("0000"); // avoid collisions
        }
        private async Task<string> NeatPasswordOnline()
        {
            if (DenyNeatOnline)
                return NeatPasswordOffline();

            try
            {
                var httpClient = new RestClient("https://makemeapassword.ligos.net/");
                var request = new RestRequest("api/v1/passphrase/plain", Method.GET);
                request.AddParameter("pc", "1");
                request.AddParameter("wc", "3");
                request.AddParameter("sp", "n");
                request.AddParameter("maxCh", "30");
                var response = await Task.Run(() => httpClient.Execute(request));
                if (!response.IsSuccessful)
                    return NeatPasswordOffline(); // if no connection or something else

                var password = response.Content.Trim();
                if (Regex.IsMatch(password, @"automatically\swithin\s(\d+)\shour", RegexOptions.IgnoreCase))
                {
                    DenyNeatOnline = true; // disable online neating
                    password = NeatPasswordOffline();
                }
                else
                    return password + Utility.GetRandomString(2) + Utility.GetRandomNumber(100, 1000);

                return password;
            }
            catch (Exception ex)
            {
                DenyNeatOnline = true; // disable online neating
                Logger.Error("Online neat password error. Disaled, neating offline.", ex);
                return NeatPasswordOffline();
            }
        }

        private async Task PrepareToRegister()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            if (Config.Password.Random)
                await RandomizePassword();
            else if (string.IsNullOrEmpty(Config.Password.Value) || Config.Password.Value.Length < 8)
            {
                Logger.Warn("Password is less than 8 symbols or empty. " +
                    $"Generating password {nameof(GetPasswordUsingRng)}() method...");

                _Password = GetPasswordUsingRng();
            }
            else
                _Password = Config.Password.Value;

            if (Config.Mail.Random)
            {
                MailBoxResponse = await Task.Run(() => HandlerMail.GetMailBox(
                    new modModels.Mail.MailBoxRequest(Login, default)));

                if (!MailBoxResponse.Success)
                {
                    UpdateStatus(MailBoxResponse?.Comment ?? "Failed to get email...", State.Failed);
                    IsValidToRegister = false;
                    return;
                }

                _Mail = MailBoxResponse.Email;
            }
            else
            {
                if (!Regex.IsMatch(Config.Mail.Value ?? "", @"\S+\@.+")) // simple check for mail
                {
                    Logger.Warn("Random mail is disabled, no mail specified. " +
                        "IDK how GUI was passed this sh*t here.");

                    UpdateStatus("Something went wrong with email.", State.Failed);
                    IsValidToRegister = false;
                    return;
                }

                _Mail = Config.Mail.Value;
            }
        }

        private bool UpdateProxy()
        {
            if (!(Options?.ProxyManager?.Enabled ?? false))
            {
                Warn("Proxy is disabled or something went wrong. Cannot update.");
                return false;
            }

            if (!Options.ProxyManager.GetNew())
            {
                Warn("Cannot update proxy.");
                return false;
            }

            var proxy = Options.ProxyManager.Current;
            if (proxy == null)
            {
                Warn("Proxy is updated but current is null");
                return false;
            }

            Debug($"Proxy updated. Current is {proxy}");
            HttpClient.Proxy = proxy?.ToWebProxy();

            return true;
        }

        private int HttpErrorRetryCount = 0;
        public async Task Register(bool skipPrepare = false)
        {
            if (HttpErrorRetryCount > MaximumOfRetriesOfRegister)
            {
                if (!IsValidToRegister)
                    return;

                UpdateStatus(ErrorMessages.Account.CONNECTION_UNSTABLE, State.Failed);

                return;
            }

            UpdateProxy();

            if (!skipPrepare)
            {
                UpdateStatus("Prepating account...", State.Processing);
                await PrepareToRegister();
            }

            Options?.RefreshDisplayFn();

            if (!IsValidToRegister)
            {
                SwitchState(State.Failed);
                return;
            }

            UpdateStatus("Requesting join page...", State.Processing);
            var initialRequest = await Task.Run(() => Steam.Join.GetJoinPage());
            if (!initialRequest.Response)
            {
                if (initialRequest.IsSuccess)
                {
                    UpdateStatus(ErrorMessages.Account.INITIAL_REQUEST_ERROR, State.Failed);
                    Warn(ErrorMessages.Account.INITIAL_REQUEST_ERROR);
                    return;
                }
                else if (initialRequest.Exception != null)
                {
                    UpdateStatus(ErrorMessages.Account.INITIAL_REQUEST_ERROR, State.Failed);
                    Error(ErrorMessages.Account.INITIAL_REQUEST_ERROR, initialRequest.Exception);
                    return;
                }
                else
                {
                    UpdateStatus(ErrorMessages.Account.INITIAL_REQUEST_HTTP_ERROR, State.Failed);
                    Warn(ErrorMessages.Account.INITIAL_REQUEST_HTTP_ERROR);
                    return;
                }
            }

            var creationAccountId = string.Empty;
            var isEndlessCaptcha = false;
            for (int i = 0; i < StopIfCaptchaRequiredMoreThan; i++)
            {
                isEndlessCaptcha = i + 1 >= StopIfCaptchaRequiredMoreThan;

                UpdateStatus($"[Try {i + 1}/{StopIfCaptchaRequiredMoreThan}] Waiting for captcha solution...", State.Processing);
                Info($"[Try {i + 1}/{StopIfCaptchaRequiredMoreThan}] Waiting for captcha solution...");

                var captcha = await Task.Run(() => Steam.Join.RefreshCaptcha(i + 1));
                if (!captcha.IsSuccess)
                {
                    if (captcha.Exception == null)
                    {
                        Warn(ErrorMessages.Account.CAPTCHA_GET_HTTP_ERROR);
                        i--; // cuz http error
                        continue;
                    }
                    else
                    {
                        UpdateStatus(ErrorMessages.Account.CAPTCHA_GET_ERROR, State.Failed);
                        Error(ErrorMessages.Account.CAPTCHA_GET_ERROR, captcha.Exception);
                        return;
                    }
                }

                var currentProxy = Options.ProxyManager.Current;

                var isRecaptcha = captcha.Response.Type == 2;
                var solution = new CaptchaResponse(CaptchaStatus.Failed, "Something went wrong with captcha...");
                if (isRecaptcha)
                {
                    solution = await Task.Run(() => HandlerGooleCaptcha?.Solve(
                        new ReCaptchaRequest(captcha.Response.SiteKey, Web.Steam.SteamDefaultUrls.JOIN, currentProxy?.ToModuleProxy())) ??
                            new CaptchaResponse(CaptchaStatus.Failed, "Can't find any solver for Google captcha!"));
                }
                else
                {
                    var data = await Task.Run(() => Steam.DownloadCaptchaImage(captcha.Response));
                    if (!data.IsSuccess)
                    {
                        if (data.Exception == null)
                        {
                            Warn(ErrorMessages.Account.CAPTCHA_DOWNLOAD_IMAGE_HTTP_ERROR);
                            i--;
                            continue; // retry?
                        }
                        else
                        {
                            _Status = ErrorMessages.Account.CAPTCHA_DOWNLOAD_IMAGE_ERROR;
                            Error(ErrorMessages.Account.CAPTCHA_DOWNLOAD_IMAGE_ERROR, data.Exception);
                            return;
                        }
                    }

                    try
                    {
                        var base64Image = Convert.ToBase64String(data.Response);

                        solution = await Task.Run(() => HandlerImageCaptcha?.Solve(
                            new CaptchaRequest(base64Image, currentProxy?.ToModuleProxy())) ??
                                new CaptchaResponse(CaptchaStatus.Failed, "Can't find any solver for image captcha!"));
                    }
                    catch (Exception ex)
                    {
                        Error(ErrorMessages.Account.CAPTCHA_PROCESSING_ERROR, ex);
                        return;
                    }
                }

                Info($"Captcha solved, solution:" +
                    $"\nStatus: {solution.Status}" +
                    $"\nText: {solution.Solution ?? "-"}" +
                    $"\nMessage: {solution.Message ?? "-"}" +
                    $"\nAny identify?: {((solution.Identify == null) ? "No" : "Yes")}");

                switch (solution.Status)
                {
                    case CaptchaStatus.CannotSolve: // will change to another module that can and retry...
                    case CaptchaStatus.Failed: // that's bad. stop creating
                        UpdateStatus(solution.Message, State.Failed);
                        return;
                    case CaptchaStatus.Success:
                        break; // fine!
                    case CaptchaStatus.RetryAvailable:
                        continue; // so... okay, retry
                }

                UpdateStatus("Requesting verification mail...", State.Processing);

                var verifyEmail = await Task.Run(() => Steam.Join.VerifyEmail(Mail, captcha.Response.Gid, solution.Solution));
                if (!verifyEmail.IsSuccess)
                {
                    if (verifyEmail.Exception == null)
                    {
                        UpdateStatus(ErrorMessages.Account.VF_MAIL_HTTP_ERROR, State.Processing);
                        Warn(ErrorMessages.Account.VF_MAIL_HTTP_ERROR);
                        continue; // retry?
                    }
                    else
                    {
                        UpdateStatus(ErrorMessages.Account.VF_MAIL_ERROR, State.Failed);
                        Error(ErrorMessages.Account.VF_MAIL_ERROR, verifyEmail.Exception);
                        return;
                    }
                }

                switch (verifyEmail.Response.Status)
                {
                    case 1:
                        Debug($"#{verifyEmail.Response.Status} = Waiting for mail confirmation...");
                        UpdateStatus("Waiting for mail confirmation...", State.Processing);
                        creationAccountId = verifyEmail.Response.CreationId;
                        break;
                    case 13:
                        Warn($"#{verifyEmail.Response.Status} = {ErrorMessages.Steam.INVALID_MAIL}");
                        UpdateStatus(ErrorMessages.Steam.INVALID_MAIL, State.Failed);
                        return;
                    case 17:
                        Warn($"#{verifyEmail.Response.Status} = {ErrorMessages.Steam.TRASH_MAIL}");
                        UpdateStatus(ErrorMessages.Steam.TRASH_MAIL, State.Failed);
                        return;
                    case 62:
                        Warn($"#{verifyEmail.Response.Status} = {ErrorMessages.Steam.SIMILIAR_MAIL}");
                        UpdateStatus(ErrorMessages.Steam.SIMILIAR_MAIL, State.Failed);
                        return;
                    case 84:
                        Warn($"#{verifyEmail.Response.Status} = {ErrorMessages.Steam.PROBABLY_IP_BAN}");
                        if (UpdateProxy())
                        {
                            UpdateStatus(ErrorMessages.Steam.PROBABLY_IP_BAN, State.Processing);
                            await Task.Delay(300);
                            continue; // try again?
                        }

                        UpdateStatus(ErrorMessages.Steam.PROBABLY_IP_BAN, State.Failed);
                        Warn("Stopped creation.");
                        return;
                    case 101: // Please verify your humanity by re-entering the characters below.

                        //if (captcha.Config != null)
                        //{
                        //    if (captcha.Config.Service == Enums.CaptchaService.RuCaptcha &&
                        //        captcha.Config.RuCaptcha.ReportBad)
                        //    {
                        //        TwoCaptchaReport(captcha, false);
                        //    }
                        //}

                        Warn($"#{verifyEmail.Response.Status} = {ErrorMessages.Steam.WRONG_CAPTCHA}");
                        if (UpdateProxy())
                        {
                            UpdateStatus(ErrorMessages.Steam.WRONG_CAPTCHA, State.Processing);
                            await Task.Delay(300);
                            continue; // try again?
                        }

                        UpdateStatus(ErrorMessages.Steam.WRONG_CAPTCHA, State.Failed);
                        Warn("Stopped creation.");
                        return;
                    default:
                        Warn($"--/default_case:{verifyEmail.Response.Details ?? "no_more_info"}/-- #{verifyEmail.Response.Status} = {ErrorMessages.Steam.UNKNOWN}");
                        if (UpdateProxy())
                        {
                            UpdateStatus(ErrorMessages.Steam.UNKNOWN, State.Processing);
                            await Task.Delay(300);
                            continue; // try again?
                        }

                        UpdateStatus(ErrorMessages.Steam.UNKNOWN, State.Failed);
                        Warn(ErrorMessages.Account.FAILED_TO_CREATE_FATAL);
                        return;
                }

                break; // exit from loop
            }

            if (string.IsNullOrEmpty(creationAccountId))
            {
                if (isEndlessCaptcha)
                {
                    Warn(ErrorMessages.Account.CAPTCHA_LOOP_DETECTED);
                    UpdateStatus(ErrorMessages.Account.CAPTCHA_LOOP_DETECTED, State.Failed);
                    return;
                }

                Warn(ErrorMessages.Account.CREATION_ID_NOT_FOUND);
                if (string.IsNullOrEmpty(_Status))
                    UpdateStatus(ErrorMessages.Account.CREATION_ID_NOT_FOUND, State.Failed);

                return;
            }

            /*
            For exaple:
            - CheckUserMailVerifyCount by default is 120
            - We waiting for 30 seconds per request

            30*120 = 3600 then 3600/60=60 and then 60/60=1

            so.. we have about 1 hour to verify mail.
            idk about default Steam delay to confirm mail
            */

            var _mailConfirmStatusMessage = (IsMailInHandMode)
                ? ErrorMessages.Steam.MAIL_UNVERIFIED_HAND_MODE
                : ErrorMessages.Steam.MAIL_UNVERIFIED;

            UpdateStatus(_mailConfirmStatusMessage, State.Processing);
            for (int i = 0; i < MailVerifyMaxRetry; i++)
            {
                if (!IsMailInHandMode)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3));

                    var link = await GetConfirmLinkFromMail(creationAccountId);
                    if (!link.Success)
                        continue; // we will try again...

                    var confirmLinkState = await LoadConfirmLink(link.Link);
                    if (!confirmLinkState)
                        return;
                }
                else
                    await Task.Delay(TimeSpan.FromSeconds(30));

                var checkMailConfirm = await Task.Run(() => Steam.Join.CheckEmailVerified(creationAccountId));
                if (!checkMailConfirm.IsSuccess)
                {
                    i--;
                    if (checkMailConfirm.Exception == null)
                    {
                        UpdateStatus(ErrorMessages.Account.MCF_CHECK_HTTP_ERROR, State.Failed);
                        Warn($"--/hand_mode/-- {ErrorMessages.Account.MCF_CHECK_HTTP_ERROR}");
                    }
                    else
                    {
                        UpdateStatus(ErrorMessages.Account.MCF_CHECK_ERROR, State.Failed);
                        Error($"--/hand_mode/-- {ErrorMessages.Account.MCF_CHECK_ERROR}", checkMailConfirm.Exception);
                    }

                    await Task.Delay(2000);

                    continue;
                }

                switch (checkMailConfirm.Response)
                {
                    case "1":
                        Debug("Mail has been confirmed...");
                        UpdateStatus("Mail has been confirmed...", State.Processing);
                        break;
                    case "10":
                    case "36":
                        Warn($"#{checkMailConfirm.Response} = {_mailConfirmStatusMessage}");
                        UpdateStatus(_mailConfirmStatusMessage, State.Processing);
                        continue;
                    case "27":
                        Warn($"#{checkMailConfirm.Response} = {ErrorMessages.Steam.TIMEOUT}");
                        UpdateStatus(ErrorMessages.Steam.TIMEOUT, State.Failed);
                        return;
                    case "29":
                    case "42":
                        Warn($"#{checkMailConfirm.Response} = {ErrorMessages.Steam.REGISTRATION}");
                        UpdateStatus(ErrorMessages.Steam.REGISTRATION, State.Failed);
                        return;
                    default:
                        Warn($"#{checkMailConfirm.Response} = {ErrorMessages.Steam.UNKNOWN}");
                        if (UpdateProxy())
                        {
                            UpdateStatus(ErrorMessages.Steam.UNKNOWN, State.Processing);
                            continue; // try again?
                        }

                        UpdateStatus(ErrorMessages.Steam.UNKNOWN, State.Failed);
                        Warn(ErrorMessages.Account.FAILED_TO_CREATE_FATAL);
                        return;
                }

                break; // to exit from loop
            }

            int accountCount = 0;

            #region fn's inside
            async Task<bool> checkLoginFn()
            {
                UpdateStatus("Checking Steam login...", State.Processing);

                accountCount++;
                for (int i = 0; i < MaximumOfRetriesOfRegister; i++)
                {
                    var checkLogin = await Task.Run(() => Steam.Join.CheckLogin(_Login, accountCount));
                    if (!checkLogin.IsSuccess)
                    {
                        if (checkLogin.Exception == null)
                        {
                            Warn($"--/Try {i + 1} of {MaximumOfRetriesOfRegister}/-- {ErrorMessages.Account.LOGIN_CHECK_FAILED}");
                            UpdateStatus($"[Try {i + 1}/{MaximumOfRetriesOfRegister}] {ErrorMessages.Account.LOGIN_CHECK_FAILED}", State.Processing);
                        }
                        else
                        {
                            Error($"--/Try {i + 1} of {MaximumOfRetriesOfRegister}/-- {ErrorMessages.Account.LOGIN_CHECK_FAILED_FATAL}", checkLogin.Exception);
                            UpdateStatus($"[Try {i + 1}/{MaximumOfRetriesOfRegister}] {ErrorMessages.Account.LOGIN_CHECK_FAILED_FATAL}", State.Processing);
                        }
                        continue;
                    }

                    if (checkLogin.Response == null)
                    {
                        Warn($"--/Try {i + 1} of {MaximumOfRetriesOfRegister}/-- {ErrorMessages.Account.LOGIN_CHECK_FAILED}");
                        UpdateStatus($"[Try {i + 1}/{MaximumOfRetriesOfRegister}] {ErrorMessages.Account.LOGIN_CHECK_FAILED}", State.Processing);
                        accountCount++;
                        continue; // idk why it can be so... but go retry...
                    }

                    if (checkLogin.Response.IsAvailable)
                        return true;

                    if ((checkLogin.Response.Suggestions?.Any(x => string.IsNullOrEmpty(x)) ?? true))
                        _Login += Utility.GetRandomNumberRng(99).ToString("00"); // self fix login
                    else
                        _Login = checkLogin.Response.Suggestions.RandomElement(); // fix login by valve suggestion

                    accountCount++;
                }

                SwitchState(State.Failed);
                return false;
            }

            async Task<bool> checkPasswordFn()
            {
                UpdateStatus("Checking password...", State.Processing);

                accountCount++;
                for (int i = 0; i < MaximumOfRetriesOfRegister; i++)
                {
                    var checkPassword = await Task.Run(() => Steam.Join.CheckPassword(_Password, _Login, accountCount));
                    if (!checkPassword.IsSuccess)
                    {
                        if (checkPassword.Exception == null)
                        {
                            Warn($"--/Try {i + 1} of {MaximumOfRetriesOfRegister}/-- {ErrorMessages.Account.PASSWORD_CHECK_FAILED}");
                            UpdateStatus($"[Try {i + 1}/{MaximumOfRetriesOfRegister}] {ErrorMessages.Account.PASSWORD_CHECK_FAILED}", State.Processing);
                        }
                        else
                        {
                            Error($"--/Try {i + 1} of {MaximumOfRetriesOfRegister}/-- {ErrorMessages.Account.PASSWROD_CHECK_FAILED_FATAL}", checkPassword.Exception);
                            UpdateStatus($"[Try {i + 1}/{MaximumOfRetriesOfRegister}] {ErrorMessages.Account.PASSWROD_CHECK_FAILED_FATAL}", State.Processing);
                        }
                        continue;
                    }

                    if (checkPassword.Response == null)
                    {
                        Warn($"--/Try {i + 1} of {MaximumOfRetriesOfRegister}/-- {ErrorMessages.Account.PASSWORD_CHECK_FAILED}");
                        UpdateStatus($"[Try {i + 1}/{MaximumOfRetriesOfRegister}] {ErrorMessages.Account.PASSWORD_CHECK_FAILED}", State.Processing);
                        accountCount++;
                        continue; // idk why it can be so... but go retry...
                    }

                    if (checkPassword.Response.IsAvailable)
                        return true;

                    if (Config.Password.Random)
                    {
                        await RandomizePassword(); // make new password
                        accountCount++;
                        continue; // and retry
                    }

                    accountCount++;
                }

                SwitchState(State.Failed);
                return false;
            }
            #endregion

            if (!(await checkLoginFn()))
            {
                Warn(ErrorMessages.Account.FAILED_TO_CREATE_FATAL);
                return;
            }
            if (!(await checkPasswordFn()))
            {
                Warn(ErrorMessages.Account.FAILED_TO_CREATE_FATAL);
                return;
            }

            accountCount++;
            var accountCreated = false;
            for (int i = 0; i < MaximumOfRetriesOfRegister; i++)
            {
                UpdateStatus("Creating account...", State.Processing);
                var createAccount = await Task.Run(() => Steam.Join.CreateAccount(_Login, _Password, creationAccountId, accountCount));
                if (!createAccount.IsSuccess)
                {
                    if (createAccount.Exception == null)
                    {
                        Warn($"--/Try {i + 1} of {MaximumOfRetriesOfRegister}/-- {ErrorMessages.Account.CREATE_FAILED}");
                        UpdateStatus($"[Try {i + 1}/{MaximumOfRetriesOfRegister}] {ErrorMessages.Account.CREATE_FAILED}", State.Processing);
                    }
                    else
                    {
                        Error($"--/Try {i + 1} of {MaximumOfRetriesOfRegister}/-- {ErrorMessages.Account.CREATE_FAILED_FATAL}", createAccount.Exception);
                        UpdateStatus($"[Try {i + 1}/{MaximumOfRetriesOfRegister}] {ErrorMessages.Account.CREATE_FAILED_FATAL}", State.Processing);
                    }
                    continue;
                }

                if (createAccount.Response == null)
                {
                    Warn($"--/Try {i + 1} of {MaximumOfRetriesOfRegister}/-- {ErrorMessages.Account.CREATE_FAILED}");
                    UpdateStatus($"[Try {i + 1}/{MaximumOfRetriesOfRegister}] {ErrorMessages.Account.CREATE_FAILED}", State.Processing);
                    accountCount++;
                    await Task.Delay(3000);
                    continue;
                }

                accountCreated = createAccount.Response.IsSuccess;
                if (!accountCreated)
                {
                    Warn($"--/Try {i + 1} of {MaximumOfRetriesOfRegister} :: er=#{createAccount.Response.EnumResult}/-- {ErrorMessages.Account.CREATE_FAILED_FATAL}");
                    UpdateStatus($"({createAccount.Response.EnumResult}) {ErrorMessages.Account.CREATE_FAILED}", State.Failed);
                    return;
                }

                break;
            }
            if (!accountCreated)
            {
                SwitchState(State.Failed);
                Warn(ErrorMessages.Account.FAILED_TO_CREATE_FATAL);
                return;
            }

            UpdateStatus("Account almost created...", State.Processing);

            await Task.Delay(3000);

            var isRedirected = false;
            for (int i = 0; i < MaximumOfRetriesOfRegister; i++)
            {
                var redirectResponse = Steam.Join.CallRedirectUrl(creationAccountId);
                if (!redirectResponse.Response)
                {
                    if (!redirectResponse.IsSuccess)
                    {
                        if (redirectResponse.Exception == null)
                        {
                            UpdateStatus($"[Try {i + 1}/{MaximumOfRetriesOfRegister}] {ErrorMessages.Account.CREATE_REDIRECT_FAILED}", State.Processing);
                            Warn($"--/Try {i + 1} of {MaximumOfRetriesOfRegister}/-- {ErrorMessages.Account.CREATE_REDIRECT_FAILED}");
                        }
                        else
                        {
                            UpdateStatus($"--/Try {i + 1} of {MaximumOfRetriesOfRegister}/-- {ErrorMessages.Account.CREATE_REDIRECT_FAILED_FATAL}", State.Processing);
                            Error($"--/Try {i + 1} of {MaximumOfRetriesOfRegister}/-- {ErrorMessages.Account.CREATE_REDIRECT_FAILED_FATAL}", redirectResponse.Exception);
                        }
                    }
                    continue;
                }

                isRedirected = true;
                break;
            }
            if (!isRedirected)
            {
                SwitchState(State.Failed);
                Warn(ErrorMessages.Account.FAILED_TO_CREATE_FATAL);
                return;
            }

            Steam.CopyCookiesForCommunity();

            await Task.Delay(3000);

            var isGuardDisabled = false;
            if (!SkipSteamGuardDisable)
            {
                var disableGuard = await DisableSteamGuard();
                if (!disableGuard.Success)
                {
                    if (disableGuard.IsFatal)
                    {
                        SwitchState(State.Failed);
                        Warn(ErrorMessages.Account.FAILED_TO_CREATE_FATAL);
                        return;
                    }
                }
                else
                    isGuardDisabled = true;
            }

            var activatedLicenses = 0;
            var doLicensesActivation = Config.Games.AddGames && (Config?.Games?.GamesToAdd?.Count() ?? 0) > 0;
            if (doLicensesActivation)
            {
                UpdateStatus("Starting activating licenses...", State.Processing);

                await Task.Delay(3000);

                activatedLicenses = await ActivateLicenses();
            }

            await Task.Delay(3000);

            var isProfileUpdated = false;
            var setProfilePhotoState = ProfilePhotoState.FileNotSet;
            if ((Config?.Profile?.Enabled ?? false))
            {
                isProfileUpdated = await UpdateProfileInformation();

                await Task.Run(Steam.LegitDelay);

                setProfilePhotoState = await SetProfilePhoto();
            }

            var groupsJoined = 0;
            var doGroupsJoin = Config.Profile.DoJoinToGroups && (Config?.Profile?.GroupsToJoin?.Count() ?? 0) > 0;
            if (doGroupsJoin)
            {
                await Task.Run(Steam.LegitDelay);
                groupsJoined = await JoinToGroups();
            }

            var _lastState = SaveAccount();

            var actionsDone = new Dictionary<string, string>();

            if (!SkipSteamGuardDisable)
                actionsDone.Add("Guard", (isGuardDisabled) ? "Off" : "ON!");
            if ((Config?.Profile?.Enabled ?? false))
            {
                actionsDone.Add("Profile", (isProfileUpdated) ? "OK" : "Fail!");
                switch (setProfilePhotoState)
                {
                    case ProfilePhotoState.Success:
                        actionsDone.Add("Photo", "OK");
                        break;
                    case ProfilePhotoState.FileNotFound:
                        actionsDone.Add("Profile", "Lost file!");
                        break;
                    case ProfilePhotoState.RequestError:
                        actionsDone.Add("Profile", "Fail!");
                        break;
                    case ProfilePhotoState.FileNotSet:
                    default:
                        break;
                }
            }
            if (doGroupsJoin)
                actionsDone.Add("Groups", $"{groupsJoined}/{Config?.Profile?.GroupsToJoin?.Count() ?? 0}");
            if (doLicensesActivation)
                actionsDone.Add("Licenses", $"{activatedLicenses}/{Config?.Games?.GamesToAdd?.Count() ?? 0}");

            var completeStatus = "Done.";
            switch (_lastState)
            {
                case State.NotSavedToFile:
                    completeStatus = "Done but failed to save account.";
                    break;
                case State.NotSavedToFileDisabled:
                    completeStatus = "Done but not saved to file.";
                    break;
            }

            UpdateStatus($"{completeStatus} [{string.Join(";", actionsDone.Select(x => $"{x.Key}={x.Value}"))}]", _lastState);
        }

        private State SaveAccount()
        {
            if (!Config.Output.Enabled)
                return State.NotSavedToFileDisabled;

            var fileHandler = new File.FileHandler(this, (s) => UpdateStatus(s, State.Processing), Info, Warn, Error);
            if (fileHandler.Save())
                return State.Success;
            else
                return State.NotSavedToFile;
        }

        private async Task<int> JoinToGroups()
        {
            UpdateStatus("Gonna join to groups......", State.Processing);

            var joined = 0;

            var groups = Config?.Profile?.GroupsToJoin ?? new string[0];
            foreach (var group in groups)
            {
                var regex = SteamGroupLink.Match(group);
                if (!regex.Success)
                {
                    Warn($"Cannot parse group url.\n{group}");
                    continue;
                }

                var groupLink = regex.Groups[1].Value;
                UpdateStatus($"Joining to group: '{groupLink}'...", State.Processing);
                var response = await Task.Run(() => Steam.Account.JoinGroup(groupLink));
                if (response.IsSuccess)
                {
                    if (response.Response)
                    {
                        Info($"Joining to group: '{groupLink}' success!");
                        UpdateStatus($"Joining to group: '{groupLink}' success!", State.Processing);

                        joined++;
                    }
                    else
                    {
                        Info($"Joining to group: '{groupLink}' failed.");
                        UpdateStatus($"Cannot join to group '{groupLink}'!", State.Processing);
                    }

                    if (group != groups.Last())
                        await Task.Delay(1500);

                    continue;
                }

                if (response.Exception == null)
                {
                    Warn($"Request to join was failed.\nGroup: {groupLink}");
                    UpdateStatus($"[Group={groupLink}] Request to join was failed.", State.Failed);
                }
                else
                {
                    Warn($"Request to join was failed.\nGroup: {groupLink}");
                    UpdateStatus($"[Group={groupLink}] Request to join was failed.", State.Failed);
                }

                if (group != groups.Last())
                    await Task.Delay(1500);
            }

            return joined;
        }

        private async Task<bool> UpdateProfileInformation()
        {
            UpdateStatus("Updating profile information...", State.Processing);

            var profile = Config.Profile;
            var response = await Task.Run(() => Steam.Account.EditProfile(
                profile.Name,
                profile.RealName,
                profile.Country,
                profile.State,
                profile.City,
                (profile.Url) ? _Login : string.Empty,
                profile.Bio));

            if (response.IsSuccess)
                return true;

            if (response.Exception == null)
            {
                Warn(ErrorMessages.Account.PROFILE_UPDATE_FAILED);
                UpdateStatus(ErrorMessages.Account.PROFILE_UPDATE_FAILED, State.Failed);
            }
            else
            {
                Error(ErrorMessages.Account.PROFILE_UPDATE_FAILED_FATAL, response.Exception);
                UpdateStatus(ErrorMessages.Account.PROFILE_UPDATE_FAILED_FATAL, State.Failed);
            }

            return false;
        }
        private async Task<ProfilePhotoState> SetProfilePhoto()
        {
            if (string.IsNullOrEmpty(Config?.Profile?.Image))
            {
                Info("No profile photo is set!");
                return ProfilePhotoState.FileNotSet;
            }
            if (!System.IO.File.Exists(Config?.Profile?.Image))
            {
                Warn($"Cannot find profile image at '{Config?.Profile?.Image ?? "..."}'!");
                return ProfilePhotoState.FileNotFound;
            }

            UpdateStatus("Uploading profile photo...", State.Processing);

            var response = await Task.Run(() => Steam.Account.UploadAvatar(Config.Profile.Image));
            if (response.IsSuccess)
                return ProfilePhotoState.Success;

            if (response.Exception == null)
            {
                Warn(ErrorMessages.Account.AVATAR_UPLOAD_FAILED);
                UpdateStatus(ErrorMessages.Account.AVATAR_UPLOAD_FAILED, State.Failed);
            }
            else
            {
                Error(ErrorMessages.Account.AVATAR_UPLOAD_FAILED_FATAL, response.Exception);
                UpdateStatus(ErrorMessages.Account.AVATAR_UPLOAD_FAILED_FATAL, State.Failed);
            }

            return ProfilePhotoState.RequestError;
        }

        private async Task<int> ActivateLicenses()
        {
            var activated = 0;

            var licensesList = Config?.Games?.GamesToAdd ?? new GameInfo[0];
            foreach (var licenseInfo in licensesList)
            {
                Debug($"Trying to activate license {{id={licenseInfo.SubId};name={licenseInfo.Name}}}...");
                UpdateStatus($"Activating license ({licenseInfo.SubId}) {licenseInfo.Name}...", State.Processing);

                var response = await Task.Run(() => Steam.Account.AddFreeLicense(licenseInfo.SubId));
                if (response.IsSuccess)
                {
                    if (response.Response)
                    {
                        activated++;
                        Debug($"License activated {{id={licenseInfo.SubId};name={licenseInfo.Name}}}");
                        UpdateStatus($"Activating license ({licenseInfo.SubId}) {licenseInfo.Name} success!", State.Processing);
                    }
                    else
                    {
                        Warn($"Cannot confirm that activate license {{id={licenseInfo.SubId};name={licenseInfo.Name}}} is successful!");
                        UpdateStatus($"Can't confirm that license is activated ({licenseInfo.SubId}) {licenseInfo.Name}!", State.Failed);
                    }
                }
                else
                {
                    if (response.Exception == null)
                    {
                        Warn($"Failed to activate license {{id={licenseInfo.SubId};name={licenseInfo.Name}}}!");
                        UpdateStatus($"Failed to activate license ({licenseInfo.SubId}) {licenseInfo.Name}!", State.Failed);
                    }
                    else
                    {
                        Warn($"Cannot activate license {{id={licenseInfo.SubId};name={licenseInfo.Name}}}!");
                        UpdateStatus($"Cannot activate license ({licenseInfo.SubId}) {licenseInfo.Name}!", State.Failed);
                    }
                }

                if (licenseInfo != licensesList.Last())
                    await Task.Delay(300);
            }

            return activated;
        }

        private async Task<(bool Success, bool IsFatal)> DisableSteamGuard()
        {
            UpdateStatus("Enabling Steam guard...", State.Processing);

            var tfEnableResponse = await Task.Run(() => Steam.TwoFactor.TurnOnByMail());
            if (!tfEnableResponse.IsSuccess)
            {
                if (tfEnableResponse.Exception == null)
                {
                    Warn(ErrorMessages.Account.TF_FAILED_TO_ENABLE);
                    UpdateStatus(ErrorMessages.Account.TF_FAILED_TO_ENABLE, State.Failed);
                }
                else
                {
                    Error(ErrorMessages.Account.TF_FAILED_TO_ENABLE_FATAL, tfEnableResponse.Exception);
                    UpdateStatus(ErrorMessages.Account.TF_FAILED_TO_ENABLE_FATAL, State.Failed);
                }
                return (false, true);
            }

            UpdateStatus("Disabling Steam guard...", State.Processing);

            var tfDisableResponse = await Task.Run(() => Steam.TwoFactor.TurnOff());
            if (!tfDisableResponse.IsSuccess)
            {
                if (tfDisableResponse.Exception == null)
                {
                    Warn(ErrorMessages.Account.TF_FAILED_TO_DISABLE);
                    UpdateStatus(ErrorMessages.Account.TF_FAILED_TO_DISABLE, State.Failed);
                }
                else
                {
                    Warn(ErrorMessages.Account.TF_FAILED_TO_DISABLE_FATAL);
                    UpdateStatus(ErrorMessages.Account.TF_FAILED_TO_DISABLE_FATAL, State.Failed);
                }
                return (false, true);
            }

            var tfDisable = tfDisableResponse.HttpResponses.Last();
            if (!Regex.IsMatch(tfDisable?.Content ?? "", @"phone_box", RegexOptions.IgnoreCase))
                return (false, false); //thonk... old: (false, true)

            var _status = (MailBoxResponse == null)
                ? "Waiting for your confirmation..."
                : "Waiting for mail...";

            await Task.Delay(TimeSpan.FromSeconds(5)); // wait 5s. we will wait for mail to be delivered

            for (int i = 0; i < MailVerifyMaxRetry; i++)
            {
                UpdateStatus($"[Disabling guard | Try {i + 1}/{ConfirmMailLoopMax}]: {_status}", State.Processing);
                var disableLink = await GetGuardLinkFromMail();
                if (!disableLink.Success)
                {
                    Warn($"--/Try {i + 1} of {MailVerifyMaxRetry}; Steam guard/-- {ErrorMessages.Account.TF_SEARCH_MESSAGE_ERROR_FATAL}");
                    UpdateStatus($"[Disabling guard | Try {i + 1}/{ConfirmMailLoopMax}]: {ErrorMessages.Account.TF_SEARCH_MESSAGE_ERROR_FATAL}", State.Processing);
                    await Task.Delay(TimeSpan.FromSeconds(5)); // wait 5s and retry
                    continue;
                }

                return await LoadGuardConfirmLink(disableLink.Link);
            }

            return (false, true);
        }

        private async Task<bool> LoadConfirmLink(string link)
        {
            for (int i = 0; i < ConfirmMailLoopMax; i++)
            {
                var request = new RestRequest(link, Method.GET);
                request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                var response = await Task.Run(() => HttpClient.Execute(request));
                if (!response.IsSuccessful)
                {
                    if (response.ErrorException == null)
                    {
                        Warn($"--/Try {i + 1} of {ConfirmMailLoopMax}/-- {ErrorMessages.Account.MC_LOAD_LINK_FAILED}\n{link}");
                        UpdateStatus($"[Try {i + 1}/{ConfirmMailLoopMax}]: {ErrorMessages.Account.MC_LOAD_LINK_FAILED}", State.Processing);
                        await Task.Delay(TimeSpan.FromSeconds(15));
                    }
                    else
                    {
                        Error($"--/Try {i + 1} of {ConfirmMailLoopMax}/-- {ErrorMessages.Account.MC_LOAD_LINK_FAILED_FATAL}\n{link}", response.ErrorException);
                        UpdateStatus($"[Try {i + 1}/{ConfirmMailLoopMax}]: {ErrorMessages.Account.MC_LOAD_LINK_FAILED_FATAL}", State.Processing);
                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }
                    continue;
                }

                if (SteamMailConfirmed.IsMatch(response.Content))
                    return true;
            }

            SwitchState(State.Failed);
            return false;
        }

        private async Task<(bool Success, bool IsFatal)> LoadGuardConfirmLink(string link)
        {
            for (int i = 0; i < ConfirmMailLoopMax; i++)
            {
                var request = new RestRequest(link, Method.GET);
                request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                var response = await Task.Run(() => HttpClient.Execute(request));
                if (!response.IsSuccessful)
                {
                    if (response.ErrorException == null)
                    {
                        Warn($"--/Try {i + 1} of {ConfirmMailLoopMax}/-- {ErrorMessages.Account.TF_LOAD_LINK_FAILED}\n{link}");
                        UpdateStatus($"[Try {i + 1}/{ConfirmMailLoopMax}]: {ErrorMessages.Account.TF_LOAD_LINK_FAILED}", State.Processing);
                        await Task.Delay(TimeSpan.FromSeconds(15));
                    }
                    else
                    {
                        Error($"--/Try {i + 1} of {ConfirmMailLoopMax}/-- {ErrorMessages.Account.TF_LOAD_LINK_FAILED_FATAL}\n{link}", response.ErrorException);
                        UpdateStatus($"[Try {i + 1}/{ConfirmMailLoopMax}]: {ErrorMessages.Account.TF_LOAD_LINK_FAILED_FATAL}", State.Processing);
                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }
                    continue;
                }

                if (!SteamGuardDisabled.IsMatch(response.Content)) // yeah. it will not match cuz checking for disable error
                    return (true, false);

                return (false, false);
            }

            SwitchState(State.Failed);
            return (false, true);
        }

        private async Task<(bool Success, string Link)> GetConfirmLinkFromMail(string creationId)
        {
            var mails = await Task.Run(() => HandlerMail?.GetMails(MailBoxResponse));
            if ((mails?.Count ?? 0) < 1)
                return (false, null);

            foreach (var mail in mails)
            {
                var linkRegex = SteamMailConfirmationLink.Match(mail.Body);
                if (!linkRegex.Success)
                    continue; // next mail...

                try
                {
                    var link = linkRegex.Groups[1].Value;
                    var uri = new Uri(linkRegex.Groups[1].Value);
                    if (string.IsNullOrEmpty(uri.Query))
                        continue;

                    if (!Regex.IsMatch(uri.Query, $@"creationid\={creationId}", RegexOptions.IgnoreCase))
                        continue; // we will get another link with valid to this account creation ID

                    return (true, link);
                }
                catch (Exception ex)
                {
                    Error(ErrorMessages.Account.MCF_SEARCH_MESSAGE_ERROR, ex);
                    continue;
                }
            }
            return (false, null);
        }

        private async Task<(bool Success, string Link)> GetGuardLinkFromMail()
        {
            var mails = await Task.Run(() => HandlerMail?.GetMails(MailBoxResponse));
            if ((mails?.Count ?? 0) < 1)
                return (false, null);

            foreach (var mail in mails)
            {
                var linkRegex = SteamGuardConfirmationLink.Match(mail.Body);
                if (!linkRegex.Success)
                    continue; // next mail...

                try
                {
                    var link = linkRegex.Groups[1].Value;
                    var uri = new Uri(linkRegex.Groups[1].Value);
                    if (string.IsNullOrEmpty(uri.Query))
                        continue;

                    if (!SteamIdLong.HasValue) // in case when something went wrong with id...
                        return (true, link);

                    if (!Regex.IsMatch(uri.Query, $@"steamid\={SteamIdLong.Value}", RegexOptions.IgnoreCase))
                        continue; // we will get another link with valid to this account ID

                    return (true, link);
                }
                catch (Exception ex)
                {
                    Error(ErrorMessages.Account.TF_SEARCH_MESSAGE_ERROR, ex);
                    continue;
                }
            }
            return (false, null);
        }
        private void UpdateStatus(string message, State state)
        {
            _Status = message;
            Options?.RefreshDisplayFn();
            SwitchState(state);
        }

        private void SwitchState(State state)
        {
            switch (state)
            {
                case State.Success:
                    Options?.SetStatusColorFn(System.Drawing.Color.FromArgb(112, 166, 20));
                    break;
                case State.GuardLeavedOn:
                    Options?.SetStatusColorFn(System.Drawing.Color.FromArgb(212, 188, 11));
                    break;
                case State.Processing:
                    Options?.SetStatusColorFn(System.Drawing.Color.FromArgb(20, 155, 227));
                    break;
                case State.NotSavedToFile:
                case State.NotSavedToFileDisabled:
                    Options?.SetStatusColorFn(System.Drawing.Color.FromArgb(13, 189, 116));
                    break;
                default:
                    Options?.SetStatusColorFn(System.Drawing.Color.FromArgb(255, 55, 0));
                    break;
            }
        }

        private string LogAccInfo => $"{{{nameof(Account)};login={_Login ?? "!!!NOT YET READY"};mail={_Mail ?? "!!!NOT YET READY"}}}";
        private void Info(string message)
            => Logger.Info($"{LogAccInfo}: {message}");
        private void Debug(string message)
            => Logger.Debug($"{LogAccInfo}: {message}");
        private void Warn(string message)
            => Logger.Warn($"{LogAccInfo}: {message}");
        private void Error(string message, Exception exception)
            => Logger.Error($"{LogAccInfo}: {message}", exception);

        private enum State
        {
            Success = 0,
            NotSavedToFile,
            NotSavedToFileDisabled,
            Processing,
            GuardLeavedOn,
            Failed,
        }

        private enum ProfilePhotoState
        {
            Success,
            FileNotFound,
            FileNotSet,
            RequestError
        }
    }
}
