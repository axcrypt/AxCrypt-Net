using AxCrypt.Api.Model.Reseller.Status;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AxCrypt.Api.Model.Reseller
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ResellerInformationApiModel : BaseApiModel
    {
        [JsonConstructor]
        public ResellerInformationApiModel()
        {
        }

        public enum CompanySize
        {
            Micro,
            Small,
            Medium,
            Large,
            Enterprise
        }

        public static ResellerInformationApiModel Empty { get; } = new ResellerInformationApiModel();

        [JsonProperty("resellerId")]
        public Guid ResellerId { get; set; }

        [JsonProperty("userEmail")]
        public string UserEmail { get; set; }

        [JsonProperty("companyName")]
        public string CompanyName { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("postalCode")]
        public string PostalCode { get; set; }

        [JsonProperty("companySize")]
        public CompanySize CompanySizeValue { get; set; }

        [JsonProperty("vatNumber")]
        public string VatNumber { get; set; }

        [JsonProperty("phoneNumber")]
        public string PhoneNumber { get; set; }

        [JsonProperty("firstname")]
        public string FirstName { get; set; }

        [JsonProperty("lastname")]
        public string LastName { get; set; }

        [JsonProperty("servingcountry")]
        public string ServingCountry { get; set; }

        [JsonProperty("registrationnumber")]
        public string RegistrationNumber { get; set; }

        [JsonProperty("contactName")]
        public string ContactName { get; set; }

        [JsonProperty("website")]
        public string Website { get; set; }

        [JsonProperty("expectedBusinessCustomers")]
        public int? ExpectedBusinessCustomers { get; set; }

        [JsonProperty("additionalInfo")]
        public string AdditionalInfo { get; set; }

        [JsonProperty("status")]
        public ResellerStatus Status { get; set; }

        [JsonProperty("reviewedBy")]
        public string ReviewedBy { get; set; }

        [JsonProperty("reviewedUtc")]
        public DateTime ReviewedUtc { get; set; }

        [JsonProperty("reviewNotes")]
        public string ReviewNotes { get; set; }

        [JsonProperty("greCaptchaResponse")]
        public string GreCaptchaResponse { get; set; }

        [JsonProperty("members")]
        public IEnumerable<ResellerMemberApiModel> Members { get; set; }
    }

    public class ResellerDashboardApiModel
    {
        [JsonProperty("resellerId")]
        public Guid ResellerId { get; set; }

        [JsonProperty("businesscount")]
        public int BusinessCount { get; set; }

        [JsonProperty("membercount")]
        public int MemberCount { get; set; }

        [JsonProperty("rolecount")]
        public int RoleCount { get; set; }
    }
}