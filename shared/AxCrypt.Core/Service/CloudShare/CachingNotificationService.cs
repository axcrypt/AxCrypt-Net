using AxCrypt.Api.Model.CloudShare;
using AxCrypt.Common;
using AxCrypt.Core.Crypto;

namespace AxCrypt.Core.Service.CloudShare
{
    public class CachingKeyShareNotificationService : IKeyShareNotificationService
    {
        private IKeyShareNotificationService _service;

        public CachingKeyShareNotificationService(IKeyShareNotificationService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            _service = service;
        }

        public LogOnIdentity Identity => throw new NotImplementedException();

        public async Task<bool> SendCloudKeyShareNotificationAsync(KeyShareNotificationApiModel keyShareApiModel)
        {
            return await _service.SendCloudKeyShareNotificationAsync(keyShareApiModel).Free();
        }

        public async Task<bool> SendLocalKeyShareNotificationAsync(KeyShareNotificationApiModel keyShareApiModel)
        {
            return await _service.SendLocalKeyShareNotificationAsync(keyShareApiModel).Free();
        }

        public IKeyShareNotificationService Refresh()
        {
            return this;
        }
    }
}