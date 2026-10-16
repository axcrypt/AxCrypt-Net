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
using Moq;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Api.Test
{
    [TestFixture]
    public static class TestAuthApiClient
    {
        private static readonly Uri Base = new Uri("https://api.example.test/api/");

        private static Mock<ITokenProvider> Provider()
        {
            Mock<ITokenProvider> provider = new Mock<ITokenProvider>();
            TypeMap.Register.Singleton<ITokenProvider>(() => provider.Object);
            return provider;
        }

        [TearDown]
        public static void Teardown()
        {
            TypeMap.Register.Clear();
        }

        [Test]
        public static async Task TestLoginPassesTheIdentityAndTheApiAuthorityToTheProvider()
        {
            Mock<ITokenProvider> provider = Provider();
            provider.Setup(p => p.SignInAsync(Base, "a@b.com", "pw")).ReturnsAsync(new SignInResult(SignInOutcome.Success));

            SignInResult result = await new AxAuthApiClient(new RestIdentity("a@b.com", "pw"), Base, TimeSpan.Zero).LoginAsync();

            Assert.That(result.Succeeded, Is.True);
            provider.VerifyAll();
        }

        [Test]
        public static async Task TestLogoutAndLogoutEverywhereCallTheProvider()
        {
            Mock<ITokenProvider> provider = Provider();
            provider.Setup(p => p.SignOutAsync("a@b.com")).Returns(Task.CompletedTask);
            provider.Setup(p => p.SignOutEverywhereAsync("a@b.com")).Returns(Task.CompletedTask);
            AxAuthApiClient client = new AxAuthApiClient(new RestIdentity("a@b.com", "pw"), Base, TimeSpan.Zero);

            await client.LogoutAsync();
            await client.LogoutEverywhereAsync();

            provider.Verify(p => p.SignOutAsync("a@b.com"), Times.Once);
            provider.Verify(p => p.SignOutEverywhereAsync("a@b.com"), Times.Once);
        }

        [Test]
        public static void TestEnsureSignedInMapsEachOutcomeToAnException()
        {
            Assert.DoesNotThrow(() => AxAuthApiClient.EnsureSignedIn(new SignInResult(SignInOutcome.Success)));

            UnauthorizedException invalid = Assert.Throws<UnauthorizedException>(() => AxAuthApiClient.EnsureSignedIn(new SignInResult(SignInOutcome.InvalidCredentials)));
            Assert.That(invalid.Message, Is.EqualTo("invalid_credentials"));

            UnauthorizedException disabled = Assert.Throws<UnauthorizedException>(() => AxAuthApiClient.EnsureSignedIn(new SignInResult(SignInOutcome.AccountDisabled)));
            Assert.That(disabled.Message, Is.EqualTo("account_disabled"));

            ApiException limited = Assert.Throws<ApiException>(() => AxAuthApiClient.EnsureSignedIn(new SignInResult(SignInOutcome.RateLimited, TimeSpan.FromSeconds(30))));
            Assert.That(limited.HttpStatusCode, Is.EqualTo((System.Net.HttpStatusCode)429));
            Assert.That(limited.Message, Does.Contain("30"));
        }

        [Test]
        public static void TestEnsureSignedInTreatsUnavailableAsOffline()
        {
            TypeMap.Register.Singleton<AxCryptOnlineState>(() => new AxCryptOnlineState());

            Assert.Throws<OfflineApiException>(() => AxAuthApiClient.EnsureSignedIn(new SignInResult(SignInOutcome.Unavailable)));
        }
    }
}
