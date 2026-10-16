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
    /// Finds the registered <see cref="ITokenProvider"/>, or a do-nothing one for hosts that do not register one
    /// (unit tests, tools), so callers never have to care.
    /// </summary>
    public static class TokenProviders
    {
        public static ITokenProvider Current
        {
            get
            {
                try
                {
                    return TypeResolve.New<ITokenProvider>();
                }
                catch (ArgumentException)
                {
                    return NullTokenProvider.Instance;
                }
            }
        }

        private sealed class NullTokenProvider : ITokenProvider
        {
            public static readonly NullTokenProvider Instance = new NullTokenProvider();

            public Task<string> GetAccessTokenAsync(Uri apiAuthority, string user, string password)
            {
                return Task.FromResult(string.Empty);
            }

            public Task<SignInResult> SignInAsync(Uri apiAuthority, string user, string password)
            {
                return Task.FromResult(new SignInResult(SignInOutcome.Unavailable));
            }

            public Task SignOutEverywhereAsync(string user)
            {
                return Task.CompletedTask;
            }

            public void Invalidate(Uri apiAuthority, string user)
            {
            }

            public Task SignOutAsync(string user)
            {
                return Task.CompletedTask;
            }

            public void Clear(string user)
            {
            }

            public void ClearAll()
            {
            }
        }
    }
}
