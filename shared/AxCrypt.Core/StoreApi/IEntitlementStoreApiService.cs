using AxCrypt.Api.Model.Entitlement;
using System;
using System.Collections.Generic;
using System.Text;

namespace AxCrypt.Core.StoreApi
{
    public interface IEntitlementStoreApiService
    {
        Task<EntitlementApiModel> GetUserCountDetailsAsync(EntitlementUsageRequestModel featureUsageModel);

        Task<bool> InsertUserUsageCountAsync(EntitlementUsageRequestModel featureUsageModel);
    }
}
