using Newtonsoft.Json;

namespace SteamAccCreator.Web.Steam.Models
{
    public class SteamCaptcha
    {
        [JsonProperty("gid")]
        public string Gid { get; protected set; }
        [JsonProperty("type")]
        public int Type { get; protected set; }
        [JsonProperty("sitekey")]
        public string SiteKey { get; protected set; }
    }
}
