using Microsoft.Graph;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace MSAL.Net_GroupOveragesClaim.Authentication
{
    class ManualTokenProvider : IAuthenticationProvider
    {
        string _accessToken;

        public ManualTokenProvider ( string accessToken)
        {
            _accessToken = accessToken;
        }

        async Task IAuthenticationProvider.AuthenticateRequestAsync(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("ConsistencyLevel", "eventual");
        }
    }
}
