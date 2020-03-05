using System;
using System.Text.RegularExpressions;

namespace SACModuleBase.Models.Mail
{
    public class MailBoxResponse
    {
        public bool Success { get; }
        public string Email { get; }
        public string Comment { get; private set; }

        public MailBoxResponse() { Success = false; }
        public MailBoxResponse(string email) : this(email, null) { }
        public MailBoxResponse(string email, string comment = null)
        {
            Email = email;
            Success = Regex.IsMatch(email ?? "", @"^\S+\@\S+$");
            Comment = comment;
        }

        public static MailBoxResponse Error(string comment)
            => new MailBoxResponse() { Comment = comment };
    }
}
