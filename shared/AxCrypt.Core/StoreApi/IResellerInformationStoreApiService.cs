using AxCrypt.Api.Model.Reseller;
using AxCrypt.Api.Model.Reseller.Status;
using System;
using System.Collections.Generic;
using System.Text;

namespace AxCrypt.Core.StoreApi
{
    public interface IResellerInformationStoreApiService
    {
        Task<bool> RegisterAsync(ResellerInformationApiModel resellerApiModel);

        Task<IEnumerable<ResellerInformationApiModel>> GetListAsync(ResellerStatus status);

        Task<ResellerInformationApiModel> GetByIdAsync(Guid resellerId);

        Task<ResellerDashboardApiModel> GetDashboardCountsync(string userEmail);

        Task<bool> UpdateAsync(ResellerInformationApiModel resellerApiModel);

        Task<ResellerInformationApiModel> GetResellerInfoWithMembersAsync(Guid resellerId);
    }
}
