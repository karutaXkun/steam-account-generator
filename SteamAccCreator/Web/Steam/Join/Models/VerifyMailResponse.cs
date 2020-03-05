using Newtonsoft.Json;

namespace SteamAccCreator.Web.Steam.Join.Models
{
    public class VerifyMailResponse
    {
        [JsonProperty("success")]
        public int Status { get; protected set; }
        [JsonProperty("sessionid")]
        public string CreationId { get; protected set; }
        [JsonProperty("details")]
        public string Details { get; protected set; }
    }
}
