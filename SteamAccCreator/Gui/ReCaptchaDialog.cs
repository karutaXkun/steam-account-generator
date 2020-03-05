using Gecko;
using SACModuleBase.Enums;
using SACModuleBase.Enums.Captcha;
using SACModuleBase.Models;
using SACModuleBase.Models.Capcha;
using System;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SteamAccCreator.Gui
{
    public partial class ReCaptchaDialog : Form, Interfaces.ICaptchaDialog
    {
        private CaptchaResponse Solution;

        public ReCaptchaDialog(Proxy proxy)
        {
            Solution = new CaptchaResponse(CaptchaStatus.Failed, "Something went wrong...");

            InitializeComponent();
            if (proxy == null)
            {
                try
                {
                    GeckoPreferences.Default.SetIntPref("network.proxy.type", 4);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to update gecko preference 'network.proxy.type' to '4'", ex);
                }
                return;
            }

            GeckoPreferences.Default["network.proxy.type"] = 1;
            // clear proxies
            GeckoSetProxy(ProxyType.Http, "", 0);
            GeckoSetProxy(ProxyType.Socks4, "", 0);

            GeckoSetProxy(proxy.Type, proxy.Host, proxy.Port);
        }

        private void GeckoSetProxy(ProxyType proxyType, string host, int port)
        {
            switch (proxyType)
            {
                case ProxyType.Socks4:
                case ProxyType.Socks5:
                    GeckoPreferences.Default["network.proxy.socks"] = host;
                    GeckoPreferences.Default["network.proxy.socks_port"] = port;
                    break;
                case ProxyType.Unknown:
                case ProxyType.Http:
                case ProxyType.Https:
                default:
                    GeckoPreferences.Default["network.proxy.http"] = host;
                    GeckoPreferences.Default["network.proxy.http_port"] = port;
                    GeckoPreferences.Default["network.proxy.ssl"] = host;
                    GeckoPreferences.Default["network.proxy.ssl_port"] = port;
                    break;
            }

            if (proxyType == ProxyType.Socks4)
                GeckoPreferences.Default["network.proxy.socks_version"] = 4;
            else if (proxyType == ProxyType.Socks5)
                GeckoPreferences.Default["network.proxy.socks_version"] = 5;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Solution = new CaptchaResponse(CaptchaStatus.Failed, "Captcha dialog was closed!");
            Close();
        }

        private void btnAccept_Click(object sender, EventArgs e)
        {
            var resps = geckoWebBrowser1.Document.GetElementsByName("g-recaptcha-response");
            var solutionText = string.Empty;
            foreach (var resp in resps)
            {
                var textArea = resp as Gecko.DOM.GeckoTextAreaElement;
                var captchaText = textArea?.Value ?? "";
                if (string.IsNullOrEmpty(captchaText))
                    continue;

                solutionText += $"{captchaText}\n\n";
            }

            Solution = new CaptchaResponse(solutionText);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void geckoWebBrowser1_Navigating(object sender, Gecko.Events.GeckoNavigatingEventArgs e)
        {
            if (Regex.IsMatch(e.Uri?.Segments?.LastOrDefault() ?? "", @"join\/?", RegexOptions.IgnoreCase)
                || (e.Uri?.Host ?? "").ToLower() != (new Uri(Web.Steam.SteamDefaultUrls.JOIN).Host.ToLower())) // wat?
            {
                Logger.Trace("Navigated to /join/.");
                return;
            }

            try
            {
                Logger.Info("Stopping navigation to new location...");
                e.Cancel = true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to stop navigation...", ex);
                try
                {
                    geckoWebBrowser1.Navigate(Web.Steam.SteamDefaultUrls.JOIN, GeckoLoadFlags.FirstLoad);
                }
                catch (Exception exNav)
                {
                    Logger.Error("Navigation error", exNav);
                }
            }
        }

        private void geckoWebBrowser1_ReadyStateChange(object sender, DomEventArgs e)
        {
            var accFormBox = geckoWebBrowser1.Document.GetElementById("account_form_box");
            foreach (var accChild in accFormBox.ChildNodes)
            {
                if (accChild == null)
                    continue;

                if (accChild.NodeType != NodeType.Element)
                    continue;

                var joinRow = accChild as GeckoHtmlElement;
                if (!(joinRow?.ClassName?.Contains("join_form") ?? false))
                    continue;

                foreach (var jRowChild in joinRow.ChildNodes)
                {
                    if (jRowChild == null)
                        continue;
                    if (jRowChild.NodeType != NodeType.Element)
                        continue;

                    var jForm = jRowChild as GeckoHtmlElement;
                    if (jForm == null)
                        continue;
                    if (!(jForm.ClassName?.Contains("form_row") ?? false))
                        continue;
                    if (jForm.ChildNodes
                        .Where(x => x != null && x.NodeType == NodeType.Element)
                            .Select(x => x as GeckoHtmlElement)
                                .Any(x => x?.Id?.ToLower()?.Contains("captcha_entry") ?? false))
                        continue;

                    joinRow.RemoveChild(jRowChild);
                }
            }
        }

        private void geckoWebBrowser1_CreateWindow(object sender, GeckoCreateWindowEventArgs e)
        {
            e.Cancel = true;
        }

        public DialogResult ShowDialog(out CaptchaResponse solution)
        {
            btnReload_Click(null, null);

            var result = this.ShowDialog();
            solution = this.Solution;
            return result;
        }

        private void btnReload_Click(object sender, EventArgs e)
        {
            geckoWebBrowser1.Navigate(Web.Steam.SteamDefaultUrls.JOIN, GeckoLoadFlags.FirstLoad);
        }
    }
}
