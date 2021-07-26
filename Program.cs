using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Identity.Client;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Graph;
using System.Net.Http;

using System.Text.Json;


/// <summary>
/// https://docs.microsoft.com/en-us/azure/active-directory/develop/access-tokens
/// </summary>
namespace MSAL.Net_GroupOveragesClaim
{
    class Program
    {
		static readonly HttpClient _httpClient = new HttpClient();

		static AzureConfig _config = null;
		public static AzureConfig AzureSettings
		{
			get
			{
				// only load this once when the app starts.
				// To reload, you will have to set the variable _config to null again before calling this property
				if (_config == null)
				{
					_config = new AzureConfig();
					IConfiguration builder = new ConfigurationBuilder()
						.SetBasePath(System.IO.Directory.GetCurrentDirectory())
						.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
						.Build();

					ConfigurationBinder.Bind(builder.GetSection("Azure"), _config);
				}

				return _config;
			}
		}

		static IPublicClientApplication _msal_publicClient;
		static IPublicClientApplication Msal_PublicClient
        {
            get
            {
				if(_msal_publicClient == null)
                {
					IPublicClientApplication app = PublicClientApplicationBuilder
						.Create(AzureSettings.ClientId)
						.WithAuthority(AzureCloudInstance.AzurePublic, AzureSettings.TenantId)
						.WithRedirectUri(AzureSettings.CallbackPath)
						.Build();

					_msal_publicClient = app;
                }
				return _msal_publicClient;
			}
        }

		// use this for the client credentials grant flow
		static IConfidentialClientApplication _msal_confidentialClient;
		static IConfidentialClientApplication Msal_ConfidentialClient
        {
			get
            {
				if(_msal_confidentialClient == null)
                {
					_msal_confidentialClient = ConfidentialClientApplicationBuilder
						.Create(AzureSettings.ClientId)
						.WithClientSecret(AzureSettings.ClientSecret)
						.WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
						.WithTenantId(AzureSettings.TenantId)
						.Build();
                }
				return _msal_confidentialClient;
            }
        }

		static string userId = string.Empty;

		/// <summary>
		/// Main method that runs this application
		/// </summary>
		/// <param name="args"></param>
		static void Main(string[] args)
        {
            Console.WriteLine("Starting test application...");
			
			string accessToken = string.Empty;
			string graphToken = string.Empty;
			bool client_credentials = false;

			// Only an interactive flow makes sense to log into this application
			accessToken = GetAccessToken_UserInteractive(AzureSettings.AppScopes).Result;

			// get the group overages claim if it exists in the access token
			string groupOveragesUrl = Get_GroupsOverageClaimURL(accessToken);

			// if there is a value for groupOveragesUrl, then we need to make a graph call for groups
			Console.WriteLine($"\nNew Microsoft Graph Group Membership URL:\n{groupOveragesUrl}");

			if(groupOveragesUrl != string.Empty)
            {
				bool loop = true;
				do
				{

					Console.WriteLine($"\nHow do you want to get an access token for the Microsoft Graph request?");
					Console.WriteLine($"1: User token for the currently signed in user ( refresh token flow )");
					Console.WriteLine($"2: Application token using the client credentials grant flow");
					Console.WriteLine($"Any other key to Exit");
					Console.Write("Enter choice > ");
					string choice = Console.ReadLine();

					switch (choice.Trim())
					{
						case "1":
							graphToken = Get_GraphTokenUsingRefeshToken(AzureSettings.GraphScopes).Result;
							break;
						case "2":
							graphToken = GetAccessToken_ClientCredentials(new string[] { "https://graph.microsoft.com/.default" }).Result;
							client_credentials = true;
							break;
						default:
							Console.WriteLine("Exiting...");
							loop = false;
							break;
					}
					choice = string.Empty;

                    if (loop)
                    {
						Console.WriteLine($"How do you want to get the groups?\n1: .Net HTTP Request\n2: Graph .Net SDK");
						Console.Write("Enter choice > ");
						choice = Console.ReadLine();
						
						switch (choice.Trim())
						{
							case "1":
								Console.WriteLine("Getting group list via a .Net HTTP request...");
								Get_Groups_HTTP_Method(graphToken, groupOveragesUrl).Wait();
								break;
							case "2":
								Console.WriteLine("Getting group list via the Graph .Net SDK...");
								Get_Groups_GraphSDK_Method(graphToken, !client_credentials).Wait();
								break;
							default:
								Console.WriteLine("Not a valid choice...");
								break;
						}
                    }
				} while (loop);
            } else
            {
				Console.WriteLine("\nNo group overages claim found in token...");
				Console.WriteLine($"\nPress any key to exit...");
				Console.ReadKey();            
			}


		}

		/// <summary>
		/// Entry point to make the request to Microsoft graph using the .Net HTTP Client
		/// </summary>
		/// <param name="graphToken"></param>
		/// <returns></returns>
		private static async Task Get_Groups_HTTP_Method(string graphToken, string url)
        {
			List<Group> groupList = new List<Group>();
						
			groupList = await Graph_Request_viaHTTP(graphToken, url);
			foreach (Group g in groupList)
			{
				Console.WriteLine($"Group Id: {g.Id} : Display Name: {g.DisplayName}");
			}
		}

		/// <summary>
		/// Entry point to make the request to Microsoft Graph using the Graph sdk and outputs the list to the console.
		/// </summary>
		/// <param name="graphToken"></param>
		/// <returns></returns>
		private static async Task Get_Groups_GraphSDK_Method(string graphToken, bool me_endpoint)
        {
			List<Group> groupList = new List<Group>();

			groupList = await Get_GroupList_GraphSDK(graphToken, me_endpoint);
			foreach (Group g in groupList)
			{
				Console.WriteLine($"Group Id: {g.Id} : Display Name: {g.DisplayName}");
			}
		}

		/// <summary>
		/// Signs the current user in and gets an access token for the scopes specified
		/// </summary>
		/// <param name="scopes"></param>
		/// <returns></returns>
		private static async Task<string> GetAccessToken_UserInteractive(string[] scopes)
		{

			string accessToken = string.Empty;

			AuthenticationResult authResult = null;
			IEnumerable<IAccount> accounts = await Msal_PublicClient.GetAccountsAsync();

			try
			{
				authResult = await Msal_PublicClient.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
				accessToken = authResult.AccessToken;
			}
			catch (MsalUiRequiredException)
			{
				authResult = await Msal_PublicClient.AcquireTokenInteractive(scopes).ExecuteAsync();
				accessToken = authResult.AccessToken;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Authentication error: {ex.Message}");
			}

			Console.WriteLine($"Access token: {accessToken}\n");

			return accessToken;
		}

		private static async Task<string> GetAccessToken_ClientCredentials(string[] scopes)
        {
			string accessToken = string.Empty;
			AuthenticationResult authResult = null;

			try
            {
				authResult = await Msal_ConfidentialClient.AcquireTokenForClient(scopes).ExecuteAsync();
				accessToken = authResult.AccessToken;
            } catch ( Exception ex)
            {
				Console.WriteLine($"Authentication error: {ex.Message}");
            }

			Console.WriteLine($"Access token: {accessToken}\n");

			return accessToken;
        }

		/// <summary>
		/// Exchanges the current cached refresh token for an access token for the scopes specified.
		/// This will only work for previously consented scopes.
		/// </summary>
		/// <param name="scopes"></param>
		/// <returns></returns>
		private static async Task<string> Get_GraphTokenUsingRefeshToken(string[] scopes)
        {
			string accessToken = string.Empty;

			AuthenticationResult authResult = null;
			IEnumerable<IAccount> accounts = await Msal_PublicClient.GetAccountsAsync();

			try
			{
				authResult = await Msal_PublicClient.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
				accessToken = authResult.AccessToken;
			}
			catch (MsalUiRequiredException)
			{
				authResult = await Msal_PublicClient.AcquireTokenInteractive(scopes).ExecuteAsync();
				accessToken = authResult.AccessToken;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Authentication error: {ex.Message}");
			}

			Console.WriteLine($"Access token: {accessToken}\n");

			return accessToken;
		}

		/// <summary>
		/// Looks for a groups overage claim in an access token and returns the value if found.
		/// </summary>
		/// <param name="accessToken"></param>
		/// <returns></returns>
		private static string Get_GroupsOverageClaimURL(string accessToken)
        {
			JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
			string claim = string.Empty;
			string sources = string.Empty;
			string originalUrl = string.Empty;
			string newUrl = string.Empty;

            try
            {
				// use the user id in the new graph url since the old overage link is for aad graph which is being deprecated.
				userId = token.Claims.First(c => c.Type == "oid").Value;

				// getting the claim name to properly parse from the claim sources but the next 3 lines of code are not needed,
				// just for demonstration purposes only so you can see the original value that was used in the token.
				claim = token.Claims.First(c => c.Type == "_claim_names").Value;
				sources = token.Claims.First(c => c.Type == "_claim_sources").Value;
				originalUrl = sources.Split("{\"" + claim.Split("{\"groups\":\"")[1].Replace("}","").Replace("\"","") + "\":{\"endpoint\":\"")[1].Replace("}","").Replace("\"", "");
				
				// make sure the endpoint is specific for your tenant -- .gov for example for gov tenants, etc.
				newUrl = $"https://graph.microsoft.com/v1.0/users/{userId}/memberOf?$orderby=displayName&$count=true";

				Console.WriteLine($"Original Overage URL:\n{originalUrl}");
				//Console.WriteLine($"New URL: {newUrl}");


			} catch {
				// no need to do anything because the claim does not exist
			} 

			return newUrl;
        }

		/// <summary>
		/// Calls Microsoft Graph via a HTTP request.  Handles paging in the request
		/// </summary>
		/// <param name="user_access_token"></param>
		/// <returns>List of Microsoft Graph Groups</returns>
		private static async Task<List<Group>> Graph_Request_viaHTTP(string user_access_token, string url)
        {
			string json = string.Empty;
			//string url = "https://graph.microsoft.com/v1.0/me/memberOf?$orderby=displayName&$count=true";
			List<Group> groups = new List<Group>();

			// todo: check for the count parameter in the request and add if missing

			/*
			 * refer to this documentation for usage of the http client in .net
			 * https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient?view=net-5.0
			 * 
			 */

			// add the bearer token to the authorization header for this request
			_httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue( "Bearer", user_access_token);
			
			// adding the consistencylevel header value if there is a $count parameter in the request as this is needed to get a count
			// this only needs to be done one time so only add it if it does not exist already.  It is case sensitive as well.
			// if this value is not added to the header, the results will not sort properly -- if that even matters for your scenario
			if(url.Contains("&$count", StringComparison.OrdinalIgnoreCase))
            {
                if (!_httpClient.DefaultRequestHeaders.Contains("ConsistencyLevel"))
                {
					_httpClient.DefaultRequestHeaders.Add("ConsistencyLevel", "eventual");
                }
            }
			
			// while loop to handle paging
			while(url != string.Empty)
            {
				HttpResponseMessage response = await _httpClient.GetAsync(new Uri(url));
				url = string.Empty; // clear now -- repopulate if there is a nextlink value.

				if (response.IsSuccessStatusCode)
				{
					json = await response.Content.ReadAsStringAsync();

					// Console.WriteLine(json);

					using (JsonDocument document = JsonDocument.Parse(json))
					{
						JsonElement root = document.RootElement;
						// check for the nextLink property to see if there is paging that is occuring for our while loop
						if (root.TryGetProperty("@odata.nextLink", out JsonElement nextPage))
                        {
							url = nextPage.GetString();
                        }
						JsonElement valueElement = root.GetProperty("value"); // the values

						// loop through each value in the value array
						foreach (JsonElement value in valueElement.EnumerateArray())
						{
							if (value.TryGetProperty("@odata.type", out JsonElement objtype))
							{
								// only getting groups -- roles will show up in this graph query as well.
								// If you want those too, then remove this if filter check
								if (objtype.GetString() == "#microsoft.graph.group")
								{
									Group g = new Group();

									// specifically get each property you want here and populate it in our new group object
									if (value.TryGetProperty("id", out JsonElement id)) { g.Id = id.GetString(); }
									if (value.TryGetProperty("displayName", out JsonElement displayName)) { g.DisplayName = displayName.GetString(); }

									groups.Add(g);
								}
							}
						}
					}
				} else
                {
					Console.WriteLine($"Error making graph request:\n{response.ToString()}");
                }
			} // end while loop
	
			return groups;
        }

		/// <summary>
		/// Calls the Me.MemberOf endpoint in Microsoft Graph and handles paging
		/// </summary>
		/// <param name="graphToken"></param>
		/// <returns>List of Microsoft Graph Groups</returns>
		private static async Task<List<Group>> Get_GroupList_GraphSDK(string graphToken, bool use_me_endpoint)
        {

			GraphServiceClient client;

			Authentication.ManualTokenProvider authProvider = new Authentication.ManualTokenProvider(graphToken);

			client = new GraphServiceClient(authProvider);
			IUserMemberOfCollectionWithReferencesPage membershipPage = null;

			HeaderOption option = new HeaderOption("ConsistencyLevel","eventual");

			if (use_me_endpoint)
            {
                if (!client.Me.MemberOf.Request().Headers.Contains(option))
                {
					client.Me.MemberOf.Request().Headers.Add(option);
                }

				membershipPage = await client.Me.MemberOf
					.Request()
					.OrderBy("displayName&$count=true") // todo: find the right way to add the generic query string value for count
					.GetAsync();
            } else
            {
                if (!client.Users[userId].MemberOf.Request().Headers.Contains(option))
                {
					client.Users[userId].MemberOf.Request().Headers.Add(option);
                }

				membershipPage = await client.Users[userId].MemberOf
					.Request()
					.OrderBy("displayName&$count=true")
					.GetAsync();
            }

			List<Group> allItems = new List<Group>();			
			
			if(membershipPage != null)
            {
				foreach(DirectoryObject o in membershipPage)
                {
					if(o is Group)
                    {
						allItems.Add((Group)o);
                    }
                }

				while (membershipPage.AdditionalData.ContainsKey("@odata.nextLink") && membershipPage.AdditionalData["@odata.nextLink"].ToString() != string.Empty)
                {
					membershipPage = await membershipPage.NextPageRequest.GetAsync();
					foreach (DirectoryObject o in membershipPage)
					{
						if (o is Group)
						{
							allItems.Add(o as Group);
						}
					}
				}

            }

             return allItems;

		}

	}
}
