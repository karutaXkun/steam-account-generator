using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Gecko;
using SteamAccCreator.Gui;

namespace SteamAccCreator
{
    static class Program
    {
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        /// 
        private static Mutex mutex = null;
        public static bool UseRuCaptchaDomain = false;
        public static readonly Web.Updater.UpdaterHandler UpdaterHandler = new Web.Updater.UpdaterHandler();
        public static bool GeckoInitialized = false;

        public static bool EndlessTwoCaptcha = true;

        [STAThread]
        static void Main()
        {
            const string appName = "StmAccGen";
            bool createdNew;

#if !DEBUG
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.IsTerminating)
                    Logger.Fatal("FATAL_UNHANDLED_EXCEPTION", e.ExceptionObject as Exception);
                else
                    Logger.Error("UNHANDLED_EXCEPTION", e.ExceptionObject as Exception);
            };
            AppDomain.CurrentDomain.FirstChanceException += (s, e)
                => Logger.Warn("FIRST_CHANCE_EXCEPTION", e.Exception);
#endif

            Logger.SuppressErrorDialogs = Utility.HasStartOption("-suppressErrors");
            Logger.SuppressAllErrorDialogs = Utility.HasStartOption("-suppressAllErrors");

            Logger.Warn($@"Coded by:
https://github.com/Ashesh3
https://github.com/EarsKilla

Version: {Web.Updater.UpdaterHandler.CurrentVersion}
Latest versions will be here: https://github.com/EarsKilla/Steam-Account-Generator/releases");

            Logger.Trace("Starting...");

            mutex = new Mutex(true, appName, out createdNew);
            if (!createdNew)
            {
                Logger.Warn("Another instance is running. Shutting down...");
                return;
            }

            try
            {
                Logger.Debug("Initializing gecko/xpcom...");
                Xpcom.Initialize(Pathes.DIR_GECKO);
                GeckoInitialized = true;
                Logger.Debug("Initializing gecko/xpcom: done!");
            }
            catch (Exception ex)
            {
                Logger.Error($"Initializing gecko/xpcom: error!", ex);
            }
            Logger.Info($"Gecko/xpcom profile directory: {Xpcom.ProfileDirectory}");

            UpdaterHandler.Refresh();

            UseRuCaptchaDomain = Utility.HasStartOption("-rucaptcha");
            if (UseRuCaptchaDomain)
                Logger.Info("Option '-rucaptcha' detected. Switched from 2captcha.com to rucaptcha.com");

            Web.Captcha.Handlers.RuCaptchaHandler.Domain = Utility.GetStartOption(@"-(two|ru)captchaDomain[:=](.*)",
                (m) => Utility.MakeUri(m.Groups[2].Value),
                new Uri((UseRuCaptchaDomain) ? "http://rucaptcha.com" : "http://2captcha.com"));

            EndlessTwoCaptcha = Utility.GetStartOption(@"-noEndless(Two|Ru)captcha",
                (m) => false,
                EndlessTwoCaptcha);

            Web.Captcha.Handlers.CaptchaSolutionsHandler.Domain = Utility.GetStartOption(@"-captchaSolutionsDomain[:=](.*)",
                (m) => Utility.MakeUri(m.Groups[1].Value),
                new Uri("http://api.captchasolutions.com/"));

            Web.Mail.MailHandler.MailboxUri = Utility.GetStartOption(@"-mailBox[:=](.*)",
                (m) =>
                {
                    Web.Mail.MailHandler.IsMailBoxCustom = true;
                    return Utility.MakeUri(m.Groups[1].Value);
                },
                Web.Mail.MailHandler.MailboxUri);

            Web.Mail.MailHandler.CheckUserMailVerifyCount = Utility.GetStartOption(@"-mailUserChecks[:=](\d+)",
                (m) =>
                {
                    if (!int.TryParse(m.Groups[1].Value, out int val))
                        return Defaults.Mail.COUNT_OF_CHECKS_MAIL_USER;

                    return val;
                }, Defaults.Mail.COUNT_OF_CHECKS_MAIL_USER);
            Web.Mail.MailHandler.CheckRandomMailVerifyCount = Utility.GetStartOption(@"-mailBoxChecks[:=](\d+)",
                (m) =>
                {
                    if (!int.TryParse(m.Groups[1].Value, out int val))
                        return Defaults.Mail.COUNT_OF_CHECKS_MAIL_AUTO;

                    return val;
                }, Defaults.Mail.COUNT_OF_CHECKS_MAIL_AUTO);

            Web.Steam.SteamWebClient.DisableLegitDelay = Utility.GetStartOption(@"-(ns|noSteam|disableSteam)Legit",
                (m) => true,
                Web.Steam.SteamWebClient.DisableLegitDelay);

            Web.Account.SkipSteamGuardDisable = Utility.GetStartOption(@"-skip(Steam)?Guard",
                (m) => true,
                Web.Account.SkipSteamGuardDisable);

            var noStylesOpt = Utility.HasStartOption("-nostyles");
            if (!noStylesOpt)
            {
                Logger.Trace("Enabling visual styles...");
                Application.EnableVisualStyles();
            }
            var defTextRenderOpt = Utility.HasStartOption("-defaultTextRendering");
            if (!defTextRenderOpt)
            {
                Logger.Trace($"{nameof(Application.SetCompatibleTextRenderingDefault)}(false)...");
                Application.SetCompatibleTextRenderingDefault(false);
            }

            Logger.Info("\n######################################\n" +
                "Running config:\n" +
                $"{nameof(GeckoInitialized)}: {(GeckoInitialized ? "Yes" : "No")}\n" +
                $"{nameof(UseRuCaptchaDomain)}: {(UseRuCaptchaDomain ? "Yes" : "No")}\n" +
                $"Ru/TwoCaptcha enless mode: {(EndlessTwoCaptcha ? "Yes" : "No")}" +
                $"Ru/TwoCaptcha domain: {Web.Captcha.Handlers.RuCaptchaHandler.Domain}\n" +
                $"CaptchaSolutions domain: {Web.Captcha.Handlers.CaptchaSolutionsHandler.Domain}\n" +
                $"MailBox URL: {Web.Mail.MailHandler.MailboxUri}\n" +
                $"Count of mail checks in hand mode: {Web.Mail.MailHandler.CheckUserMailVerifyCount}\n" +
                $"Count of mail checks in auto mode: {Web.Mail.MailHandler.CheckRandomMailVerifyCount}\n" +
                $"Disable \"legit\" delays between Steam requests: {(Web.Steam.SteamWebClient.DisableLegitDelay ? "Yes" : "No")}\n" +
                $"Skip disabling Steam guard: {(Web.Account.SkipSteamGuardDisable ? "Yes" : "No")}\n" +
                $"NoStyles: {(noStylesOpt ? "Yes" : "No")}\n" +
                $"Default text render: {(defTextRenderOpt ? "Yes" : "No")}\n" +
                "######################################");

            Logger.Trace($"Starting app with {nameof(MainForm)}...");
            Application.Run(new MainForm());
        }


    }

}

