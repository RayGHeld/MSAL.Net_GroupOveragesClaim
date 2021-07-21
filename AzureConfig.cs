using System;
using System.Collections.Generic;
using System.Text;

namespace MSAL.Net_GroupOveragesClaim
{
    public class AzureConfig
    {
        public string ClientId { get; set; }
        public string TenantId { get; set; }
        public string CallbackPath { get; set; }
        public string ClientSecret { get; set; }
        public string[] AppScopes { get; set; }
        public string[] GraphScopes { get; set; }
    }
}
