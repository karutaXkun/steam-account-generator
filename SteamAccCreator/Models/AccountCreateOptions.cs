using SACModuleBase;
using SteamAccCreator.Web;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SteamAccCreator.Models
{
    public class AccountCreateOptions
    {
        /// <summary>
        /// Need to get working local cpatcha
        /// </summary>
        public Form ParentForm { get; set; }
        public Configuration Config { get; set; }
        public ProxyManager ProxyManager { get; set; }

        /// <summary>
        /// Needed to append number to account if random login disabled
        /// </summary>
        public int AccountNumber { get; set; }

        public ISACHandlerMailBox HandlerMailBox { get; set; }
        public ISACHandlerCaptcha HandlerImageCaptcha { get; set; }
        public ISACHandlerReCaptcha HandlerGoogleCaptcha { get; set; }
        public ISACHandlerUserAgent HandlerUserAgent { get; set; }

        /// <summary>
        /// Used to update accounts table
        /// </summary>
        public Action RefreshDisplayFn { get; set; }
        public Action<Color> SetStatusColorFn { get; set; }
        public Action<Action> ExecuteInUiFn { get; set; }
    }
}
