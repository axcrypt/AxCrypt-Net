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

using Newtonsoft.Json;
using System.Collections.Generic;

namespace AxCrypt.Api.Model.KeyShare
{
    /// <summary>
    /// A request to notify people by email that a file's key has been shared with them.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class KeyShareApiModel
    {
        [JsonProperty("fileId")]
        public long FileId { get; set; }

        /// <summary>
        /// Ignored by the API, which always uses the signed-in user.
        /// </summary>
        [JsonProperty("owner")]
        public string Owner { get; set; }

        [JsonProperty("sharedFileName")]
        public string SharedFileName { get; set; }

        /// <summary>
        /// Empty when a local file is shared; the HTTPS download URL when a cloud file is shared.
        /// </summary>
        [JsonProperty("sharedLink")]
        public string SharedLink { get; set; }

        /// <summary>
        /// At most 50 valid email addresses.
        /// </summary>
        [JsonProperty("recipients")]
        public IEnumerable<string> Recipients { get; set; }

        [JsonProperty("personalMessage")]
        public string PersonalMessage { get; set; }

        [JsonProperty("sharePermission")]
        public string SharePermission { get; set; }
    }

    public enum SharePermission
    {
        ViewOnly,
        Edit,
    }
}
