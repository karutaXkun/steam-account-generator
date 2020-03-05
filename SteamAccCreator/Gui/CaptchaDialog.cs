using SACModuleBase.Enums.Captcha;
using SACModuleBase.Models.Capcha;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SteamAccCreator.Gui
{
    public partial class CaptchaDialog : Form, Interfaces.ICaptchaDialog
    {
        private CaptchaResponse Solution;
        private CaptchaRequest Captcha;
        public CaptchaDialog(CaptchaRequest captchaRequest)
        {
            Logger.Debug("Initializing image captcha solver...");
            Solution = new CaptchaResponse(CaptchaStatus.CannotSolve, "Something went wrong...");
            Captcha = captchaRequest;

            InitializeComponent();

            DrawCaptcha();
        }

        private Image GetImageFromBase64String(string base64)
        {
            try
            {
                Logger.Trace($"Loading captcha image from base64:\n{base64}");
                var data = Convert.FromBase64String(base64);
                using (var memStream = new MemoryStream(data))
                {
                    return Image.FromStream(memStream);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Initializing captcha image error.", ex);
            }
            return null;
        }
        private void DrawCaptcha()
        {
            var img = GetImageFromBase64String(Captcha.CaptchaImage);
            if (img == null)
            {
                Solution = new CaptchaResponse(CaptchaStatus.Failed, "Loading captcha image error!");
                DialogResult = DialogResult.Abort;
                Close();
                return;
            }

            Logger.Trace("Captcha image was loaded and will be displayed...");
            boxCaptcha.Image = img;
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            Logger.Info("Captcha image refresh requested...");
            Solution = new CaptchaResponse(CaptchaStatus.RetryAvailable, "Refresh requested...");
            DialogResult = DialogResult.Retry;
            Close();
        }

        private void BtnConfirm_Click(object sender, EventArgs e)
        {
            Solution = new CaptchaResponse(txtCaptcha.Text);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void TxtCaptcha_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                BtnConfirm_Click(sender, e);
        }

        private void TxtCaptcha_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.KeyChar = char.ToUpper(e.KeyChar);
        }

        private void CaptchaDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing &&
                DialogResult != DialogResult.OK)
            {
                Solution = new CaptchaResponse(CaptchaStatus.Failed, "Captcha dialog was closed!");
                DialogResult = DialogResult.Cancel;
            }
        }

        public DialogResult ShowDialog(out CaptchaResponse solution)
        {
            var result = ShowDialog();
            solution = Solution;
            return result;
        }
    }
}
