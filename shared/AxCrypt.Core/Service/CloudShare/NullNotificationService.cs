using AxCrypt.Api.Model.CloudShare;
using AxCrypt.Core.Crypto;

namespace AxCrypt.Core.Service.CloudShare
{
    public class NullCloudShareService : IKeyShareNotificationService
    {
        private static readonly Task<bool> _completedTask = Task.FromResult(true);

        public NullCloudShareService(LogOnIdentity identity)
        {
            Identity = identity;
        }

        public IKeyShareNotificationService Refresh()
        {
            return this;
        }

        public LogOnIdentity Identity
        {
            get; private set;
        }

        public Task<bool> SendKeyShareNotificationAsync(KeyShareNotificationApiModel cloudShareLinkApiModel)
        {
            return _completedTask;
        }
    }
}