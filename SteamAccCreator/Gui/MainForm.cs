using SteamAccCreator.Web;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteamAccCreator.Gui
{
    public partial class MainForm : Form
    {
        const string URL_WIKI_FIND_SUBID = "https://github.com/steam-account-creator/Steam-Account-Generator/wiki/Find-sub-ID";
        public const long PHOTO_MAX_SIZE = 1048576;

        public Models.Configuration Configuration { get; private set; } = new Models.Configuration();
        public ProxyManager ProxyManager { get; private set; }
        public Models.ModuleManager ModuleManager { get; private set; }

        public BindingList<Account> Accounts { get; private set; } = new BindingList<Account>();

        public MainForm()
        {
            Logger.Trace($"{nameof(InitializeComponent)}()...");
            InitializeComponent();

            Logger.Trace($"Loading config: {Pathes.FILE_CONFIG}");
            try
            {
                if (System.IO.File.Exists(Pathes.FILE_CONFIG))
                {
                    Logger.Trace("Reading config file...");
                    var configData = System.IO.File.ReadAllText(Pathes.FILE_CONFIG);
                    Logger.Trace("Deserializing config from JSON...");
                    Configuration = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.Configuration>(configData);
                    Logger.Trace("Config data has been loaded.");
                }
                else
                    Logger.Trace("Config file does not exists. Using defaults...");
            }
            catch (Newtonsoft.Json.JsonException jEx)
            {
                Logger.Error("Probabply deserializing error...", jEx);
                Configuration = new Models.Configuration();
                Logger.Trace("Using defaults...");
            }
            catch (Exception ex)
            {
                Logger.Error("Config load or deserializing error or something else.", ex);
                Configuration = new Models.Configuration();
                Logger.Trace("Using defaults...");
            }

            Logger.Trace("Fixing NULL elements...");
            Configuration.FixNulls();

            Logger.Trace("Checking out file...");
            if (string.IsNullOrEmpty(Configuration.Output.Path))
                Configuration.Output.Path = Path.Combine(Environment.CurrentDirectory, "accounts.txt");

            Logger.Trace("Setting properties for base settings (mail, login, password, etc)...");
            CbRandomMail.Checked = Configuration.Mail.Random;
            txtEmail.Text = Configuration.Mail.Value;

            CbRandomLogin.Checked = Configuration.Login.Random;
            CbNeatLogin.Checked = Configuration.Login.Neat;
            txtAlias.Text = Configuration.Login.Value;

            CbRandomPassword.Checked = Configuration.Password.Random;
            CbNeatPassword.Checked = Configuration.Password.Neat;
            txtPass.Text = Configuration.Password.Value;

            CbAddGames.Checked = Configuration.Games.AddGames;
            ListGames.Items.AddRange(Configuration.Games.GamesToAdd ?? new Models.GameInfo[0]);

            Logger.Trace("Setting properties for captcha...");
            CbCapSolver.SelectedIndex = Configuration.Captcha.ServiceIndex;
            BsCaptchaCapsolConfig.DataSource = Configuration.Captcha.CaptchaSolutions;
            BsCaptchaTwoCapConfig.DataSource = Configuration.Captcha.RuCaptcha;

            Logger.Trace("Setting properties for file writing...");
            CbFwEnable.Checked = Configuration.Output.Enabled;
            CbFwMail.Checked = Configuration.Output.WriteEmails;
            LinkFwPath.Text = Configuration.Output.GetVisualPath();
            CbFwOutType.SelectedIndex = (int)Configuration.Output.SaveType;

            Logger.Trace("Setting properties for proxy...");
            CbProxyEnabled.Checked = Configuration.Proxy.Enabled;
            DgvProxyList.DataSource = Configuration.Proxy.List;

            LbProfileGroupsToJoin.UpdateItems(Configuration.Profile.GroupsToJoin);

            Logger.Trace("Setting properties for profile...");
            BsProfileConfig.DataSource = Configuration.Profile;

            CbUpdateChannel.SelectedIndex = (int)Program.UpdaterHandler.UpdateChannel;
            CbUpdateChannel_SelectedIndexChanged(this, null);
            CbUpdateChannel.SelectedIndexChanged += CbUpdateChannel_SelectedIndexChanged;

            ProxyManager = new ProxyManager(this);

            ModuleManager = new Models.ModuleManager(Configuration);
            DgvModules.DataSource = ModuleManager.ModuleBindings;

            DgvAccounts.DataSource = Accounts;
        }

        private void SaveConfig()
        {
            try
            {
                Logger.Info("Saving config...");
                var confData = Newtonsoft.Json.JsonConvert.SerializeObject(Configuration, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(Pathes.FILE_CONFIG, confData);
                Logger.Info("Saving config done!");
            }
            catch (Exception ex)
            {
                Logger.Error("Saving config error!", ex);
            }

            ModuleManager.SaveDisabled();
        }

        public async void BtnCreateAccount_Click(object sender, EventArgs e)
        {
            Logger.Trace($"{nameof(btnCreateAccount)} was clicked...");

            if (NumAccountsCount.Value > 100)
                NumAccountsCount.Value = 100;
            else if (NumAccountsCount.Value < 1)
                NumAccountsCount.Value = 1;

            Logger.Trace($"Accounts to create: {NumAccountsCount}.");

            switch (Configuration.Captcha.Service)
            {
                case Enums.CaptchaService.Captchasolutions:
                    {
                        if (string.IsNullOrEmpty(Configuration.Captcha.CaptchaSolutions.ApiKey) ||
                            string.IsNullOrEmpty(Configuration.Captcha.CaptchaSolutions.ApiSecret))
                        {
                            Logger.Trace("Captchasolutions cannot be used. API and secret keys is empty! Checking modules...");
                            Configuration.Captcha.Service = Enums.CaptchaService.Module;
                            goto case Enums.CaptchaService.Module;
                        }
                        Logger.Trace("Using Captchasolutions...");
                    }
                    break;
                case Enums.CaptchaService.RuCaptcha:
                    {
                        if (string.IsNullOrEmpty(Configuration.Captcha.RuCaptcha.ApiKey))
                        {
                            Logger.Trace("TwoCaptcha/RuCaptcha cannot be used. API key is empty! Checking modules...");
                            Configuration.Captcha.Service = Enums.CaptchaService.Module;
                            goto case Enums.CaptchaService.Module;
                        }
                        Logger.Trace("Using TwoCaptcha/RuCaptcha...");
                    }
                    break;
                case Enums.CaptchaService.Module:
                    {
                        if (ModuleManager.Modules.GetCaptchaSolvers().Count() < 1 &&
                            ModuleManager.Modules.GetReCaptchaSolvers().Count() < 1)
                        {
                            Logger.Trace("No any module with captcha solving support. Swithing to manual mode...");
                            Configuration.Captcha.Service = Enums.CaptchaService.None;
                            goto default;
                        }
                        Logger.Trace("Using modules...");
                    }
                    break;
                case Enums.CaptchaService.None:
                default:
                    Logger.Trace("Using manual mode...");
                    break;
            }

            Configuration.Proxy.Enabled = CbProxyEnabled.Checked;
            if (ProxyManager.Enabled && ProxyManager.Current == null)
                ProxyManager.GetNew();
            else if (!ProxyManager.Enabled)
                ProxyManager.GetNew();

            if (CbFwEnable.Checked && string.IsNullOrEmpty(Configuration.Output.Path))
                Configuration.Output.Path = Path.Combine(Environment.CurrentDirectory, $"Accounts.{((CbFwOutType.SelectedIndex == 2) ? "csv" : "txt")}");

            if (CbFwEnable.Checked)
                Logger.Info($"File writing is enabled and file will be here: {Configuration.Output.Path}.");

            SaveConfig();

            var slowCaptchaMode = Configuration.Captcha.Service == Enums.CaptchaService.None;
            var accounts2create = NumAccountsCount.Value;
            for (var i = 0; i < accounts2create; i++)
            {
                Logger.Trace($"Initializing account {i + 1} of {NumAccountsCount}...");
                var handlerMailBox = ModuleManager
                    .GetModulesByType<SACModuleBase.ISACHandlerMailBox>()
                    .FirstOrDefault();
                var handlerReCaptcha = default(SACModuleBase.ISACHandlerReCaptcha);
                var handlerImageCaptcha = default(SACModuleBase.ISACHandlerCaptcha);
                var handlerUserAgent = ModuleManager
                    .GetModulesByType<SACModuleBase.ISACHandlerUserAgent>()
                    .FirstOrDefault();

                switch (Configuration.Captcha.Service)
                {
                    case Enums.CaptchaService.Captchasolutions:
                        {
                            var handler = new Web.Captcha.Handlers.CaptchaSolutionsHandler(Configuration);
                            handlerReCaptcha = handler;
                            handlerImageCaptcha = handler;
                        }
                        break;
                    case Enums.CaptchaService.RuCaptcha:
                        {
                            var handler = new Web.Captcha.Handlers.RuCaptchaHandler(Configuration);
                            handlerReCaptcha = handler;
                            handlerImageCaptcha = handler;
                        }
                        break;
                    case Enums.CaptchaService.Module:
                        {
                            handlerReCaptcha = ModuleManager
                                .GetModulesByType<SACModuleBase.ISACHandlerReCaptcha>()
                                .FirstOrDefault();

                            handlerImageCaptcha = ModuleManager
                                .GetModulesByType<SACModuleBase.ISACHandlerCaptcha>()
                                .FirstOrDefault();
                        }
                        break;
                }

                var accountNumber = Accounts.Count;
                var account = new Account(new Models.AccountCreateOptions()
                {
                    ParentForm = this,
                    ProxyManager = ProxyManager,
                    Config = Configuration,
                    AccountNumber = accountNumber,
                    HandlerMailBox = handlerMailBox,
                    HandlerGoogleCaptcha = handlerReCaptcha,
                    HandlerImageCaptcha = handlerImageCaptcha,
                    HandlerUserAgent = handlerUserAgent,
                    RefreshDisplayFn = () => RefreshDisplay(accountNumber),
                    SetStatusColorFn = (color) => ChangeStatusColor(accountNumber, color),
                    ExecuteInUiFn = (action) => ExecuteInvoke(action ?? new Action(() => Logger.Warn("Action to execute in UI is null!")))
                });

                Accounts.Add(account);

                Logger.Trace($"Account {i + 1} of {NumAccountsCount} ({accountNumber} in total). " +
                    $"Starting creation witch captcha {((slowCaptchaMode) ? "hand" : "auto")} mode...");

                if (slowCaptchaMode)
                    await account.Register();
                else
                {
                    var thread = new Thread(async () => await account.Register());
                    thread.Start();
                }
            }
        }

        private void RefreshDisplay(int rowNumber)
        {
            Logger.Trace($"void {nameof(RefreshDisplay)}({nameof(rowNumber)}=\"{rowNumber}\");");
            Invoke(new Action(() =>
            {
                try
                {
                    var rowToRefresh = DgvAccounts.Rows[rowNumber];
                    var cellsCount = rowToRefresh.Cells.Count;
                    for (int i = 0; i < cellsCount; i++)
                    {
                        DgvAccounts.UpdateCellValue(i, rowNumber);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to refresh display of {nameof(rowNumber)}={rowNumber}", ex);
                }
            }));
        }

        private void ChangeStatusColor(int rowNumber, System.Drawing.Color color)
        {
            Logger.Trace($"void {nameof(ChangeStatusColor)}({nameof(rowNumber)}=\"{rowNumber}\", {nameof(color)}=\"{color}\");");
            Invoke(new Action(() =>
            {
                try
                {
                    var row = DgvAccounts.Rows[rowNumber];
                    for (int i = 0; i < row.Cells.Count; i++)
                    {
                        var cell = row.Cells[i];
                        if (cell.OwningColumn.HeaderText.ToLower().Trim() != "status")
                            continue;

                        cell.Style.BackColor = color;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to changle color of {nameof(rowNumber)}={rowNumber}", ex);
                }
            }));
        }

        private void ToggleForceWriteIntoFile()
        {
            Logger.Trace($"void {nameof(ToggleForceWriteIntoFile)}();");
            var shouldForce = CbRandomMail.Checked || CbRandomPassword.Checked || CbRandomLogin.Checked;
            Logger.Trace($"void {nameof(ToggleForceWriteIntoFile)}();###var {nameof(shouldForce)} = {shouldForce}");
            CbFwEnable.Checked = shouldForce;
            CbFwEnable.AutoCheck = !shouldForce;
        }

        private void LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Logger.Trace($"void {nameof(LinkClicked)}();");
            var link = sender as LinkLabel;
            if (link == null)
                return;

            try
            {
                System.Diagnostics.Process.Start(link.Text);
                e.Link.Visited = true;
                Logger.Trace($"void {nameof(LinkClicked)}();###var {nameof(link.Text)} = {link.Text}");
            }
            catch (Exception ex) { Logger.Error("Exception thrown while opening link...", ex); }
        }

        private void CbRandomMail_CheckedChanged(object sender, EventArgs e)
        {
            Configuration.Mail.Random = CbRandomMail.Checked;
            ToggleForceWriteIntoFile();
        }

        private void CbRandomLogin_CheckedChanged(object sender, EventArgs e)
        {
            txtAlias.Enabled = !(CbNeatLogin.Enabled = Configuration.Login.Random = CbRandomLogin.Checked);
            ToggleForceWriteIntoFile();
        }

        private void CbNeatLogin_CheckedChanged(object sender, EventArgs e)
        {
            Configuration.Login.Neat = CbNeatLogin.Checked;
        }

        private void CbRandomPassword_CheckedChanged(object sender, EventArgs e)
        {
            txtPass.Enabled = !(CbNeatPassword.Enabled = Configuration.Password.Random = CbRandomPassword.Checked);
            ToggleForceWriteIntoFile();
        }

        private void CbNeatPassword_CheckedChanged(object sender, EventArgs e)
        {
            Configuration.Password.Neat = CbNeatPassword.Checked;
        }

        private void BtnLoadIds_Click(object sender, EventArgs e)
        {
            Logger.Trace($"void {nameof(BtnLoadIds_Click)}();");

            using (var fileDialog = new OpenFileDialog())
            {
                fileDialog.Filter = "All supported|*.txt;*.json|Text file|*.txt|JSON file|*.json|Try to parse from any type|*.*";
                fileDialog.Title = "Load game sub IDs";

                if (fileDialog.ShowDialog() != DialogResult.OK)
                    return;

                Logger.Trace($"Selected id's file: {fileDialog.FileName}");

                var fileData = System.IO.File.ReadAllText(fileDialog.FileName);
                Configuration.Games.GamesToAdd = Configuration.Games.GamesToAdd ?? new Models.GameInfo[0];
                try
                {
                    var games = Newtonsoft.Json.JsonConvert.DeserializeObject<IEnumerable<Models.GameInfo>>(fileData);
                    var _temp = Configuration.Games.GamesToAdd.ToList();

                    games = games.Where(x => !_temp.Any(g => g.Equals(x)));
                    _temp.AddRange(games);

                    Configuration.Games.GamesToAdd = _temp;
                }
                catch (Newtonsoft.Json.JsonException jEx)
                {
                    Logger.Error("JSON exception was thrown... It's probably file don't contains JSON data. Trying to parse it...", jEx);
                    try
                    {
                        var matches = Regex.Matches(fileData, @"(\d+):(.*)", RegexOptions.IgnoreCase);
                        foreach (Match match in matches)
                        {
                            try
                            {
                                var game = new Models.GameInfo()
                                {
                                    SubId = int.Parse(match.Groups[1].Value),
                                    Name = match.Groups[2].Value
                                };

                                if (!Configuration.Games.GamesToAdd.Any(x => x.Equals(game)))
                                    Configuration.Games.GamesToAdd = Configuration.Games.GamesToAdd.Append(game);
                            }
                            catch (Exception cEx) { Logger.Error("Parsing error (in foreach)!", cEx); }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Parsing via regexp. error!", ex);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Parsing error!", ex);
                }

                Logger.Trace($"Updating {nameof(ListGames)}... Count of games: {Configuration.Games.GamesToAdd.Count()}");
                ListGames.UpdateItems(Configuration.Games.GamesToAdd);
            }
        }

        private void BtnAddGame_Click(object sender, EventArgs e)
        {
            Logger.Trace($"{nameof(BtnAddGame_Click)}(..., ...);");
            using (var dialog = new AddGameDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Configuration.Games.GamesToAdd = Configuration.Games.GamesToAdd.Append(dialog.GameInfo);
                    ListGames.UpdateItems(Configuration.Games.GamesToAdd);
                    Logger.Debug($"{dialog.GameInfo.SubId}:{dialog.GameInfo.Name} added.");
                }
            }
        }

        private void BtnRemoveGame_Click(object sender, EventArgs e)
        {
            Logger.Trace($"{nameof(BtnRemoveGame_Click)}(..., ...);");
            var index = ListGames.SelectedIndex;
            if (index < 0 || index >= ListGames.Items.Count)
                return;

            var _temp = Configuration.Games.GamesToAdd.ToList();
            var _game = _temp.ElementAtOrDefault(index);
            Logger.Debug($"{(_game?.SubId)?.ToString() ?? "NULL"}:{_game?.Name ?? "NULL"} removed.");
            _temp.RemoveAt(index);
            Configuration.Games.GamesToAdd = _temp;
            ListGames.UpdateItems(Configuration.Games.GamesToAdd);
        }

        private void BtnClearGames_Click(object sender, EventArgs e)
        {
            Logger.Trace($"{nameof(BtnClearGames_Click)}(..., ...);");
            Configuration.Games.GamesToAdd = new Models.GameInfo[0];
            ListGames.Items.Clear();
        }

        private void ListGames_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem.GetType() != typeof(Models.GameInfo))
                return;

            var info = e.ListItem as Models.GameInfo;
            e.Value = $"{info.Name} ({info.SubId})";
        }

        private void ListGames_SelectedIndexChanged(object sender, EventArgs e)
        {
            var index = ListGames.SelectedIndex;

            BtnAddGame.Enabled = BtnRemoveGame.Enabled = !(index < 0 || index >= ListGames.Items.Count);
        }

        private void ListGames_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Logger.Trace($"{nameof(ListGames_MouseDoubleClick)}(..., ...);");
            var index = ListGames.SelectedIndex;
            if (index < 0 || index >= ListGames.Items.Count)
                return;

            var game = ListGames.Items[index] as Models.GameInfo;
            using (var dialog = new AddGameDialog(game))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var _temp = Configuration.Games.GamesToAdd.ToList();
                    ListGames.Items[index] = _temp[index] = game;
                    Configuration.Games.GamesToAdd = _temp;
                    Logger.Debug($"{game.SubId}:{game.Name} edited.");
                }
            }
        }

        private void BtnExportGames_Click(object sender, EventArgs e)
        {
            if (ListGames.Items.Count < 1)
            {
                MessageBox.Show(this, "Games list is empty!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var fileDialog = new SaveFileDialog())
            {
                fileDialog.Filter = "JSON|*.json";
                fileDialog.FilterIndex = 0;
                fileDialog.DefaultExt = "json";
                fileDialog.OverwritePrompt = true;

                fileDialog.Title = "Export game IDs";

                if (fileDialog.ShowDialog() != DialogResult.OK)
                    return;

                var data = Newtonsoft.Json.JsonConvert.SerializeObject(Configuration.Games.GamesToAdd, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(fileDialog.FileName, data);
            }
        }

        private void CbAddGames_CheckedChanged(object sender, EventArgs e)
        {
            Configuration.Games.AddGames = CbAddGames.Checked;
        }

        private void LinkHowToFindSubId_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(URL_WIKI_FIND_SUBID);
            e.Link.Visited = true;
        }

        private void CbFwEnable_CheckedChanged(object sender, EventArgs e)
        {
            Configuration.Output.Enabled = CbFwEnable.Checked;
        }

        private void CbFwMail_CheckedChanged(object sender, EventArgs e)
        {
            Configuration.Output.WriteEmails = CbFwMail.Checked;
        }

        private void BtnFwChangeFolder_Click(object sender, EventArgs e)
        {
            using (var fileDialog = new SaveFileDialog())
            {
                fileDialog.Filter = "Text File|*.txt|KeePass CSV|*.csv";
                fileDialog.Title = "Where to save accounts";

                fileDialog.CheckPathExists = true;
                fileDialog.OverwritePrompt = true;

                if (fileDialog.ShowDialog() != DialogResult.OK)
                    return;

                Configuration.Output.Path = fileDialog.FileName;

                Logger.Debug($"Save path was changed to: {Configuration.Output.Path}");

                if (fileDialog.FilterIndex == 2)
                    CbFwOutType.SelectedIndex = (int)File.SaveType.KeepassCsv;

                LinkFwPath.Text = Configuration.Output.GetVisualPath();
            }
        }

        private void CbFwOutType_SelectedIndexChanged(object sender, EventArgs e)
        {
            Configuration.Output.SaveType = (File.SaveType)CbFwOutType.SelectedIndex;

            if (Configuration.Output.SaveType == File.SaveType.KeepassCsv)
                Configuration.Output.Path = Path.ChangeExtension(Configuration.Output.Path, "csv");
            else
                Configuration.Output.Path = Path.ChangeExtension(Configuration.Output.Path, "txt");

            LinkFwPath.Text = Configuration.Output.GetVisualPath();
        }

        private void LinkFwPath_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                case PlatformID.Unix:
                    System.Diagnostics.Process.Start($"file://{Path.GetDirectoryName(Configuration.Output.Path)}");
                    break;
                case PlatformID.Win32NT:
                    System.Diagnostics.Process.Start("explorer.exe", $"/select, \"{Configuration.Output.Path}\"");
                    break;
                default:
                    return;
            }

            e.Link.Visited = true;
        }

        private void CbProxyEnabled_CheckedChanged(object sender, EventArgs e)
        {
            Configuration.Proxy.Enabled = CbProxyEnabled.Checked;
        }

        private async void BtnProxyTest_Click(object sender, EventArgs e)
        {
            int disabled = 0, error = 0, good = 0;
            LabProxyTotal.Text = (Configuration.Proxy?.List?.Count())?.ToString() ?? "NULL";
            void updateCounters()
            {
                ExecuteInvoke(() =>
                {
                    LabProxyBad.Text = $"{error}";
                    LabProxyGood.Text = $"{good}";
                    LabProxyDisabled.Text = $"{disabled}";

                    DgvProxyList.Refresh();
                });
            }

            BtnProxyTestCancel.Enabled = true;
            BtnProxyLoad.Enabled =
                BtnProxyTest.Enabled = false;
            await ProxyManager.CheckProxies(() =>
            { // proxy is disabled
                disabled++;
                updateCounters();
            },
            () =>
            { // proxy not passed checks
                error++;
                updateCounters();
            },
            () =>
            { // proxy is working
                good++;
                updateCounters();
            },
            () =>
            { // check ended
                ExecuteInvoke(() =>
                {
                    BtnProxyTestCancel.Enabled = false;

                    BtnProxyLoad.Enabled =
                        BtnProxyTest.Enabled = true;
                });
            });
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveConfig();
            Logger.Info("Shutting down...");
        }

        private void txtEmail_TextChanged(object sender, EventArgs e)
        {
            Configuration.Mail.Value = txtEmail.Text;
        }

        private void txtAlias_TextChanged(object sender, EventArgs e)
        {
            Configuration.Login.Value = txtAlias.Text;
        }

        private void txtPass_TextChanged(object sender, EventArgs e)
        {
            Configuration.Password.Value = txtPass.Text;
        }

        private void BtnProxyLoad_Click(object sender, EventArgs e)
        {
            using (var fileDialog = new OpenFileDialog())
            {
                fileDialog.Filter = "Text file|*.txt";
                fileDialog.Title = "Load proxy list";

                if (fileDialog.ShowDialog() != DialogResult.OK)
                    return;

                Logger.Trace("Loading proxies...");

                var data = System.IO.File.ReadAllLines(fileDialog.FileName);
                var proxies = new List<Models.ProxyItem>();
                var proxiesOld = (DgvProxyList.DataSource as IEnumerable<Models.ProxyItem>) ?? new Models.ProxyItem[0];

                if (proxiesOld.Count() > 0)
                {
                    switch (MessageBox.Show(this, "Merge with current proxies?", "Proxy loading...", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question))
                    {
                        case DialogResult.Yes:
                            proxies.AddRange(proxiesOld);
                            break;
                        case DialogResult.No:
                            break;
                        default:
                            return;
                    }
                }

                Logger.Trace($"Proxies file have {data.Count()} line(s).");

                foreach (var line in data)
                {
                    try
                    {
                        proxies.Add(new Models.ProxyItem(line));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Adding proxy error. Line: {line}", ex);
                    }
                }

                DgvProxyList.DataSource = Configuration.Proxy.List = proxies;

                Logger.Trace("Loading proxies done.");
            }
        }

        private void BtnProxyTestCancel_Click(object sender, EventArgs e)
        {
            BtnProxyTestCancel.Enabled = false;
            ProxyManager.CancelChecking();
        }

        private void CbUpdateChannel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender != this)
                Program.UpdaterHandler.Refresh((Web.Updater.Enums.UpdateChannelEnum)CbUpdateChannel.SelectedIndex);

            LbCurrentversionStr.Text = Web.Updater.UpdaterHandler.CurrentVersion.ToString(3);
            LbServerVersionStr.Text = Program.UpdaterHandler.VersionInfo.ToString();

            if (Program.UpdaterHandler.IsCanBeUpdated && sender == this)
                tabControl.SelectedTab = tabUpdates;

            BtnDlLatestBuild.Visible = Program.UpdaterHandler.IsCanBeUpdated;
        }

        private void BtnDlLatestBuild_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(Program.UpdaterHandler.VersionInfo.Downloads.Windows);
            }
            catch (Exception ex)
            {
                Logger.Error("Error while opening update download.", ex);
            }
        }

        private void BtnUpdateNotes_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(Program.UpdaterHandler.VersionInfo.ReleaseNotes);
            }
            catch (Exception ex)
            {
                Logger.Error("Error while opening update download.", ex);
            }
        }

        private void BtnProfileSelectImg_Click(object sender, EventArgs e)
        {
            using (var openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "Image files|*.png;*.jpg;*.jpeg;*.gif|All files|*.*";

                if (openDialog.ShowDialog() != DialogResult.OK)
                    return;

                var fileInfo = new FileInfo(openDialog.FileName);
                if (fileInfo.Length > PHOTO_MAX_SIZE)
                {
                    MessageBox.Show(this, "Cannot use this file. It cannot be larger than 1024kb.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                TbProfileImagePath.Text = openDialog.FileName;
            }
        }

        private void BtnProfileRmImg_Click(object sender, EventArgs e)
        {
            TbProfileImagePath.Text = "";
        }

        public void ExecuteInvoke(Action action)
            => this.Invoke(action);
        public T ExecuteInvoke<T>(Func<T> action)
        {
            var result = default(T);
            ExecuteInvoke(new Action(() => result = action()));
            return result;
        }

        private object InvokeSync = new object();
        public void ExecuteInvokeLock(Action action)
        {
            lock (InvokeSync)
            {
                ExecuteInvoke(action);
            }
        }
        public T ExecuteInvokeLock<T>(Func<T> action)
        {
            lock (InvokeSync)
            {
                return ExecuteInvoke(action);
            }
        }

        // lol. yeah. someone ask me to implement this xd
        private FormWindowState OldWinState = FormWindowState.Normal;
        private bool IsFullScreenLikeBrowser = false;
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F11)
            {
                IsFullScreenLikeBrowser = !IsFullScreenLikeBrowser;

                if (IsFullScreenLikeBrowser)
                {
                    OldWinState = this.WindowState;

                    this.WindowState = FormWindowState.Maximized;
                    this.FormBorderStyle = FormBorderStyle.None;
                }
                else
                {
                    this.WindowState = OldWinState;
                    this.FormBorderStyle = FormBorderStyle.Sizable;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void DgvModules_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != 3)
                return;

            var module = ModuleManager.ModuleBindings.ElementAtOrDefault(e.RowIndex);
            module?.OnClick?.Invoke();
        }

        private void CbCapSolver_SelectedIndexChanged(object sender, EventArgs e)
        {
            Configuration.Captcha.ServiceIndex = CbCapSolver.SelectedIndex;
        }

        private void BtnProfileLoadGroupsList_Click(object sender, EventArgs e)
        {
            using (var fileDialog = new OpenFileDialog())
            {
                fileDialog.Filter = "Text file documents|*.txt";
                fileDialog.Title = "Load groups to join";

                if (fileDialog.ShowDialog() != DialogResult.OK)
                    return;

                var urls = System.IO.File.ReadAllLines(fileDialog.FileName);
                var groupUrls = new List<string>();
                foreach (var url in urls)
                {
                    if (!Account.SteamGroupLink.IsMatch(url ?? ""))
                    {
                        Logger.Warn($"Cannot parse group '{url}'...");
                        continue;
                    }

                    groupUrls.Add(url);
                }

                var shouldClear = MessageBox.Show("You have already groups in existing list.\n" +
                    "Replace existing list?", "...", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                switch (shouldClear)
                {
                    case DialogResult.Yes:
                        Configuration.Profile.GroupsToJoin = groupUrls;
                        LbProfileGroupsToJoin.UpdateItems(Configuration.Profile.GroupsToJoin);
                        return;
                    case DialogResult.No:
                        {
                            var shouldMerge = MessageBox.Show("You want to merge these lists?", "...", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (shouldMerge != DialogResult.Yes)
                                return;

                            var _temp = Configuration.Profile.GroupsToJoin.ToList();
                            _temp.AddRange(groupUrls.Where(ng => // exclude existing
                            {
                                var link = Account.SteamGroupLink.Match(ng);
                                return !_temp.Any(og =>
                                {
                                    var oldLink = Account.SteamGroupLink.Match(og);
                                    if (!oldLink.Success)
                                        return false;

                                    return oldLink.Groups[1].Value.ToLower() == link.Groups[1].Value.ToLower();
                                });
                            }));
                            Configuration.Profile.GroupsToJoin = _temp;
                            LbProfileGroupsToJoin.UpdateItems(Configuration.Profile.GroupsToJoin);
                        }
                        break;
                }
            }
        }

        private void BtnProfileGroupsAdd_Click(object sender, EventArgs e)
        {
            using (var addDialog = new AddGroupDialog())
            {
                if (addDialog.ShowDialog() != DialogResult.OK)
                    return;

                var url = addDialog?.TbGroupUrl?.Text;
                if (!Account.SteamGroupLink.IsMatch(url ?? ""))
                {
                    MessageBox.Show("URL cannot be empty!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var _temp = Configuration.Profile.GroupsToJoin.ToList();
                _temp.Add(addDialog.TbGroupUrl.Text);
                Configuration.Profile.GroupsToJoin = _temp;

                LbProfileGroupsToJoin.UpdateItems(Configuration.Profile.GroupsToJoin);
            }
        }

        private void BtnProfileGroupsRm_Click(object sender, EventArgs e)
        {
            Logger.Trace($"{nameof(BtnProfileGroupsRm_Click)}(..., ...);");
            var index = LbProfileGroupsToJoin.SelectedIndex;
            if (index < 0 || index >= LbProfileGroupsToJoin.Items.Count)
                return;

            var _temp = Configuration.Profile.GroupsToJoin.ToList();
            var _uri = _temp.ElementAtOrDefault(index);
            Logger.Debug($"URL '{_uri}' removed.");
            _temp.RemoveAt(index);
            Configuration.Profile.GroupsToJoin = _temp;

            LbProfileGroupsToJoin.UpdateItems(Configuration.Profile.GroupsToJoin);
        }

        private void BtnGroupsClear_Click(object sender, EventArgs e)
        {
            Configuration.Profile.GroupsToJoin = new string[0];
            LbProfileGroupsToJoin.UpdateItems(Configuration.Profile.GroupsToJoin);
        }
    }
}
