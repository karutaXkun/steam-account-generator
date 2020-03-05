using SteamAccCreator.Models;
using SteamAccCreator.Web;
using System;
using System.Collections.Generic;
using System.IO;
using io = System.IO;

namespace SteamAccCreator.File
{
    public class FileHandler
    {
        private static object Sync = new object();

        private Account Account;
        private OutputConfig Config => Account?.Config?.Output;
        private Action<string> UpdateStatusFn;
        private Action<string> LogInfo;
        private Action<string> LogWarning;
        private Action<string, Exception> LogError;
        public FileHandler(Account account, Action<string> updateStatusFn,
            Action<string> logInfoFn,
            Action<string> logWarningFn,
            Action<string, Exception> logErrorFn)
        {
            Account = account ?? throw new ArgumentNullException($"{nameof(account)} cannot be null!");
            UpdateStatusFn = updateStatusFn ?? throw new ArgumentNullException($"{nameof(updateStatusFn)} cannot be null!");
            LogInfo = logInfoFn ?? throw new ArgumentNullException($"{nameof(logInfoFn)} cannot be null!");
            LogWarning = logWarningFn ?? throw new ArgumentNullException($"{nameof(logWarningFn)} cannot be null!");
            LogError = logErrorFn ?? throw new ArgumentNullException($"{nameof(logErrorFn)} cannot be null!");
        }

        public bool Save()
        {
            if (Config == null)
            {
                UpdateStatus("Output configuration is null. Don't know where to save this account!");
                Warn("Output configuration is null. Don't know where to save this account!");
                return false;
            }

            UpdateStatus("Saving account to file...");
            Info($"Saving account to file...");

            lock (Sync)
            {
                try
                {
                    var directoryPath = new FileInfo(Config.Path).DirectoryName;
                    if (!Directory.Exists(directoryPath))
                        Utility.MkDirs(directoryPath);

                    var account = string.Empty;
                    switch (Config.SaveType)
                    {
                        case SaveType.FormattedTxt:
                            Info("Saving in formatted text mode...");
                            if (Config.WriteEmails)
                                account = $"Mail:\t\t\t\t{Account.Mail}\n";
                            account += $"Login:\t\t\t{Account.Login}\n" +
                                       $"Password:\t{Account.Password}\n" +
                                       $"Created at:\t{DateTime.Now}\n" +
                                       $"URL:\t\t\t\t{((Account.SteamIdLong.HasValue) ? $"https://steamcommunity.com/profiles/{Account.SteamIdLong.Value}" : "-")}\n" +
                                       $"###########################";
                            break;
                        case SaveType.KeepassCsv:
                            Info("Saving in CSV mode...");
                            {
                                var parts = new List<string>()
                                {
                                    CsvEscape(Account.Login),
                                    CsvEscape(Account.Login),
                                    CsvEscape(Account.Password)
                                };
                                if (Account.SteamIdLong.HasValue)
                                    parts.Add(CsvEscape($"https://steamcommunity.com/profiles/{Account.SteamIdLong.Value}"));
                                else
                                    parts.Add("");

                                if (Config.WriteEmails)
                                    parts.Add(CsvEscape(Account.Mail));

                                account = string.Join(",", parts);
                            }
                            break;
                        case SaveType.PlainTxt:
                        default:
                            Info("Saving in plain text mode...");
                            account = $"{Account.Login}:{Account.Password}";
                            if (Config.WriteEmails)
                                account += $":{Account.Mail}";
                            break;
                    }

                    io.File.AppendAllText(Config.Path, $"{account}\n");

                    return true;
                }
                catch (Exception ex)
                {
                    UpdateStatus("Cannot save this account at this moment!");
                    Error("Cannot save this account at this moment!", ex);
                }
            }

            return false;
        }

        private string CsvEscape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            var shouldEscape = s.Contains(",") || s.Contains("\"") || s.Contains("\n");
            if (!shouldEscape)
                return s;

            return $"\"{s.Replace("\"", "\"\"")}\"";
        }

        private void UpdateStatus(string message)
            => UpdateStatusFn?.Invoke(message);

        private void Info(string message)
            => LogInfo?.Invoke(message);
        private void Warn(string message)
            => LogWarning?.Invoke(message);
        private void Error(string message, Exception exception)
            => LogError?.Invoke(message, exception);
    }
}
