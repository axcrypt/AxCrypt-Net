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

using AxCrypt.Abstractions.Rest;
using AxCrypt.Common;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace AxCrypt.Api
{
    /// <summary>
    /// Explicit sign-in and sign-out against the AxCrypt API. Ordinary calls sign in by themselves; use this where the
    /// outcome has to be shown to the user, for example a log-on dialog, or to end the user's sessions.
    /// </summary>
    public class AxAuthApiClient
    {
        private Uri BaseUrl { get; }

        public AxAuthApiClient(RestIdentity identity, Uri baseUrl, TimeSpan timeout)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            Timeout = timeout;
        }

        public RestIdentity Identity { get; }

        public TimeSpan Timeout { get; }

        private static ITokenProvider Tokens
        {
            get { return TokenProviders.Current; }
        }

        /// <summary>
        /// Checks the identity's credentials with the API and starts a new session. Expected failures (wrong password,
        /// disabled account, rate limit, API unavailable) are returned, not thrown; see <see cref="EnsureSignedIn"/>.
        /// </summary>
        public Task<SignInResult> LoginAsync()
        {
            return Tokens.SignInAsync(BaseUrl, Identity.User, Identity.Password);
        }

        /// <summary>
        /// Ends this device's session for the identity. Never throws because the API is unreachable.
        /// </summary>
        public Task LogoutAsync()
        {
            return Tokens.SignOutAsync(Identity.User);
        }

        /// <summary>
        /// Ends every session the identity has on any device ("sign out everywhere").
        /// </summary>
        public Task LogoutEverywhereAsync()
        {
            return Tokens.SignOutEverywhereAsync(Identity.User);
        }

        /// <summary>
        /// Throws the matching API exception unless the sign-in succeeded.
        /// </summary>
        public static void EnsureSignedIn(SignInResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            switch (result.Outcome)
            {
                case SignInOutcome.Success:
                    return;

                case SignInOutcome.InvalidCredentials:
                    throw new UnauthorizedException("invalid_credentials");

                case SignInOutcome.AccountDisabled:
                    throw new UnauthorizedException("account_disabled");

                case SignInOutcome.RateLimited:
                    string retry = result.RetryAfter.HasValue ? string.Format(CultureInfo.InvariantCulture, " Retry after {0:0} seconds.", result.RetryAfter.Value.TotalSeconds) : string.Empty;
                    throw new ApiException("Too many requests." + retry, (System.Net.HttpStatusCode)429);

                default:
                    throw new OfflineApiException("The AxCrypt service is not available.");
            }
        }
    }
}
