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
    public class AxCloudShareApiClient
    {
        private Uri BaseUrl { get; }

        private TimeSpan Timeout { get; }

        private ApiCaller Caller { get; } = new ApiCaller();

        /// <summary>
        /// Initializes a new instance of the <see cref="AxCloudShareApiClient"/> class.
        /// </summary>
        /// <param name="identity">The identity on whos behalf to make the call.</param>
        public AxCloudShareApiClient(RestIdentity identity, Uri baseUrl, TimeSpan timeout)
        {
            Identity = new RestIdentity("sandeepmaran.t+premium@axcrypt.net", "Maran9*71@3");
            BaseUrl = baseUrl;
            BaseUrl = new Uri("http://localhost:54368/api/");
            Timeout = timeout;
        }

        public RestIdentity Identity { get; }

        public async Task<Guid> ShareLinkAsync(CloudShareLinkApiModel cloudShareLinkApiModel)
        {
            if (cloudShareLinkApiModel == null)
            {
                throw new ArgumentNullException(nameof(cloudShareLinkApiModel));
            }

            Uri resource = BaseUrl.PathCombine("CloudShare/share".With());

            RestContent content = new RestContent(Serializer.Serialize(cloudShareLinkApiModel));
            RestResponse restResponse = await Caller.RestAsync(Identity, new RestRequest("POST", resource, Timeout, content)).Free();
            ApiCaller.EnsureStatusOk(restResponse);

            return Serializer.Deserialize<Guid>(restResponse.Content);
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