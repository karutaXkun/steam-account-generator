using Newtonsoft.Json;

namespace SteamAccCreator.Web.Steam.Join.Models
{
    public class CreateAccountResponse
    {
        [JsonProperty("bSuccess")]
        public bool IsSuccess { get; protected set; }
        [JsonProperty("bInSteamClient")]
        public bool IsInSteamClient { get; protected set; }
        [JsonProperty("eresult")]
        public int EnumResult { get; protected set; } // only by my opinion xd
    }
}
