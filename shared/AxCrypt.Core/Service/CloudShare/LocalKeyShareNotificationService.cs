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

        public Task<bool> SendKeyShareNotificationAsync(KeyShareNotificationApiModel cloudShareLinkApiModel)
        {
            return Task.FromResult(true);
        }
    }
}