using RestSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamAccCreator.Web.Steam
{
    public class SteamResponse<T>
    {
        public T Response { get; }
        public ReadOnlyCollection<IRestResponse> HttpResponses { get; }

        /// <summary>
        /// Is HTTP request successful
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// If somewhere was thrown exception
        /// </summary>
        public Exception Exception { get; }

        internal SteamResponse() { IsSuccess = false; }
        internal SteamResponse(T response, IRestResponse httpResponse)
        {
            Response = response;
            HttpResponses = new ReadOnlyCollection<IRestResponse>(new[] { httpResponse });
            IsSuccess = httpResponse?.IsSuccessful ?? false;
        }
        internal SteamResponse(T response, IEnumerable<IRestResponse> httpResponses)
        {
            Response = response;
            HttpResponses = new ReadOnlyCollection<IRestResponse>(httpResponses?.ToList() ?? new List<IRestResponse>());
            IsSuccess = HttpResponses.All(x=> x.IsSuccessful);
        }
        internal SteamResponse(Exception exception)
        {
            IsSuccess = false;
            Exception = exception;
        }
    }
}
