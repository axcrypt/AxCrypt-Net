using AxCrypt.Api.Model.CloudShare;
using AxCrypt.Core.Crypto;

namespace AxCrypt.Core.Service.CloudShare
{
    public class NullCloudShareService : ICloudShareService
    {
        private static readonly Task<Guid> _completedTask = Task.FromResult(Guid.Empty);

        public NullCloudShareService(LogOnIdentity identity)
        {
            Identity = identity;
        }

        public ICloudShareService Refresh()
        {
            return this;
        }

        public LogOnIdentity Identity
        {
            get; private set;
        }

        public Task<Guid> ShareLinkAsync(CloudShareLinkApiModel cloudShareLinkApiModel)
        {
            return _completedTask;
        }
    }
}