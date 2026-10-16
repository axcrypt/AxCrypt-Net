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
using AxCrypt.Common;
using AxCrypt.Fake;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Mono.Test
{
    /// <summary>
    /// Runs the real RestCaller and AuthTokenProvider against a small in-process HTTP server and checks what goes on the wire.
    /// </summary>
    [TestFixture]
    public static class TestRestCaller
    {
        private sealed class Seen
        {
            public string Path;
            public string Authorization;
            public string Passphrase;
            public string Body;
        }

        private sealed class FakeApi : IDisposable
        {
            private readonly HttpListener _listener = new HttpListener();
            private readonly Task _loop;
            private int _logins;

            public readonly List<Seen> Requests = new List<Seen>();

            // Access tokens the fake API currently accepts.
            public readonly HashSet<string> ValidTokens = new HashSet<string>();

            public bool RejectLogin;

            public FakeApi()
            {
                int port = new Random().Next(20000, 40000);
                while (true)
                {
                    try
                    {
                        _listener.Prefixes.Clear();
                        _listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                        _listener.Start();
                        break;
                    }
                    catch (HttpListenerException)
                    {
                        port++;
                    }
                }

                BaseUrl = new Uri("http://127.0.0.1:" + port + "/api/");
                _loop = Task.Run(Loop);
            }

            public Uri BaseUrl { get; }

            public int Count(string path)
            {
                lock (Requests)
                {
                    return Requests.Count(r => r.Path == path);
                }
            }

            private async Task Loop()
            {
                while (_listener.IsListening)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await _listener.GetContextAsync().ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        return;
                    }

                    Handle(context);
                }
            }

            private void Handle(HttpListenerContext context)
            {
                string body = new System.IO.StreamReader(context.Request.InputStream, Encoding.UTF8).ReadToEnd();
                Seen seen = new Seen
                {
                    Path = context.Request.Url.AbsolutePath,
                    Authorization = context.Request.Headers["Authorization"],
                    Passphrase = context.Request.Headers["X-AxCrypt-Passphrase"],
                    Body = body,
                };
                lock (Requests)
                {
                    Requests.Add(seen);
                }

                int status = 200;
                string contentType = "application/json";
                string json = "{}";
                if (seen.Path == "/api/auth/login")
                {
                    if (RejectLogin)
                    {
                        status = 401;
                        json = "{\"code\":\"invalid_credentials\"}";
                        contentType = "application/problem+json";
                    }
                    else
                    {
                        string token = "token-" + Interlocked.Increment(ref _logins);
                        lock (ValidTokens)
                        {
                            ValidTokens.Add(token);
                        }
                        json = "{\"AccessToken\":\"" + token + "\",\"AccessTokenExpiresUtc\":\"" + DateTime.UtcNow.AddMinutes(15).ToString("o") + "\",\"RefreshToken\":\"refresh\",\"RefreshTokenExpiresUtc\":\"" + DateTime.UtcNow.AddDays(30).ToString("o") + "\"}";
                    }
                }
                else if (seen.Path == "/api/auth/refresh")
                {
                    status = 401;
                }
                else if (seen.Path != "/api/public/all/accounts/x@y.com/key" && seen.Path != "/api/global/apiversion")
                {
                    string token = seen.Authorization != null && seen.Authorization.StartsWith("Bearer ") ? seen.Authorization.Substring(7) : string.Empty;
                    bool valid;
                    lock (ValidTokens)
                    {
                        valid = ValidTokens.Contains(token);
                    }
                    if (!valid)
                    {
                        status = 401;
                        contentType = "application/problem+json";
                        json = "{\"title\":\"Unauthorized\",\"code\":\"token_expired\"}";
                    }
                }

                byte[] bytes = Encoding.UTF8.GetBytes(json);
                context.Response.StatusCode = status;
                context.Response.ContentType = contentType;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.Close();
            }

            public void Dispose()
            {
                _listener.Stop();
                _listener.Close();
            }
        }

        private static FakeApi _api;

        private static IRestCaller _caller;

        [SetUp]
        public static void Setup()
        {
            TypeMap.Register.Singleton<IInternetState>(() => new FakeInternetState());
            TypeMap.Register.Singleton<AxCryptOnlineState>(() => new AxCryptOnlineState());
            TypeMap.Register.Singleton<ITokenProvider>(() => new AuthTokenProvider());
            TypeMap.Register.New<IRestCaller>(() => (IRestCaller)Activator.CreateInstance(typeof(AuthTokenProvider).Assembly.GetType("AxCrypt.Mono.RestCaller")));
            _api = new FakeApi();
            _caller = New<IRestCaller>();
        }

        [TearDown]
        public static void Teardown()
        {
            _api.Dispose();
            TypeMap.Register.Clear();
        }

        private static RestRequest Get(string path)
        {
            return new RestRequest("GET", new Uri(_api.BaseUrl, path), TimeSpan.FromSeconds(10));
        }

        private static string Passphrase(string password)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
        }

        [Test]
        public static async Task TestSignsInOnceAndSendsTheBearerToken()
        {
            RestIdentity identity = new RestIdentity("a@b.com", "pw");

            RestResponse first = await _caller.SendAsync(identity, Get("users/my/account"));
            RestResponse second = await _caller.SendAsync(identity, Get("notification/list"));

            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(_api.Count("/api/auth/login"), Is.EqualTo(1));
            Assert.That(_api.Requests.Where(r => r.Path != "/api/auth/login").Select(r => r.Authorization), Is.All.EqualTo("Bearer token-1"));
        }

        [Test]
        public static async Task TestPassphraseIsSentOnlyToEndpointsThatNeedIt()
        {
            RestIdentity identity = new RestIdentity("a@b.com", "pw");

            await _caller.SendAsync(identity, Get("usersv2/my/account"));
            await _caller.SendAsync(identity, Get("users/my/account/keys/current"));
            await _caller.SendAsync(identity, Get("secrets/get"));
            await _caller.SendAsync(identity, Get("notification/list"));
            await _caller.SendAsync(identity, Get("inappsubs/pricing"));

            Dictionary<string, string> sent = _api.Requests.Where(r => r.Path != "/api/auth/login").ToDictionary(r => r.Path, r => r.Passphrase);
            Assert.That(sent["/api/usersv2/my/account"], Is.EqualTo(Passphrase("pw")));
            Assert.That(sent["/api/users/my/account/keys/current"], Is.EqualTo(Passphrase("pw")));
            Assert.That(sent["/api/secrets/get"], Is.EqualTo(Passphrase("pw")));
            Assert.That(sent["/api/notification/list"], Is.Null);
            Assert.That(sent["/api/inappsubs/pricing"], Is.Null);
        }

        [Test]
        public static async Task TestRejectedTokenIsReplacedAndTheRequestRepeatedOnce()
        {
            RestIdentity identity = new RestIdentity("a@b.com", "pw");
            await _caller.SendAsync(identity, Get("users/my/account"));
            lock (_api.ValidTokens)
            {
                _api.ValidTokens.Clear();
            }

            RestResponse response = await _caller.SendAsync(identity, Get("users/my/account"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(_api.Count("/api/auth/refresh"), Is.EqualTo(1), "tries the refresh token first");
            Assert.That(_api.Count("/api/auth/login"), Is.EqualTo(2), "then signs in again with the password");
            Assert.That(_api.Requests.Last().Authorization, Is.EqualTo("Bearer token-2"));
        }

        [Test]
        public static async Task TestAnonymousModeNeverSignsIn()
        {
            RestIdentity identity = new RestIdentity("a@b.com", "pw");
            RestRequest request = Get("users/all/accounts/a@b.com/password");
            request.AuthMode = RestAuthMode.None;

            await _caller.SendAsync(identity, request);

            Assert.That(_api.Count("/api/auth/login"), Is.EqualTo(0));
            Assert.That(_api.Requests.Single().Authorization, Is.Null);
            Assert.That(_api.Requests.Single().Passphrase, Is.Null);
        }

        [Test]
        public static async Task TestBasicModeSendsTheApiKeyAsBasicCredentials()
        {
            RestIdentity identity = new RestIdentity("a@b.com", "11111111-1111-1111-1111-111111111111");
            RestRequest request = Get("public/all/accounts/x@y.com/key");
            request.AuthMode = RestAuthMode.Basic;

            await _caller.SendAsync(identity, request);

            string expected = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("a@b.com:" + Passphrase("11111111-1111-1111-1111-111111111111")));
            Assert.That(_api.Count("/api/auth/login"), Is.EqualTo(0));
            Assert.That(_api.Requests.Single().Authorization, Is.EqualTo(expected));
        }

        [Test]
        public static async Task TestWrongPasswordDoesNotHammerTheLoginEndpoint()
        {
            _api.RejectLogin = true;
            RestIdentity identity = new RestIdentity("a@b.com", "wrong");

            RestResponse first = await _caller.SendAsync(identity, Get("users/my/account"));
            RestResponse second = await _caller.SendAsync(identity, Get("users/my/account"));

            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(_api.Count("/api/auth/login"), Is.EqualTo(1));
        }

        [Test]
        public static async Task TestJsonProblemBodiesAreNotTreatedAsOffline()
        {
            RestResponse response = await _caller.SendAsync(new RestIdentity(), Get("users/my/account"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(response.Content, Does.Contain("token_expired"));
        }

        [Test]
        public static async Task TestEmptyIdentitySendsNoCredentials()
        {
            await _caller.SendAsync(new RestIdentity(), Get("global/apiversion"));

            Assert.That(_api.Count("/api/auth/login"), Is.EqualTo(0));
            Assert.That(_api.Requests.Single().Authorization, Is.Null);
        }
    }
}
