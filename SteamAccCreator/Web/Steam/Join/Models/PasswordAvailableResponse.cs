using Newtonsoft.Json;

namespace SteamAccCreator.Web.Steam.Join.Models
{
    public class PasswordAvailableResponse
    {
        [JsonProperty("bAvailable")]
        public bool IsAvailable { get; protected set; }
    }
}
