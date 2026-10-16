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
    /// Obtains and manages the JWT session for an API identity. Tokens live in memory only; when the app restarts the
    /// identity's password is used to sign in again, so no credential or token is written to disk.
    /// </summary>
    public interface ITokenProvider
    {
        /// <summary>
        /// A valid access token for the identity, signing in or refreshing as needed. Returns an empty string when none
        /// can be obtained (wrong credentials, offline); the caller then sends the request unauthenticated and the API's
        /// own 401 drives the normal error handling.
        /// </summary>
        Task<string> GetAccessTokenAsync(Uri apiAuthority, string user, string password);

        /// <summary>
        /// Verifies the credentials with the API right now, ignoring any cached session, and starts a new session on
        /// success. Use for an explicit sign-in (a log-on dialog) where the outcome must be reported to the user.
        /// </summary>
        Task<SignInResult> SignInAsync(Uri apiAuthority, string user, string password);

        /// <summary>
        /// Drops the access token the API just rejected, keeping the refresh token for the next call.
        /// </summary>
        void Invalidate(Uri apiAuthority, string user);

        /// <summary>
        /// Ends the user's API sessions: revokes them on the server (best effort, bounded wait) and forgets the tokens.
        /// </summary>
        Task SignOutAsync(string user);

        /// <summary>
        /// Ends every API session the user has on any device, then forgets the local tokens. Falls back to an ordinary
        /// sign-out if the user cannot be authenticated.
        /// </summary>
        Task SignOutEverywhereAsync(string user);

        /// <summary>
        /// Forgets the user's tokens locally without contacting the server.
        /// </summary>
        void Clear(string user);

        void ClearAll();
    }
}
