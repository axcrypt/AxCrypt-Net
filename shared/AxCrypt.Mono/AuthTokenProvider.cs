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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AxCrypt.Mono
{
    /// <summary>
    /// Default <see cref="ITokenProvider"/>. Signs in with POST /api/auth/login, renews with /api/auth/refresh (the
    /// server rotates the refresh token on every use) and ends sessions with /api/auth/logout and /api/auth/logout-all.
    /// Tokens are kept in memory only. It talks to the API directly rather than through <see cref="IRestCaller"/>, which
    /// depends on it.
    /// </summary>
    public sealed class AuthTokenProvider : ITokenProvider, IDisposable
    {
        private static readonly TimeSpan ExpirySafetyMargin = TimeSpan.FromSeconds(30);

        // After the server rejects a password, do not try that password again for a while: every attempt counts
        // towards the account lockout.
        private static readonly TimeSpan RejectedCredentialsBackoff = TimeSpan.FromSeconds(60);

        // After a rate limit or server error, leave the API alone briefly instead of piling on requests.
        private static readonly TimeSpan TransientFailureBackoff = TimeSpan.FromSeconds(15);

        private static readonly TimeSpan SignOutTimeout = TimeSpan.FromSeconds(3);

        // Property names go out exactly as written (PascalCase, the API contract) and come back case-insensitively.
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _client;

        private readonly TimeProvider _time;

        private readonly ConcurrentDictionary<string, Session> _sessions = new ConcurrentDictionary<string, Session>();

        public AuthTokenProvider()
            : this(null, TimeProvider.System)
        {
        }

        /// <summary>
        /// Tells whether the app is offline, so sign-out does not wait for a server that cannot be reached.
        /// </summary>
        public Func<bool> IsOffline { get; set; } = () => false;

        /// <param name="handler">The HTTP handler to use; null for a pooled default. Mainly for tests.</param>
        public AuthTokenProvider(HttpMessageHandler handler, TimeProvider time)
        {
            _client = new HttpClient(handler ?? CreateHandler(), disposeHandler: handler == null) { Timeout = TimeSpan.FromSeconds(30) };
            _time = time ?? TimeProvider.System;
        }

        public async Task<string> GetAccessTokenAsync(Uri apiAuthority, string user, string password)
        {
            if (apiAuthority == null)
            {
                throw new ArgumentNullException(nameof(apiAuthority));
            }
            if (string.IsNullOrEmpty(user))
            {
                return string.Empty;
            }

            Session session = SessionFor(apiAuthority, user);
            AuthTokens tokens = session.Tokens;
            if (!tokens.IsAccessTokenExpired(UtcNow, ExpirySafetyMargin))
            {
                return tokens.AccessToken;
            }

            await session.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                SignInResult result = await AcquireAsync(session, password, forceSignIn: false).ConfigureAwait(false);
                return result.Succeeded ? session.Tokens.AccessToken : string.Empty;
            }
            finally
            {
                session.Gate.Release();
            }
        }

        public async Task<SignInResult> SignInAsync(Uri apiAuthority, string user, string password)
        {
            if (apiAuthority == null)
            {
                throw new ArgumentNullException(nameof(apiAuthority));
            }
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
            {
                return new SignInResult(SignInOutcome.InvalidCredentials);
            }

            Session session = SessionFor(apiAuthority, user);
            await session.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                // Always ask the API: a cached session says nothing about whether this password is right.
                return await AcquireAsync(session, password, forceSignIn: true).ConfigureAwait(false);
            }
            finally
            {
                session.Gate.Release();
            }
        }

        public void Invalidate(Uri apiAuthority, string user)
        {
            if (apiAuthority == null || string.IsNullOrEmpty(user))
            {
                return;
            }

            if (_sessions.TryGetValue(KeyOf(apiAuthority, user), out Session session))
            {
                session.Tokens = session.Tokens.WithoutAccessToken();
            }
        }

        public async Task SignOutAsync(string user)
        {
            foreach (Session session in SessionsOf(user))
            {
                string refreshToken = session.Tokens.RefreshToken;
                session.Tokens = AuthTokens.Empty;
                if (refreshToken.Length > 0)
                {
                    await PostBestEffortAsync(session.Authority, "/api/auth/logout", new { RefreshToken = refreshToken }, null).ConfigureAwait(false);
                }
            }
        }

        public async Task SignOutEverywhereAsync(string user)
        {
            foreach (Session session in SessionsOf(user))
            {
                // Only a signed-in caller may end all sessions; use the current session without signing in again.
                string accessToken = await GetAccessTokenAsync(session.Authority, session.User, string.Empty).ConfigureAwait(false);
                string refreshToken = session.Tokens.RefreshToken;
                session.Tokens = AuthTokens.Empty;

                if (accessToken.Length > 0 && await PostBestEffortAsync(session.Authority, "/api/auth/logout-all", null, accessToken).ConfigureAwait(false))
                {
                    continue;
                }
                if (refreshToken.Length > 0)
                {
                    await PostBestEffortAsync(session.Authority, "/api/auth/logout", new { RefreshToken = refreshToken }, null).ConfigureAwait(false);
                }
            }
        }

        public void Clear(string user)
        {
            foreach (Session session in SessionsOf(user))
            {
                session.Tokens = AuthTokens.Empty;
                session.RejectedUntilUtc = DateTime.MinValue;
                session.RejectedFingerprint = null;
            }
        }

        public void ClearAll()
        {
            _sessions.Clear();
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        private DateTime UtcNow
        {
            get { return _time.GetUtcNow().UtcDateTime; }
        }

        private Session SessionFor(Uri apiAuthority, string user)
        {
            return _sessions.GetOrAdd(KeyOf(apiAuthority, user), key => new Session(AuthorityOf(apiAuthority), user));
        }

        /// <summary>
        /// Gets the session into a signed-in state: reuse, refresh or sign in. Must be called holding the session gate.
        /// </summary>
        private async Task<SignInResult> AcquireAsync(Session session, string password, bool forceSignIn)
        {
            AuthTokens tokens = session.Tokens;
            if (!forceSignIn && !tokens.IsAccessTokenExpired(UtcNow, ExpirySafetyMargin))
            {
                return new SignInResult(SignInOutcome.Success);
            }

            if (UtcNow < session.BlockedUntilUtc)
            {
                return new SignInResult(session.BlockedOutcome, session.BlockedUntilUtc - UtcNow);
            }

            if (!forceSignIn && !tokens.IsRefreshTokenExpired(UtcNow))
            {
                TokenResult refreshed = await PostForTokensAsync(session.Authority, "/api/auth/refresh", new { RefreshToken = tokens.RefreshToken }).ConfigureAwait(false);
                if (refreshed.Tokens != null)
                {
                    session.Tokens = refreshed.Tokens;
                    return new SignInResult(SignInOutcome.Success);
                }
                if (refreshed.Outcome != SignInOutcome.InvalidCredentials)
                {
                    return Block(session, refreshed);
                }

                // The session is gone (server restarted, revoked, expired): sign in afresh.
                session.Tokens = AuthTokens.Empty;
            }

            if (string.IsNullOrEmpty(password))
            {
                return new SignInResult(SignInOutcome.InvalidCredentials);
            }

            string fingerprint = Fingerprint(password);
            if (UtcNow < session.RejectedUntilUtc && session.RejectedFingerprint == fingerprint)
            {
                return new SignInResult(session.RejectedOutcome);
            }

            TokenResult issued = await PostForTokensAsync(session.Authority, "/api/auth/login", new { Email = session.User, Password = password }).ConfigureAwait(false);
            if (issued.Tokens != null)
            {
                session.Tokens = issued.Tokens;
                session.RejectedFingerprint = null;
                session.RejectedUntilUtc = DateTime.MinValue;
                return new SignInResult(SignInOutcome.Success);
            }

            if (issued.Outcome == SignInOutcome.InvalidCredentials || issued.Outcome == SignInOutcome.AccountDisabled)
            {
                session.RejectedFingerprint = fingerprint;
                session.RejectedOutcome = issued.Outcome;
                session.RejectedUntilUtc = UtcNow + RejectedCredentialsBackoff;
                return new SignInResult(issued.Outcome);
            }

            return Block(session, issued);
        }

        private SignInResult Block(Session session, TokenResult failure)
        {
            TimeSpan wait = failure.RetryAfter ?? TransientFailureBackoff;
            session.BlockedUntilUtc = UtcNow + wait;
            session.BlockedOutcome = failure.Outcome;
            return new SignInResult(failure.Outcome, wait);
        }

        private IEnumerable<Session> SessionsOf(string user)
        {
            foreach (Session session in _sessions.Values)
            {
                if (string.Equals(session.User, user, StringComparison.OrdinalIgnoreCase))
                {
                    yield return session;
                }
            }
        }

        private async Task<bool> PostBestEffortAsync(Uri authority, string path, object body, string bearerToken)
        {
            if (IsOffline())
            {
                return false;
            }

            try
            {
                using (CancellationTokenSource timeout = new CancellationTokenSource(SignOutTimeout))
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, new Uri(authority, path)))
                {
                    if (body != null)
                    {
                        request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
                    }
                    if (bearerToken != null)
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                    }

                    using (HttpResponseMessage response = await _client.SendAsync(request, timeout.Token).ConfigureAwait(false))
                    {
                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
            {
                // Signing out locally must never fail because the server could not be reached; the server expires
                // the session by itself.
                return false;
            }
        }

        private async Task<TokenResult> PostForTokensAsync(Uri authority, string path, object body)
        {
            try
            {
                using (StringContent content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json"))
                using (HttpResponseMessage response = await _client.PostAsync(new Uri(authority, path), content).ConfigureAwait(false))
                {
                    string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        AuthTokens tokens = Parse(text);
                        return tokens != null ? TokenResult.Success(tokens) : TokenResult.Failure(SignInOutcome.Unavailable, null);
                    }

                    if (response.StatusCode == (HttpStatusCode)429)
                    {
                        return TokenResult.Failure(SignInOutcome.RateLimited, response.Headers.RetryAfter?.Delta);
                    }
                    if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        return TokenResult.Failure(ErrorCode(text) == "account_disabled" ? SignInOutcome.AccountDisabled : SignInOutcome.InvalidCredentials, null);
                    }

                    return TokenResult.Failure(SignInOutcome.Unavailable, response.Headers.RetryAfter?.Delta);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException || ex is JsonException)
            {
                return TokenResult.Failure(SignInOutcome.Unavailable, null);
            }
        }

        private static string ErrorCode(string json)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    return document.RootElement.TryGetProperty("code", out JsonElement code) && code.ValueKind == JsonValueKind.String ? code.GetString() : string.Empty;
                }
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private static AuthTokens Parse(string json)
        {
            TokenResponseDto dto = JsonSerializer.Deserialize<TokenResponseDto>(json, Json)!;
            if (dto == null || string.IsNullOrEmpty(dto.AccessToken))
            {
                return null;
            }

            return new AuthTokens(dto.AccessToken, dto.AccessTokenExpiresUtc, dto.RefreshToken, dto.RefreshTokenExpiresUtc);
        }

        private static string Fingerprint(string password)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
            }
        }

        private static Uri AuthorityOf(Uri uri)
        {
            return new Uri(uri.GetLeftPart(UriPartial.Authority));
        }

        private static string KeyOf(Uri apiAuthority, string user)
        {
            return AuthorityOf(apiAuthority).AbsoluteUri.ToLowerInvariant() + "|" + user.ToLowerInvariant();
        }

        private static HttpMessageHandler CreateHandler()
        {
            SocketsHttpHandler handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
#if DEBUG
            handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true;
#endif
            return handler;
        }

        private sealed class Session
        {
            public Session(Uri authority, string user)
            {
                Authority = authority;
                User = user;
            }

            public Uri Authority { get; }

            public string User { get; }

            public SemaphoreSlim Gate { get; } = new SemaphoreSlim(1, 1);

            public volatile AuthTokens Tokens = AuthTokens.Empty;

            public DateTime BlockedUntilUtc;

            public SignInOutcome BlockedOutcome;

            public DateTime RejectedUntilUtc;

            public SignInOutcome RejectedOutcome;

            public string RejectedFingerprint;
        }

        private sealed class TokenResult
        {
            public static TokenResult Success(AuthTokens tokens)
            {
                return new TokenResult(tokens, SignInOutcome.Success, null);
            }

            public static TokenResult Failure(SignInOutcome outcome, TimeSpan? retryAfter)
            {
                return new TokenResult(null, outcome, retryAfter);
            }

            private TokenResult(AuthTokens tokens, SignInOutcome outcome, TimeSpan? retryAfter)
            {
                Tokens = tokens;
                Outcome = outcome;
                RetryAfter = retryAfter;
            }

            public AuthTokens Tokens { get; }

            public SignInOutcome Outcome { get; }

            public TimeSpan? RetryAfter { get; }
        }

        private sealed class TokenResponseDto
        {
            public string AccessToken { get; set; }

            public DateTime AccessTokenExpiresUtc { get; set; }

            public string RefreshToken { get; set; }

            public DateTime RefreshTokenExpiresUtc { get; set; }
        }
    }
}
