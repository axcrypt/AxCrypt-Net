using AxCrypt.Abstractions;
using AxCrypt.Abstractions.Rest;
using AxCrypt.Api.Model.CloudShare;
using AxCrypt.Common;
using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Api
{
    /// <summary>
    /// Provide basic api services using the AxCrypt API. All connection errors are thrown as OfflineApiExceptions, which must be caught and
    /// handled by the caller, and should be treated as 'temporarily offline'. They root cause can be both Internet connection issues as well
    /// as the servers being down.
    /// </summary>
    public class AxKeyShareNotificationApiClient
    {
        private Uri BaseUrl { get; }

        private TimeSpan Timeout { get; }

        private ApiCaller Caller { get; } = new ApiCaller();

        /// <summary>
        /// Initializes a new instance of the <see cref="AxKeyShareNotificationApiClient"/> class.
        /// </summary>
        /// <param name="identity">The identity on whos behalf to make the call.</param>
        public AxKeyShareNotificationApiClient(RestIdentity identity, Uri baseUrl, TimeSpan timeout)
        {
            Identity = identity;
            BaseUrl = baseUrl;
            BaseUrl = baseUrl;
            Timeout = timeout;
        }

        public RestIdentity Identity { get; }

        public Task<bool> SendCloudKeyShareNotificationAsync(KeyShareNotificationApiModel cloudShareLinkApiModel)
        {
            return PostNotificationAsync(cloudShareLinkApiModel, "keyshare/notify/cloud");
        }

        public Task<bool> SendLocalKeyShareNotificationAsync(KeyShareNotificationApiModel keyShareApiModel)
        {
            return PostNotificationAsync(keyShareApiModel, "keyshare/notify/local");
        }

        private async Task<bool> PostNotificationAsync(KeyShareNotificationApiModel keyShareApiModel, string path)
        {
            if (keyShareApiModel == null)
            {
                throw new ArgumentNullException(nameof(keyShareApiModel));
            }

            Uri resource = BaseUrl.PathCombine(path.With());

            RestContent content = new RestContent(Serializer.Serialize(keyShareApiModel));
            RestResponse restResponse = await Caller.RestAsync(Identity, new RestRequest("POST", resource, Timeout, content)).Free();
            ApiCaller.EnsureStatusOk(restResponse);

            return Serializer.Deserialize<bool>(restResponse.Content);
        }

        private static IStringSerializer Serializer
        {
            get
            {
                return New<IStringSerializer>();
            }
        }
    }
}