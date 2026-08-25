using AxCrypt.Api.Model.CloudShare;
using AxCrypt.Core.Crypto;

namespace AxCrypt.Core.Service.CloudShare
{
    public interface ICloudShareService
    {
        ICloudShareService Refresh();

        LogOnIdentity Identity { get; }

        Task<Guid> ShareLinkAsync(CloudShareLinkApiModel cloudShareLinkApiModel);
    }
}