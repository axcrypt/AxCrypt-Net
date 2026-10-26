using AxCrypt.Api.Model.CloudShare;
using AxCrypt.Core.Crypto;

namespace AxCrypt.Core.Service.CloudShare
{
    public class LocalKeyShareNotificationService : IKeyShareNotificationService
    {
        public LocalKeyShareNotificationService()
        {
        }

        public IKeyShareNotificationService Refresh()
        {
            return this;
        }

        public LogOnIdentity Identity
        {
            get;
        }

        public Task<bool> SendCloudKeyShareNotificationAsync(KeyShareNotificationApiModel cloudShareLinkApiModel)
        {
            return Task.FromResult(true);
        }

        public Task<bool> SendLocalKeyShareNotificationAsync(KeyShareNotificationApiModel keyShareApiModel)
        {
            return Task.FromResult(true);
        }
    }
}