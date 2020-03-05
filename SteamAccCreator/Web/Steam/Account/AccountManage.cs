using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SteamAccCreator.Web.Steam.Account
{
    public class AccountManage : Abstractions.SteamCategoryBase
    {
        public AccountManage(SteamWebClient steam) : base(steam) { }

        public SteamResponse<bool> EditProfile(string name, string realName,
            string country, string state, string city, string customUrl,
            string bio)
        {
            var editUrl = string.Format(SteamDefaultUrls.CO_PROFILE_EDIT, Steam.SteamId);

            var request = new RestRequest(editUrl, Method.POST);
            request.AddParameter("sessionID", Steam.SessionId);
            request.AddParameter("type", "profileSave");
            request.AddParameter("personaName", name);
            request.AddParameter("real_name", realName);
            request.AddParameter("country", country);
            request.AddParameter("state", state);
            request.AddParameter("city", city);
            if (customUrl != null)
                request.AddParameter("customURL", customUrl);
            request.AddParameter("summary", bio);

            request.AddHeader("Referer", $"{editUrl}?welcomed=1");

            var response = Execute(request);
            return new SteamResponse<bool>(response.IsSuccessful, response);
        }

        public SteamResponse<Models.UploadFileResponse> UploadAvatar(string filePath)
        {
            const long PHOTO_MAX_SIZE = 1048576;

            var request = new RestRequest(SteamDefaultUrls.CO_ACTIONS_FILE_UPLOADER, Method.POST);
            request.AddParameter("MAX_FILE_SIZE", PHOTO_MAX_SIZE);
            request.AddParameter("type", "player_avatar_image");
            request.AddParameter("sId", Steam.SteamId);
            request.AddParameter("sessionid", Steam.SessionId);
            request.AddParameter("doSub", "1");
            request.AddParameter("json", "1");
            request.AddFile("avatar", filePath);

            return Steam.Execute<Models.UploadFileResponse>(request);
        }

        public SteamResponse<bool> AddFreeLicense(long subId)
        {
            var request = new RestRequest(SteamDefaultUrls.STORE_ADD_FREE_LICENSE, Method.POST);
            request.AddParameter("action", "add_to_cart");
            request.AddParameter("subid", subId);
            request.AddParameter("sessionid", Steam.SessionId);

            var response = Execute(request);
            if (!response.IsSuccessful)
                return new SteamResponse<bool>(false, response);

            var success = Regex.IsMatch(response.Content, $"class=\"add_free_content_success_area\"|steam:\\/\\/subscriptioninstall\\/{subId}", RegexOptions.IgnoreCase);
            return new SteamResponse<bool>(success, response);
        }

        public SteamResponse<bool> JoinGroup(string groupLink)
        {
            var request = new RestRequest($"{SteamDefaultUrls.CO_GROUPS}{groupLink}", Method.POST);
            request.AddParameter("action", "join");
            request.AddParameter("sessionID", Steam.SessionId);

            var response = Execute(request);
            if (!response.IsSuccessful)
                return new SteamResponse<bool>(false, response);

            var success = Regex.IsMatch(response.Content, @"\""javascript\:ConfirmLeaveGroup", RegexOptions.IgnoreCase);
            return new SteamResponse<bool>(success, response);
        }
    }
}
