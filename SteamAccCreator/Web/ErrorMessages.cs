namespace SteamAccCreator.Web
{
    public class ErrorMessages
    {
        public class Steam
        {
            public const string UNKNOWN = "Steam has disallowed your IP making this account";
            public const string PROBABLY_IP_BAN = "Your IP probably banned by Steam.";
            public const string TRASH_MAIL = "Disposable mail address detected. This mail domain was banned.";
            public const string WRONG_CAPTCHA = "Wrong captcha!";
            public const string SIMILIAR_MAIL = "This e-mail address must be different from your own.";
            public const string INVALID_MAIL = "Please enter a valid email address.";
            public const string REGISTRATION = "There was an error with your registration, please try again.";
            public const string TIMEOUT = "You've waited too long to verify your email. Please try creating your account and verifying your email again.";
            public const string MAIL_UNVERIFIED = "Trying to verify mail...";
            public const string MAIL_UNVERIFIED_HAND_MODE = "Waiting for mail confirmation...";
        }

        public class Mail
        {
            public const string REQUEST_FAILED = "Cannot communicate with inbox service!";

            public const string NO_FREE_DOMAIN = "Use custom domain. No any public domain available at this moment!";
            public const string SERVICE_ERROR = "Mail service probably broken. You can try again later...";
        }

        public class RuCaptcha
        {
            public const string REQUEST_FAILED = "Cannot communicate with 2Captcha service!";
            public const string QUEUE_ID_EMPTY = "Cannot find 2Captcha queue ID!";
        }

        public class CaptchaSolutions
        {
            public const string REQUEST_FAILED = "Cannot communicate with CaptchaSolutions service!";
        }

        public class Account
        {
            public const string INITIAL_REQUEST_ERROR = "Failed to load Steam page.";
            public const string INITIAL_REQUEST_HTTP_ERROR = "HTTP request to load Steam page was failed!";

            public const string CONNECTION_UNSTABLE = "Internet or proxy connection is unstable!";

            public const string CAPTCHA_GET_ERROR = "Failed to load captcha information.";
            public const string CAPTCHA_GET_HTTP_ERROR = "HTTP request to load captcha was failed!";
            public const string CAPTCHA_LOOP_DETECTED = "Endless captcha detected! Change IP or use proxy!";
            public const string CAPTCHA_DOWNLOAD_IMAGE_ERROR = "Something went wrong while downloading captcha image...";
            public const string CAPTCHA_DOWNLOAD_IMAGE_HTTP_ERROR = "Failed to download captcha image!";
            public const string CAPTCHA_PROCESSING_ERROR = "Captcha processing error!";

            public const string VF_MAIL_ERROR = "Verify mail error!";
            public const string VF_MAIL_HTTP_ERROR = "Failed to request mail verification!";

            public const string CREATION_ID_NOT_FOUND = "Cannot find creation ID for this account!";

            public const string MCF_CHECK_ERROR = "Failed to check for mail confirmation!";
            public const string MCF_CHECK_HTTP_ERROR = MCF_CHECK_ERROR + " HTTP request failed! Retrying...";
            public const string MCF_SEARCH_MESSAGE_ERROR = "Something went wrong while searching message from Steam...";
            public const string MCF_SEARCH_MESSAGE_ERROR_FATAL = "Failed to find mail confirmation!";

            public const string MC_REQUEST_FAILED = "Failed to request confirmation link!";
            public const string MC_REQUEST_FAILED_FATAL = "Cannot confirm your mail!";
            public const string MC_LOAD_LINK_FAILED = "Failed to load data from confirmation link!";
            public const string MC_LOAD_LINK_FAILED_FATAL = "Something went wrong while loading data from link!";

            public const string LOGIN_CHECK_FAILED = "Failed to check Steam login!";
            public const string LOGIN_CHECK_FAILED_FATAL = "Cannot check Steam login!";

            public const string PASSWORD_CHECK_FAILED = "Failed to check Steam password!";
            public const string PASSWROD_CHECK_FAILED_FATAL = "Cannot check Steam password!";

            public const string CREATE_FAILED = "Failed to execute account creation!";
            public const string CREATE_FAILED_FATAL = "Cannot execute account creation!";

            public const string CREATE_REDIRECT_FAILED = "Failed to execute redirect!";
            public const string CREATE_REDIRECT_FAILED_FATAL = "Cannot execute redirect!";

            public const string TF_FAILED_TO_DISABLE = "Failed to disable SteamGuard!";
            public const string TF_FAILED_TO_DISABLE_FATAL = "Cannot disable SteamGuard!";
            public const string TF_FAILED_TO_ENABLE = "Failed to enable SteamGuard!";
            public const string TF_FAILED_TO_ENABLE_FATAL = "Cannot enable SteamGuard!";
            public const string TF_LOAD_LINK_FAILED = "|Steam guard| Failed to load data from confirmation link!";
            public const string TF_LOAD_LINK_FAILED_FATAL = "|Steam guard| Something went wrong while loading data from link!";
            public const string TF_SEARCH_MESSAGE_ERROR = "|Steam guard| Something went wrong while searching message from Steam...";
            public const string TF_SEARCH_MESSAGE_ERROR_FATAL = "Failed to find mail!";

            public const string AVATAR_UPLOAD_FAILED = "Failed to upload profile photo!";
            public const string AVATAR_UPLOAD_FAILED_FATAL = "Cannot upload profile photo!";

            public const string PROFILE_UPDATE_FAILED = "Failed to update profile!";
            public const string PROFILE_UPDATE_FAILED_FATAL = "Cannot update profile!";

            public const string FAILED_TO_CREATE_FATAL = "Stopped creation.";
        }

        public const string HTTP_FAILED = "HTTP request failed.";
        public const string UNKNOWN_ERROR = "Something went wrong...";
    }
}