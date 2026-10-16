using Newtonsoft.Json;
using System;

namespace AxCrypt.Api.Model.Reseller
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ResellerMemberApiModel : BaseApiModel
    {
        [JsonConstructor]
        public ResellerMemberApiModel()
        {
        }

        [JsonProperty("resellerId")]
        public Guid ResellerId { get; set; }

        [JsonProperty("userEmail")]
        public string UserEmail { get; set; }

        [JsonProperty("role")]
        public ResellerMemberRole Role { get; set; }

        [JsonProperty("addedBy")]
        public string AddedBy { get; set; }
    }
}