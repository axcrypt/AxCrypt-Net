using AxCrypt.Api;
using AxCrypt.Api.Model.SecuredMessenger;
using AxCrypt.Core.UI;

namespace AxCrypt.Core.StoreApi
{
    public interface ISecMessengerStoreApiService
    {
        Task<IEnumerable<SecuredMessengerApiModel>> GetListAsync(RequestOptions requestOptions);

        Task<IEnumerable<SecuredMessengerApiModel>> GetSentListAsync(RequestOptions requestOptions);

        Task<IEnumerable<SecuredMessengerApiModel>> GetUnreadListAsync(RequestOptions requestOptions);

        Task<bool> CreateAsync(SecuredMessengerApiModel model);

        Task<bool> UpdateAsync(IEnumerable<Guid> ids, string userEmail, bool isUnread = false);

        Task<bool> DeleteAsync(IEnumerable<Guid> ids, string user, SecuredMessengerFilterTab securedMessengerFilter);

        Task<IEnumerable<SecuredMessengerApiModel>> GetSecMsgWithSearchFiltersAsync(SecuredMessengerFilterTab securedMessengerFilterTab, RequestOptions requestOptions);

        Task<long> GetFreeUserSecMsgCountAsync(string userEmail);

        Task<bool> UpdateFreeUserSecMsgCountAsync(string userEmail);

        Task<IEnumerable<SecuredMessengerApiModel>> GetSentMessagesWithRepliesAsync(Guid id, string userEmail);

        Task<IEnumerable<SecuredMessengerApiModel>> GetInboxMessagesWithRepliesAsync(Guid id, string userEmail);

        Task<SecuredMessengerApiModel> GetMessageAsync(Guid id, string userEmail);
    }
}