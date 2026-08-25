using AxCrypt.Api.Model.CloudShare;
using AxCrypt.Common;
using AxCrypt.Core.Crypto;
using AxCrypt.Core.Extensions;
using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Core.Service.CloudShare
{
    public class DeviceCloudShareService : ICloudShareService
    {
        private ICloudShareService _localService;
        private ICloudShareService _remoteService;

        public DeviceCloudShareService(ICloudShareService localService, ICloudShareService remoteService)
        {
            _localService = localService;
            _remoteService = remoteService;
        }

        public ICloudShareService Refresh()
        {
            return this;
        }

        public LogOnIdentity Identity
        {
            get
            {
                return _remoteService.Identity;
            }
        }

        public async Task<Guid> ShareLinkAsync(CloudShareLinkApiModel cloudShareLinkApiModel)
        {
            if (New<AxCryptOnlineState>().IsOnline && Identity != LogOnIdentity.Empty)
            {
                try
                {
                    return await _remoteService.ShareLinkAsync(cloudShareLinkApiModel).Free();
                }
                catch (ApiException aex)
                {
                    await aex.HandleApiExceptionAsync();
                }
            }

            return await _localService.ShareLinkAsync(cloudShareLinkApiModel).Free();
        }
    }
}