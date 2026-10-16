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
using AxCrypt.Api.Model.KeyShare;
using AxCrypt.Common;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Api.Test
{
    [TestFixture]
    public static class TestAuthentication
    {
        private static readonly Uri Base = new Uri("http://localhost/api/");

        private static Mock<IRestCaller> Caller(HttpStatusCode status, string content, List<RestRequest> captured)
        {
            Mock<IRestCaller> caller = new Mock<IRestCaller>();
            caller.Setup(c => c.SendAsync(It.IsAny<RestIdentity>(), It.IsAny<RestRequest>()))
                .Callback<RestIdentity, RestRequest>((identity, request) => captured.Add(request))
                .Returns(() => Task.FromResult(new RestResponse(status, content)));
            caller.Setup(c => c.UrlEncode(It.IsAny<string>())).Returns<string>(WebUtility.UrlEncode);
            TypeMap.Register.New<IRestCaller>(() => caller.Object);
            return caller;
        }

        [SetUp]
        public static void Setup()
        {
            TypeMap.Register.Singleton<IStringSerializer>(() => new Mock<IStringSerializer>().Object);
        }

        [TearDown]
        public static void Teardown()
        {
            TypeMap.Register.Clear();
        }

        [Test]
        public static async Task TestApiKeyLookupUsesBasicAuthentication()
        {
            List<RestRequest> requests = new List<RestRequest>();
            Caller(HttpStatusCode.OK, "{}", requests);
            Mock<IStringSerializer> serializer = new Mock<IStringSerializer>();
            serializer.Setup(s => s.Deserialize<Api.Model.AccountKey>(It.IsAny<string>())).Returns((Api.Model.AccountKey)null);
            TypeMap.Register.Singleton<IStringSerializer>(() => serializer.Object);

            AxCryptApiClient client = new AxCryptApiClient(new RestIdentity("partner@example.com", "api-key"), Base, TimeSpan.Zero);
            await client.GetPublicApiKeyAllAccountsOtherUserPublicKeyAsync("x@y.com");

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].AuthMode, Is.EqualTo(RestAuthMode.Basic));
        }

        [Test]
        public static async Task TestSettingAPasswordWithAVerificationCodeIsAnonymous()
        {
            List<RestRequest> requests = new List<RestRequest>();
            Caller(HttpStatusCode.OK, "{}", requests);
            Mock<IStringSerializer> serializer = new Mock<IStringSerializer>();
            serializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            TypeMap.Register.Singleton<IStringSerializer>(() => serializer.Object);

            AxCryptApiClient client = new AxCryptApiClient(new RestIdentity("a@b.com", "new-password"), Base, TimeSpan.Zero);
            await client.PutAllAccountsUserPasswordAsync("code");

            Assert.That(requests[0].AuthMode, Is.EqualTo(RestAuthMode.None), "a failed sign-in attempt would count towards the account lockout");
        }

        [Test]
        public static async Task TestOrdinaryCallsUseBearerAuthentication()
        {
            List<RestRequest> requests = new List<RestRequest>();
            Caller(HttpStatusCode.OK, "{}", requests);
            Mock<IStringSerializer> serializer = new Mock<IStringSerializer>();
            serializer.Setup(s => s.Deserialize<Api.Model.AccountKey>(It.IsAny<string>())).Returns((Api.Model.AccountKey)null);
            TypeMap.Register.Singleton<IStringSerializer>(() => serializer.Object);

            await new AxCryptApiClient(new RestIdentity("a@b.com", "pw"), Base, TimeSpan.Zero).MyAccountKeysCurrentAsync();

            Assert.That(requests[0].AuthMode, Is.EqualTo(RestAuthMode.Bearer));
        }

        [Test]
        public static void TestEnsureStatusOkExplainsRateLimiting()
        {
            RestResponse limited = new RestResponse((HttpStatusCode)429, "{\"code\":\"rate_limited\"}", TimeSpan.FromSeconds(30));

            ApiException exception = Assert.Throws<ApiException>(() => ApiCaller.EnsureStatusOk(limited));

            Assert.That(exception.HttpStatusCode, Is.EqualTo((HttpStatusCode)429));
            Assert.That(exception.Message, Does.Contain("Too many requests").And.Contain("30"));
        }

        [Test]
        public static void TestEnsureStatusOkStillMapsUnauthorized()
        {
            Assert.Throws<UnauthorizedException>(() => ApiCaller.EnsureStatusOk(new RestResponse(HttpStatusCode.Unauthorized, "{\"code\":\"invalid_credentials\"}")));
        }

        [TestCase("{\"title\":\"x\",\"code\":\"session_revoked\",\"traceId\":\"t\"}", "session_revoked")]
        [TestCase("{\"title\":\"x\"}", "")]
        [TestCase("not json", "")]
        [TestCase("", "")]
        public static void TestErrorCode(string body, string expected)
        {
            Assert.That(ApiCaller.ErrorCode(new RestResponse(HttpStatusCode.Unauthorized, body)), Is.EqualTo(expected));
        }

        [Test]
        public static async Task TestKeyShareNotifiesLocalAndCloudFiles()
        {
            List<RestRequest> requests = new List<RestRequest>();
            Caller(HttpStatusCode.OK, "true", requests);
            Mock<IStringSerializer> serializer = new Mock<IStringSerializer>();
            serializer.Setup(s => s.Serialize(It.IsAny<object>())).Returns("{}");
            serializer.Setup(s => s.Deserialize<bool>("true")).Returns(true);
            TypeMap.Register.Singleton<IStringSerializer>(() => serializer.Object);
            AxKeyShareApiClient client = new AxKeyShareApiClient(new RestIdentity("a@b.com", "pw"), Base, TimeSpan.Zero);

            bool local = await client.NotifyLocalFileAsync(new KeyShareApiModel { Recipients = new[] { "r@x.com" }, SharedFileName = "f.txt" });
            bool cloud = await client.NotifyCloudFileAsync(new KeyShareApiModel { Recipients = new[] { "r@x.com" }, SharedFileName = "f.txt", SharedLink = "https://cloud.example/f" });

            Assert.That(local && cloud, Is.True);
            Assert.That(requests[0].Url, Is.EqualTo(new Uri("http://localhost/api/keyshare/notify/local")));
            Assert.That(requests[1].Url, Is.EqualTo(new Uri("http://localhost/api/keyshare/notify/cloud")));
        }

        [Test]
        public static void TestKeyShareRejectsNonHttpsCloudLinksWithoutCallingTheApi()
        {
            List<RestRequest> requests = new List<RestRequest>();
            Caller(HttpStatusCode.OK, "true", requests);
            AxKeyShareApiClient client = new AxKeyShareApiClient(new RestIdentity("a@b.com", "pw"), Base, TimeSpan.Zero);

            Assert.ThrowsAsync<ArgumentException>(() => client.NotifyCloudFileAsync(new KeyShareApiModel { Recipients = new[] { "r@x.com" }, SharedLink = "http://insecure.example/f" }));
            Assert.That(requests, Is.Empty);
        }

        [Test]
        public static async Task TestKeyShareWithoutRecipientsIsANoOp()
        {
            List<RestRequest> requests = new List<RestRequest>();
            Caller(HttpStatusCode.OK, "true", requests);
            AxKeyShareApiClient client = new AxKeyShareApiClient(new RestIdentity("a@b.com", "pw"), Base, TimeSpan.Zero);

            Assert.That(await client.NotifyLocalFileAsync(new KeyShareApiModel { Recipients = new string[0] }), Is.True);
            Assert.That(requests, Is.Empty);
        }
    }
}
