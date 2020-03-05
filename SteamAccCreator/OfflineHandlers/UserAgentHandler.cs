using SACModuleBase;
using SACModuleBase.Models;

namespace SteamAccCreator.OfflineHandlers
{
    public class UserAgentHandler : ISACHandlerUserAgent
    {
        public bool ModuleEnabled { get; set; } = true;

        public string GetUserAgent()
            => UserAgentList.Get();

        public void ModuleInitialize(SACInitialize initialize) { }
    }
}
