using Newtonsoft.Json;

namespace AxCrypt.Api.Model.CloudShare
{
    [JsonObject(MemberSerialization.OptIn)]
    public class KeyShareNotificationApiModel
    {
        [JsonProperty("fileId")]
        public long FileId { get; set; }

        [JsonProperty("owner")]
        public string Owner { get; set; }

        [JsonProperty("sharedFileName")]
        public string SharedFileName { get; set; }

        // Empty when a local file is shared; set to the cloud download URL when a cloud file is shared.
        [JsonProperty("sharedLink")]
        public string SharedLink { get; set; }

        [JsonProperty("recipients")]
        public IEnumerable<string> Recipients { get; set; }

        [JsonProperty("personalMessage")]
        public string PersonalMessage { get; set; }

        [JsonProperty("sharePermission")]
        public string SharePermission { get; set; }
    }
}