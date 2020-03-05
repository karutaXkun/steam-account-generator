using RestSharp;
using System;

namespace SteamAccCreator.Web.Steam.Abstractions
{
    public abstract class SteamCategoryBase
    {
        internal SteamWebClient Steam;
        internal RestClient HttpClient => Steam.HttpClient;

        internal SteamCategoryBase(SteamWebClient steam)
        {
            Steam = steam;
        }

        internal IRestResponse Execute(IRestRequest request)
            => HttpClient.Execute(request);
    }
}
