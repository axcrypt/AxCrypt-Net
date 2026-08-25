using AxCrypt.Api.Model.CloudShare;
using AxCrypt.Core.Crypto;

namespace AxCrypt.Core.Service.CloudShare
{
    public interface IKeyShareNotificationService
    {
        IKeyShareNotificationService Refresh();

        LogOnIdentity Identity { get; }

        Task<bool> SendKeyShareNotificationAsync(KeyShareNotificationApiModel keyShareApiModel);
    }
}