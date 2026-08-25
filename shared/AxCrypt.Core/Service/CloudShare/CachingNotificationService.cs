using AxCrypt.Api.Model.CloudShare;
using AxCrypt.Common;
using AxCrypt.Core.Crypto;

namespace AxCrypt.Core.Service.CloudShare
{
    public class CachingCloudShareService : ICloudShareService
    {
        private ICloudShareService _service;

        public CachingCloudShareService(ICloudShareService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            _service = service;
        }

        public LogOnIdentity Identity => throw new NotImplementedException();

        public async Task<Guid> ShareLinkAsync(CloudShareLinkApiModel cloudShareLinkApiModel)
        {
            return await _service.ShareLinkAsync(cloudShareLinkApiModel).Free();
        }

        public ICloudShareService Refresh()
        {
            return this;
        }
    }
}