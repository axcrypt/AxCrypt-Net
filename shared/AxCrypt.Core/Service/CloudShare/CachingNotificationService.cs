using AxCrypt.Api.Model.CloudShare;
using AxCrypt.Common;
using AxCrypt.Core.Crypto;

namespace AxCrypt.Core.Service.CloudShare
{
    public class CachingCloudShareService : IKeyShareNotificationService
    {
        private IKeyShareNotificationService _service;

        public CachingCloudShareService(IKeyShareNotificationService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            _service = service;
        }

        public LogOnIdentity Identity => throw new NotImplementedException();

        public async Task<bool> SendKeyShareNotificationAsync(KeyShareNotificationApiModel keyShareApiModel)
        {
            return await _service.SendKeyShareNotificationAsync(keyShareApiModel).Free();
        }

        public IKeyShareNotificationService Refresh()
        {
            return this;
        }
    }
}