using AxCrypt.Api.Model.User;


namespace AxCrypt.Core.StoreApi
{
    public interface IUserInfoStoreApiService
    {
        Task<UserInfoApiModel> GetUserInfoAsync(string userEmail);

        Task<UserKeyValuePairApiModel> GetUserInfoByKeyAsync(string userEmail, string memPropertyName);

        Task<bool> UpdateUserInfoByKeyAsync(UserKeyValuePairApiModel userKeyValuePairApiModel);

        Task<AxCrypt.Abstractions.Rest.RestResponse> GetVerifiedUsersStatsAsync(string userEmail, int pageNumber);
    }
}
