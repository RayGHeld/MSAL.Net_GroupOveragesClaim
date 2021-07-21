using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Graph;
using Microsoft.Identity.Client;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

/// <summary>
/// Modeled after: https://docs.microsoft.com/en-us/graph/tutorials/dotnet-core?tutorial-step=3
/// </summary>
namespace MSAL.Net_GroupOveragesClaim.Authentication
{
    class ClientCredentialAuthProvider: IAuthenticationProvider
    {
        private IConfidentialClientApplication _msalClient;
        private string[] _scopes;

        public ClientCredentialAuthProvider(string clientId, string tenantId, string clientSecret, string[] scopes)
        {
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
                AuthenticationResult result = await _msalClient.AcquireTokenForClient(_scopes).ExecuteAsync();
                return result.AccessToken;
            } catch (Exception exception)
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
