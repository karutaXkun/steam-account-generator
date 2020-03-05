using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace SteamAccCreator.Web.Steam.Join.Models
{
    public class LoginAvailableResponse
    {
        [JsonProperty("bAvailable")]
        public bool IsAvailable { get; protected set; }
        [JsonProperty("rgSuggestions")]
        public ReadOnlyCollection<string> Suggestions { get; protected set; }
    }
}
