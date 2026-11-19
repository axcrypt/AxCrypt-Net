using AxCrypt.Api.Model.Reseller;
using System;
using System.Collections.Generic;
using System.Text;

namespace AxCrypt.Core.StoreApi
{
    public interface IResellerMembersStoreApiService
    {
        Task<bool> CreateAsync(ResellerMemberApiModel resellerApiModel);

        Task<bool> DeleteAsync(Guid resellerId, string userEmail);

        Task<bool> UpdateAsync(ResellerMemberApiModel roleAssignment);

        Task<ResellerMemberApiModel> GetMembersAsync(string userEmail);

        Task<bool> CanManageResellerMembersAsync(Guid resellerId, string userEmail);

        Task<bool> CanManageResellerInvoicesAsync(Guid resellerId, string userEmail);

        Task<int> GetResellerMembersCountAsync(Guid resellerId);
    }
}
