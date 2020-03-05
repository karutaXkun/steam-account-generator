using Newtonsoft.Json;

namespace SteamAccCreator.Web.Steam.Account.Models
{
    public class UploadFileResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }
    }
}
