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

namespace AxCrypt.Abstractions.Rest
{
    /// <summary>
    /// How a request proves who is calling.
    /// </summary>
    public enum RestAuthMode
    {
        /// <summary>
        /// A JWT Bearer token for the request's identity (signing in on demand). The default.
        /// </summary>
        Bearer,

        /// <summary>
        /// Anonymous. Use for public endpoints so no sign-in attempt is made, which also keeps failed attempts off the account.
        /// </summary>
        None,

        /// <summary>
        /// HTTP Basic with the identity's user and password. Only for the partner API-key endpoints, where the
        /// "password" is an API key.
        /// </summary>
        Basic,
    }
}
