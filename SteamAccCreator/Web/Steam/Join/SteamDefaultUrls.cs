namespace SteamAccCreator.Web.Steam
{
    public partial class SteamDefaultUrls
    {
        public const string JOIN_RES = "join/";
        public const string JOIN = STORE_BASE + JOIN_RES;

        public const string JOIN_REFRESH_CAPTCHA = JOIN + "refreshcaptcha";

        public const string CHECK_AVAILABLE = JOIN + "checkavail";
        public const string CHECK_AVAILABLE_PASSWORD = JOIN + "checkpasswordavail";

        public const string CREATE_ACCOUNT = JOIN + "createaccount";
        public const string AJAX_VERIFY_EMAIL = JOIN + "ajaxverifyemail";
        public const string AJAX_CHECK_VERIFIED = JOIN + "ajaxcheckemailverified";
        public const string ACCOUNT_VERIFY = STORE_BASE + "account/newaccountverification";
    }
}
