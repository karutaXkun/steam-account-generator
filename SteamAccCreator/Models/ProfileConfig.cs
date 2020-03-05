using System;
using System.Collections.Generic;

namespace SteamAccCreator.Models
{
    [Serializable]
    public class ProfileConfig
    {
        public bool Enabled { get; set; } = false;
        public string Name { get; set; } = "";
        public string RealName { get; set; } = "";
        public bool Url { get; set; } = false;
        public string Bio { get; set; } = "";
        public string Image { get; set; } = "";
        public string Country { get; set; } = "";
        public string State { get; set; } = "";
        public string City { get; set; } = "";

        public bool DoJoinToGroups { get; set; } = false;
        public IEnumerable<string> GroupsToJoin { get; set; } = new string[0];
    }
}
