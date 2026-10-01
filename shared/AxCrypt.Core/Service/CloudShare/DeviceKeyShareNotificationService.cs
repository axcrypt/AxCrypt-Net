using AxCrypt.Api.Model.CloudShare;
using AxCrypt.Common;
using AxCrypt.Core.Crypto;
using AxCrypt.Core.Extensions;
using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Core.Service.CloudShare
{
    public class DeviceKeyShareNotificationService : IKeyShareNotificationService
    {
        private IKeyShareNotificationService _localService;
        private IKeyShareNotificationService _remoteService;

        public DeviceKeyShareNotificationService(IKeyShareNotificationService localService, IKeyShareNotificationService remoteService)
        {
            _localService = localService;
            _remoteService = remoteService;
        }

        public IKeyShareNotificationService Refresh()
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

        public async Task<bool> SendKeyShareNotificationAsync(KeyShareNotificationApiModel keyShareApiModel)
        {
            if (New<AxCryptOnlineState>().IsOnline && Identity != LogOnIdentity.Empty)
            {
                try
                {
                    return await _remoteService.SendKeyShareNotificationAsync(keyShareApiModel).Free();
                }
                catch (ApiException aex)
                {
                    await aex.HandleApiExceptionAsync();
                }
            }

            return await _localService.SendKeyShareNotificationAsync(keyShareApiModel).Free();
        }
    }
}