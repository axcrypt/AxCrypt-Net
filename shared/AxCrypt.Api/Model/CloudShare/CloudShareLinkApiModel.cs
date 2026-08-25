using Newtonsoft.Json;

namespace AxCrypt.Api.Model.CloudShare
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CloudShareLinkApiModel
    {
        [JsonProperty("fileId")]
        public long FileId { get; set; }

        [JsonProperty("owner")]
        public string Owner { get; set; }

        [JsonProperty("sharedFileName")]
        public string SharedFileName { get; set; }

        [JsonProperty("sharedLink")]
        public string SharedLink { get; set; }

        [JsonProperty("recipients")]
        public IEnumerable<string> Recipients { get; set; }

        [JsonProperty("personalMessage")]
        public string PersonalMessage { get; set; }

        [JsonProperty("sharePermission")]
        public string SharePermission { get; set; }

        [JsonProperty("allowDownload")]
        public bool AllowDownload { get; set; }

        [JsonProperty("allowReshare")]
        public bool AllowReshare { get; set; }

        [JsonProperty("visibleUntil")]
        public DateTime? VisibleUntil { get; set; }
    }
}