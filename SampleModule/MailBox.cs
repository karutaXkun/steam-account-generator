using MailKit.Net.Imap;
using MimeKit;
using SACModuleBase.Attributes;
using SACModuleBase.Models;
using SACModuleBase.Models.Mail;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SampleModule
{
    [SACModuleInfo("C89C87D9-D9FE-447B-85A6-92203F0C3397", "IMAP mail handler", "1.0.0.0")]
    public class MailBox : SACModuleBase.ISACHandlerMailBox
    {
        public bool ModuleEnabled { get; set; } = true;

        private ConfigManager<Models.MailConfig> Config;
        private SACModuleBase.ISACLogger Logger;
        public void ModuleInitialize(SACInitialize initialize)
        {
            Config = new ConfigManager<Models.MailConfig>(initialize.ConfigurationPath, "mail.json",
                new Models.MailConfig(), Misc.MailBoxConfigSync);
            if (!Config.Load())
                Config.Save();

            Logger = initialize.Logger;
        }

        public MailBoxResponse GetMailBox(MailBoxRequest request)
        {
            if (!Config.Load())
            {
                Config.Save();
                return null;
            }
            return new MailBoxResponse(Config.Running.Email);
        }

        public IReadOnlyCollection<MailBoxMailItem> GetMails(MailBoxResponse response)
        {
            Logger.Info($"Getting mails from {response.Email}...");
            var mails = new List<MailBoxMailItem>();
            if (Config.Load())
            {
                using (var client = new ImapClient())
                {
                    try
                    {
                        Logger.Info("Connecting to IMAP...");
                        client.Connect(Config.Running.Host, Config.Running.Port, Config.Running.UseSsh);

                        Logger.Info("Authenticating on IMAP...");
                        client.Authenticate(Config.Running.Email, Config.Running.Password);

                        var inbox = client.Inbox;
                        Logger.Info("Getting inbox...");
                        inbox.Open(MailKit.FolderAccess.ReadWrite);

                        var unreadSort = inbox.Sort(MailKit.Search.SearchQuery.NotSeen, new[] { MailKit.Search.OrderBy.Date, MailKit.Search.OrderBy.Subject });
                        var unread = inbox.Fetch(unreadSort, MailKit.MessageSummaryItems.All);
                        foreach (var inboxMail in unread)
                        {
                            if (inboxMail == null)
                                continue;

                            Logger.Info("Unread message found...");
                            try
                            {
                                inbox?.SetFlags(inboxMail.Index, MailKit.MessageFlags.Seen, true);
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn("Mark as read error.", ex);
                            }
                            if (!(inboxMail?.NormalizedSubject?.ToLower()?.Contains("steam") ?? false))
                                continue;

                            var mailWithData = inbox.GetMessage(inboxMail.UniqueId);

                            var envelope = inboxMail?.Envelope;
                            var _body = mailWithData?.HtmlBody ?? mailWithData?.TextBody ?? "";
                            var _from = envelope?.From?.Mailboxes?.FirstOrDefault()?.Address
                                ?? mailWithData?.From?.FirstOrDefault()?.Name
                                ?? "noreply@steampowered.com";
                            var _to = envelope?.To?.Mailboxes?.FirstOrDefault()?.Address
                                ?? mailWithData?.To?.FirstOrDefault()?.Name
                                ?? response?.Email
                                ?? "unknown";
                            var _subject = inboxMail?.NormalizedSubject
                                ?? mailWithData?.Subject
                                ?? "unknown [will be steam confirmation]";

                            var mail = new MailBoxMailItem(_from, _to, _subject, _body);
                            mails.Add(mail);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error getting mails...", ex);
                    }
                }
                Logger.Info($"Getting mails from {response.Email} success!");
            }
            return new ReadOnlyCollection<MailBoxMailItem>(mails);
        }
    }
}
