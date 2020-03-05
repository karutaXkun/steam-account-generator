using SACModuleBase;
using SACModuleBase.Enums.Captcha;
using SACModuleBase.Models;
using SACModuleBase.Models.Capcha;
using SteamAccCreator.Gui;
using SteamAccCreator.Interfaces;
using SteamAccCreator.Models;
using System;
using System.Windows.Forms;

namespace SteamAccCreator.Web.Captcha.Handlers
{
    public class LocalCaptchaHandler : ISACHandlerCaptcha, ISACHandlerReCaptcha
    {
        public bool ModuleEnabled { get; set; } = true;
        public void ModuleInitialize(SACInitialize initialize) { }

        private AccountCreateOptions Options;
        public LocalCaptchaHandler(AccountCreateOptions options)
        {
            Options = options;
        }

        public CaptchaResponse Solve(ReCaptchaRequest request)
        {
            var solution = default(CaptchaResponse);
            Options.ExecuteInUiFn(() =>
            {
                using (var dialog = new ReCaptchaDialog(request.Proxy))
                {
                    solution = ShowSolveDialog(dialog);
                }
            });
            return solution;
        }

        public CaptchaResponse Solve(CaptchaRequest request)
        {
            using (var dialog = new CaptchaDialog(request))
            {
                var solution = ShowSolveDialog(dialog);
                return solution;
            }
        }

        private CaptchaResponse ShowSolveDialog(ICaptchaDialog dialog)
        {
            try
            {
                var dialogStatus = dialog.ShowDialog(out var solution);
                switch (dialogStatus)
                {
                    case DialogResult.Yes:
                    case DialogResult.OK:
                    case DialogResult.Cancel:
                    case DialogResult.Retry:
                    case DialogResult.Abort:
                        return solution;
                    default:
                        return solution ?? new CaptchaResponse(CaptchaStatus.CannotSolve, "Something went wrong...");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Open captcha window error.", ex);
                return new CaptchaResponse(CaptchaStatus.CannotSolve, "Open captcha window error.");
            }
        }
    }
}
