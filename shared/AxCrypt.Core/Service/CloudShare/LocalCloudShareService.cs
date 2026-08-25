using AxCrypt.Api.Model.CloudShare;
using AxCrypt.Core.Crypto;

namespace AxCrypt.Core.Service.CloudShare
{
    public class LocalCloudShareService : ICloudShareService
    {
        public LocalCloudShareService()
        {
        }

        public ICloudShareService Refresh()
        {
            return this;
        }

        public LogOnIdentity Identity
        {
            get;
        }

        public Task<Guid> ShareLinkAsync(CloudShareLinkApiModel cloudShareLinkApiModel)
        {
            return Task.FromResult(Guid.Empty);
        }
    }
}