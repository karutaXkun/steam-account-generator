using Newtonsoft.Json;

namespace SteamAccCreator.Models.Steam
{
    public class CaptchaResponse
    {
        [JsonProperty("gid")]
        public string Gid { get; set; }
        [JsonProperty("type")]
        public int Type { get; set; }
        [JsonProperty("sitekey")]
        public string SiteKey { get; set; }
    }
}
