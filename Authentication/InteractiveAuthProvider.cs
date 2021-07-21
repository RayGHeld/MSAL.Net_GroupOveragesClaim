using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Graph;
using Microsoft.Identity.Client;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Linq;

namespace MSAL.Net_GroupOveragesClaim.Authentication
{

    class InteractiveAuthProvider : IAuthenticationProvider
    {

        private IPublicClientApplication _msalClient;
        private string[] _scopes;
        private string _redirectUri;

        public InteractiveAuthProvider(string clientId, string tenantId, string redirectUri, string[] scopes)
        {
            _scopes = scopes;
            _redirectUri = redirectUri;

            _msalClient = PublicClientApplicationBuilder
                .Create(clientId)
                .WithRedirectUri(_redirectUri)
                .WithTenantId(tenantId)
                .Build();
        }

        async Task<string> GetAccessToken()
        {
            IEnumerable<IAccount> accounts = await _msalClient.GetAccountsAsync();

            try
            {
                AuthenticationResult result = await _msalClient.AcquireTokenSilent(_scopes, accounts.FirstOrDefault() ).ExecuteAsync();
                return result.AccessToken;
            } catch (MsalUiRequiredException)
            {
                Console.WriteLine("User Interaction is required...");
                try
                {
                    AuthenticationResult result = await _msalClient.AcquireTokenInteractive(_scopes).ExecuteAsync();
                    return result.AccessToken;
                } catch (MsalException msalEx)
                {
                    Console.WriteLine($"Error acquiring token: {msalEx}");
                    return null;
                }
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
