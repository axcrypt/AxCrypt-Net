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
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AxCrypt.Mono.Test
{
    [TestFixture]
    public static class TestAuthTokenProvider
    {
        private static readonly Uri Api = new Uri("https://api.example.test/");

        private sealed class ManualTime : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow()
            {
                return Now;
            }
        }

        private sealed class StubApi : HttpMessageHandler
        {
            public readonly List<string> Calls = new List<string>();

            public readonly List<string> Bodies = new List<string>();

            public Func<string, string, HttpResponseMessage> Respond;

            public Action<HttpRequestMessage> OnRequest;

            public int Count(string path)
            {
                return Calls.FindAll(c => c == path).Count;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                OnRequest?.Invoke(request);
                lock (Calls)
                {
                    Calls.Add(request.RequestUri.AbsolutePath);
                    Bodies.Add(body);
                }

                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                return Respond(request.RequestUri.AbsolutePath, body);
            }
        }

        private static HttpResponseMessage Tokens(string access, string refresh, DateTime accessExpires, DateTime refreshExpires)
        {
            string json = JsonSerializer.Serialize(new { TokenType = "Bearer", AccessToken = access, AccessTokenExpiresUtc = accessExpires, RefreshToken = refresh, RefreshTokenExpiresUtc = refreshExpires });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }

        private static HttpResponseMessage Status(HttpStatusCode code)
        {
            return new HttpResponseMessage(code) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }

        private static (AuthTokenProvider provider, StubApi api, ManualTime time) Create(Func<string, string, HttpResponseMessage> respond)
        {
            ManualTime time = new ManualTime();
            StubApi api = new StubApi { Respond = respond };
            return (new AuthTokenProvider(api, time), api, time);
        }

        [Test]
        public static async Task TestLoginIsCachedUntilTheTokenNearsExpiry()
        {
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create((path, body) => Tokens("a1", "r1", time0().AddMinutes(15), time0().AddDays(30)));

            string first = await provider.GetAccessTokenAsync(Api, "a@b.com", "pw");
            string second = await provider.GetAccessTokenAsync(Api, "A@B.com", "pw");

            Assert.That(first, Is.EqualTo("a1"));
            Assert.That(second, Is.EqualTo("a1"));
            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(1));
            Assert.That(api.Bodies[0], Does.Contain("\"Email\":\"a@b.com\"").And.Contain("\"Password\":\"pw\""));
        }

        private static DateTime time0()
        {
            return new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        }

        [Test]
        public static async Task TestExpiredAccessTokenIsRefreshedNotSignedInAgain()
        {
            int issued = 0;
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create(null);
            api.Respond = (path, body) => path.EndsWith("/refresh") ? Tokens("a2", "r2", time.Now.UtcDateTime.AddMinutes(15), time.Now.UtcDateTime.AddDays(30)) : Tokens("a" + ++issued, "r1", time.Now.UtcDateTime.AddMinutes(15), time.Now.UtcDateTime.AddDays(30));

            Assert.That(await provider.GetAccessTokenAsync(Api, "a@b.com", "pw"), Is.EqualTo("a1"));
            time.Now = time.Now.AddMinutes(16);
            Assert.That(await provider.GetAccessTokenAsync(Api, "a@b.com", "pw"), Is.EqualTo("a2"));

            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(1));
            Assert.That(api.Count("/api/auth/refresh"), Is.EqualTo(1));
            Assert.That(api.Bodies[^1], Does.Contain("\"RefreshToken\":\"r1\""));
        }

        [Test]
        public static async Task TestRejectedRefreshFallsBackToSigningInWithThePassword()
        {
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create(null);
            api.Respond = (path, body) => path.EndsWith("/refresh") ? Status(HttpStatusCode.Unauthorized) : Tokens("a" + api.Count("/api/auth/login"), "r", time.Now.UtcDateTime.AddMinutes(15), time.Now.UtcDateTime.AddDays(30));

            await provider.GetAccessTokenAsync(Api, "a@b.com", "pw");
            provider.Invalidate(Api, "a@b.com");
            string token = await provider.GetAccessTokenAsync(Api, "a@b.com", "pw");

            Assert.That(token, Is.EqualTo("a2"));
            Assert.That(api.Count("/api/auth/refresh"), Is.EqualTo(1));
            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(2));
        }

        [Test]
        public static async Task TestWrongPasswordIsNotRetriedWhileBackedOff()
        {
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create((path, body) => Status(HttpStatusCode.Unauthorized));

            Assert.That(await provider.GetAccessTokenAsync(Api, "a@b.com", "wrong"), Is.Empty);
            Assert.That(await provider.GetAccessTokenAsync(Api, "a@b.com", "wrong"), Is.Empty);
            Assert.That(await provider.GetAccessTokenAsync(Api, "a@b.com", "wrong"), Is.Empty);
            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(1), "every failed sign-in counts towards the account lockout");

            await provider.GetAccessTokenAsync(Api, "a@b.com", "different");
            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(2), "a changed password is tried immediately");

            time.Now = time.Now.AddMinutes(2);
            await provider.GetAccessTokenAsync(Api, "a@b.com", "wrong");
            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(3), "after the back-off the password is tried again");
        }

        [Test]
        public static async Task TestServerErrorBacksOffBriefly()
        {
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create((path, body) => Status(HttpStatusCode.InternalServerError));

            Assert.That(await provider.GetAccessTokenAsync(Api, "a@b.com", "pw"), Is.Empty);
            Assert.That(await provider.GetAccessTokenAsync(Api, "a@b.com", "pw"), Is.Empty);
            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(1));

            time.Now = time.Now.AddSeconds(20);
            await provider.GetAccessTokenAsync(Api, "a@b.com", "pw");
            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(2));
        }

        [Test]
        public static async Task TestConcurrentCallersShareOneSignIn()
        {
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create(null);
            api.Respond = (path, body) => Tokens("a1", "r1", time.Now.UtcDateTime.AddMinutes(15), time.Now.UtcDateTime.AddDays(30));

            string[] tokens = await Task.WhenAll(Task.Run(() => provider.GetAccessTokenAsync(Api, "a@b.com", "pw")), Task.Run(() => provider.GetAccessTokenAsync(Api, "a@b.com", "pw")), Task.Run(() => provider.GetAccessTokenAsync(Api, "a@b.com", "pw")), Task.Run(() => provider.GetAccessTokenAsync(Api, "a@b.com", "pw")));

            Assert.That(tokens, Is.All.EqualTo("a1"));
            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(1));
        }

        [Test]
        public static async Task TestSignOutRevokesTheSessionOnTheServerAndForgetsTokens()
        {
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create(null);
            api.Respond = (path, body) => path.EndsWith("/logout") ? Status(HttpStatusCode.NoContent) : Tokens("a1", "r1", time.Now.UtcDateTime.AddMinutes(15), time.Now.UtcDateTime.AddDays(30));

            await provider.GetAccessTokenAsync(Api, "a@b.com", "pw");
            await provider.SignOutAsync("a@b.com");

            Assert.That(api.Count("/api/auth/logout"), Is.EqualTo(1));
            Assert.That(api.Bodies[^1], Does.Contain("\"RefreshToken\":\"r1\""));

            await provider.GetAccessTokenAsync(Api, "a@b.com", "pw");
            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(2), "after sign-out a new sign-in is needed");
        }

        [Test]
        public static async Task TestSignOutNeverFailsWhenTheServerIsUnreachable()
        {
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create(null);
            api.Respond = (path, body) => path.EndsWith("/logout") ? throw new HttpRequestException("offline") : Tokens("a1", "r1", time.Now.UtcDateTime.AddMinutes(15), time.Now.UtcDateTime.AddDays(30));

            await provider.GetAccessTokenAsync(Api, "a@b.com", "pw");

            Assert.DoesNotThrowAsync(() => provider.SignOutAsync("a@b.com"));
        }


        private static HttpResponseMessage Problem(HttpStatusCode code, string errorCode, TimeSpan? retryAfter = null)
        {
            HttpResponseMessage response = new HttpResponseMessage(code) { Content = new StringContent("{\"code\":\"" + errorCode + "\"}", Encoding.UTF8, "application/problem+json") };
            if (retryAfter.HasValue)
            {
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter.Value);
            }
            return response;
        }

        [Test]
        public static async Task TestSignInReportsWhyItFailed()
        {
            Func<string, string, HttpResponseMessage> respond = null;
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create((path, body) => respond(path, body));

            respond = (p, b) => Problem(HttpStatusCode.Unauthorized, "invalid_credentials");
            Assert.That((await provider.SignInAsync(Api, "a@b.com", "wrong")).Outcome, Is.EqualTo(SignInOutcome.InvalidCredentials));

            respond = (p, b) => Problem(HttpStatusCode.Unauthorized, "account_disabled");
            Assert.That((await provider.SignInAsync(Api, "c@d.com", "pw")).Outcome, Is.EqualTo(SignInOutcome.AccountDisabled));

            respond = (p, b) => Problem((HttpStatusCode)429, "rate_limited", TimeSpan.FromSeconds(45));
            SignInResult limited = await provider.SignInAsync(Api, "e@f.com", "pw");
            Assert.That(limited.Outcome, Is.EqualTo(SignInOutcome.RateLimited));
            Assert.That(limited.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(45)));

            respond = (p, b) => Status(HttpStatusCode.InternalServerError);
            Assert.That((await provider.SignInAsync(Api, "g@h.com", "pw")).Outcome, Is.EqualTo(SignInOutcome.Unavailable));
        }

        [Test]
        public static async Task TestSignInAlwaysVerifiesThePasswordEvenWithACachedSession()
        {
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create(null);
            api.Respond = (path, body) => body.Contains("\"Password\":\"right\"") ? Tokens("a1", "r1", time.Now.UtcDateTime.AddMinutes(15), time.Now.UtcDateTime.AddDays(30)) : Problem(HttpStatusCode.Unauthorized, "invalid_credentials");

            Assert.That((await provider.SignInAsync(Api, "a@b.com", "right")).Succeeded, Is.True);
            Assert.That((await provider.SignInAsync(Api, "a@b.com", "wrong")).Outcome, Is.EqualTo(SignInOutcome.InvalidCredentials), "a cached session must not vouch for a different password");
            Assert.That((await provider.SignInAsync(Api, "a@b.com", "right")).Succeeded, Is.True);
            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(3));
        }

        [Test]
        public static async Task TestSignInDoesNotRepeatARejectedPasswordWithinTheBackOff()
        {
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create((path, body) => Problem(HttpStatusCode.Unauthorized, "invalid_credentials"));

            await provider.SignInAsync(Api, "a@b.com", "wrong");
            SignInResult again = await provider.SignInAsync(Api, "a@b.com", "wrong");

            Assert.That(again.Outcome, Is.EqualTo(SignInOutcome.InvalidCredentials));
            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(1));
        }

        [Test]
        public static async Task TestSignInGivesTheSessionToLaterCalls()
        {
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create((path, body) => Tokens("a1", "r1", time0().AddMinutes(15), time0().AddDays(30)));

            await provider.SignInAsync(Api, "a@b.com", "pw");
            string token = await provider.GetAccessTokenAsync(Api, "a@b.com", "pw");

            Assert.That(token, Is.EqualTo("a1"));
            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(1));
        }

        [Test]
        public static async Task TestSignOutEverywhereEndsAllSessionsWithTheBearerToken()
        {
            string authorization = null;
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create(null);
            api.Respond = (path, body) => path.EndsWith("/logout-all") ? Status(HttpStatusCode.NoContent) : Tokens("a1", "r1", time.Now.UtcDateTime.AddMinutes(15), time.Now.UtcDateTime.AddDays(30));
            api.OnRequest = request => { if (request.RequestUri.AbsolutePath.EndsWith("/logout-all")) { authorization = request.Headers.Authorization?.ToString(); } };

            await provider.SignInAsync(Api, "a@b.com", "pw");
            await provider.SignOutEverywhereAsync("a@b.com");

            Assert.That(api.Count("/api/auth/logout-all"), Is.EqualTo(1));
            Assert.That(api.Count("/api/auth/logout"), Is.EqualTo(0));
            Assert.That(authorization, Is.EqualTo("Bearer a1"));

            await provider.GetAccessTokenAsync(Api, "a@b.com", "pw");
            Assert.That(api.Count("/api/auth/login"), Is.EqualTo(2), "tokens are forgotten afterwards");
        }

        [Test]
        public static async Task TestSignOutEverywhereFallsBackToEndingThisSessionWhenTheServerRefusesIt()
        {
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create(null);
            api.Respond = (path, body) => path.EndsWith("/logout-all") ? Status(HttpStatusCode.Unauthorized) : path.EndsWith("/logout") ? Status(HttpStatusCode.NoContent) : Tokens("a1", "r1", time.Now.UtcDateTime.AddMinutes(15), time.Now.UtcDateTime.AddDays(30));

            await provider.SignInAsync(Api, "a@b.com", "pw");
            await provider.SignOutEverywhereAsync("a@b.com");

            Assert.That(api.Count("/api/auth/logout"), Is.EqualTo(1));
        }

        [Test]
        public static async Task TestNoUserOrPasswordMeansNoToken()
        {
            (AuthTokenProvider provider, StubApi api, ManualTime time) = Create((path, body) => Status(HttpStatusCode.OK));

            Assert.That(await provider.GetAccessTokenAsync(Api, string.Empty, "pw"), Is.Empty);
            Assert.That(await provider.GetAccessTokenAsync(Api, "a@b.com", string.Empty), Is.Empty);
            Assert.That(api.Calls, Is.Empty);
        }
    }
}
