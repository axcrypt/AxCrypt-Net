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
    public enum SignInOutcome
    {
        Success,

        /// <summary>
        /// Unknown account or wrong password (the API does not say which).
        /// </summary>
        InvalidCredentials,

        AccountDisabled,

        /// <summary>
        /// Too many attempts; see <see cref="SignInResult.RetryAfter"/>.
        /// </summary>
        RateLimited,

        /// <summary>
        /// The API could not be reached or failed; try again later.
        /// </summary>
        Unavailable,
    }

    /// <summary>
    /// The outcome of an explicit sign-in, so the UI can tell the user what actually happened.
    /// </summary>
    public sealed class SignInResult
    {
        public SignInResult(SignInOutcome outcome, TimeSpan? retryAfter = null)
        {
            Outcome = outcome;
            RetryAfter = retryAfter;
        }

        public SignInOutcome Outcome { get; }

        public TimeSpan? RetryAfter { get; }

        public bool Succeeded
        {
            get { return Outcome == SignInOutcome.Success; }
        }
    }
}
