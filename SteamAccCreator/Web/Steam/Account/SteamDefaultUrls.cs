namespace SteamAccCreator.Web.Steam
{
    public partial class SteamDefaultUrls
    {
        public const string COMMUNITY_BASE = "https://steamcommunity.com/";

        public const string CO_PROFILES = COMMUNITY_BASE + "profiles/";
        public const string CO_PROFILE_BY_ID = CO_PROFILES + "{0}/";
        public const string CO_PROFILE_EDIT = CO_PROFILE_BY_ID + "edit";

        public const string CO_ACTIONS = COMMUNITY_BASE + "actions/";
        public const string CO_ACTIONS_FILE_UPLOADER = CO_ACTIONS + "FileUploader";

        public const string STORE_CHECKOUT = STORE_BASE + "checkout/";
        public const string STORE_ADD_FREE_LICENSE = STORE_CHECKOUT + "addfreelicense";

        public const string CO_GROUPS = COMMUNITY_BASE + "groups/";
    }
}
