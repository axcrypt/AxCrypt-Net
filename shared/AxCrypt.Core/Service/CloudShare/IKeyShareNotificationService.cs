using AxCrypt.Api.Model.CloudShare;
using AxCrypt.Core.Crypto;

namespace AxCrypt.Core.Service.CloudShare
{
    public interface IKeyShareNotificationService
    {
        IKeyShareNotificationService Refresh();

        LogOnIdentity Identity { get; }

        Task<bool> SendCloudKeyShareNotificationAsync(KeyShareNotificationApiModel keyShareApiModel);

        Task<bool> SendLocalKeyShareNotificationAsync(KeyShareNotificationApiModel keyShareApiModel);
    }
}