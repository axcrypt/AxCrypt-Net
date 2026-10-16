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
    /// The JWT access token and rotating refresh token issued by the AxCrypt API, with their UTC expiry times.
    /// </summary>
    public sealed class AuthTokens
    {
        public static readonly AuthTokens Empty = new AuthTokens(string.Empty, DateTime.MinValue, string.Empty, DateTime.MinValue);

        public AuthTokens(string accessToken, DateTime accessTokenExpiresUtc, string refreshToken, DateTime refreshTokenExpiresUtc)
        {
            AccessToken = accessToken ?? string.Empty;
            AccessTokenExpiresUtc = accessTokenExpiresUtc;
            RefreshToken = refreshToken ?? string.Empty;
            RefreshTokenExpiresUtc = refreshTokenExpiresUtc;
        }

        public string AccessToken { get; }

        public DateTime AccessTokenExpiresUtc { get; }

        public string RefreshToken { get; }

        public DateTime RefreshTokenExpiresUtc { get; }

        public bool IsEmpty
        {
            get { return AccessToken.Length == 0 && RefreshToken.Length == 0; }
        }

        /// <summary>
        /// True when there is no usable access token, counting a token as expired <paramref name="safetyMargin"/> early.
        /// </summary>
        public bool IsAccessTokenExpired(DateTime nowUtc, TimeSpan safetyMargin)
        {
            return AccessToken.Length == 0 || nowUtc + safetyMargin >= AccessTokenExpiresUtc;
        }

        public bool IsRefreshTokenExpired(DateTime nowUtc)
        {
            return RefreshToken.Length == 0 || nowUtc >= RefreshTokenExpiresUtc;
        }

        /// <summary>
        /// The same session without its access token, so the next call refreshes instead of reusing a rejected token.
        /// </summary>
        public AuthTokens WithoutAccessToken()
        {
            return new AuthTokens(string.Empty, DateTime.MinValue, RefreshToken, RefreshTokenExpiresUtc);
        }
    }
}
