using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Identity.Client;
using Microsoft.Graph;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;

namespace MSAL.Net_GroupOveragesClaim.Authentication
{
    class OnBehalfOfAuthProvider : IAuthenticationProvider
    {
        private IConfidentialClientApplication _msalClient;
        private string[] _scopes;
        private string _assertion;

        public OnBehalfOfAuthProvider(string clientId, string tenantId, string clientSecret, string user_assertion, string[] scopes)
        {
            _assertion = user_assertion;
            _scopes = scopes;

            _msalClient = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithTenantId(tenantId)
                .Build();
        }

        async Task<string> GetAccessToken()
        {
            try
            {
                UserAssertion assertion = new UserAssertion(_assertion);

                AuthenticationResult result = await _msalClient.AcquireTokenOnBehalfOf(_scopes, assertion).ExecuteAsync();
                return result.AccessToken;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Error getting access token: {exception.Message}");
                return null;
            }
        }

        async Task IAuthenticationProvider.AuthenticateRequestAsync(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessToken());
        }

    }
}
