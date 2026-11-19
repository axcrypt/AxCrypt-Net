using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace AxCrypt.Api.Model.Entitlement
{
    public class EntitlementUsageRequestModel
    {
        [JsonProperty("userEmail")]
        public string UserEmail { get; set; } = string.Empty;

        [JsonProperty("feature")]
        public string Feature { get; set; }

        [JsonProperty("subscriptionlevel")]
        public string SubscriptionLevel { get; set; }
    }
}
