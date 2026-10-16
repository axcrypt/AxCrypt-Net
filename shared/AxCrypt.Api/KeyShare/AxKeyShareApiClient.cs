#region Coypright and License

/*
 * AxCrypt - Copyright 2026, AxCrypt AB, All Rights Reserved
 *
 * This file is part of AxCrypt.
 *
 * AxCrypt is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * AxCrypt is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with AxCrypt.  If not, see <http://www.gnu.org/licenses/>.
 *
 * The source is maintained at https://github.com/axcrypt/AxCrypt-Net please visit for
 * updates, contributions and contact with the author. You may also visit
 * http://www.axcrypt.net for more information about the author.
*/

#endregion Coypright and License

using AxCrypt.Abstractions;
using AxCrypt.Abstractions.Rest;
using AxCrypt.Api.Model.KeyShare;
using AxCrypt.Common;
using System;
using System.Linq;
using System.Threading.Tasks;
using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Api
{
    /// <summary>
    /// Asks the API to email people that a file's key has been shared with them.
    /// </summary>
    public class AxKeyShareApiClient
    {
        private Uri BaseUrl { get; }

        private TimeSpan Timeout { get; }

        private ApiCaller Caller { get; } = new ApiCaller();

        public AxKeyShareApiClient(RestIdentity identity, Uri baseUrl, TimeSpan timeout)
        {
            Identity = identity;
            BaseUrl = baseUrl;
            Timeout = timeout;
        }

        public RestIdentity Identity { get; }

        /// <summary>
        /// Notify recipients that the key to a local file was shared with them.
        /// </summary>
        public Task<bool> NotifyLocalFileAsync(KeyShareApiModel model)
        {
            return NotifyAsync("keyshare/notify/local", model);
        }

        /// <summary>
        /// Notify recipients that the key to a cloud file was shared with them. The link must be an HTTPS URL.
        /// </summary>
        public Task<bool> NotifyCloudFileAsync(KeyShareApiModel model)
        {
            if (model != null && !(Uri.TryCreate(model.SharedLink, UriKind.Absolute, out Uri link) && link.Scheme == Uri.UriSchemeHttps))
            {
                throw new ArgumentException("A cloud file needs an HTTPS link.", nameof(model));
            }

            return NotifyAsync("keyshare/notify/cloud", model);
        }

        private async Task<bool> NotifyAsync(string path, KeyShareApiModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }
            if (model.Recipients == null || !model.Recipients.Any())
            {
                return true;
            }

            Uri resource = BaseUrl.PathCombine(path);
            RestContent content = new RestContent(Serializer.Serialize(model));
            RestResponse restResponse = await Caller.RestAsync(Identity, new RestRequest("POST", resource, Timeout, content)).Free();
            ApiCaller.EnsureStatusOk(restResponse);

            return Serializer.Deserialize<bool>(restResponse.Content);
        }

        private static IStringSerializer Serializer
        {
            get
            {
                return New<IStringSerializer>();
            }
        }
    }
}
