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
 * The source is maintained at http://bitbucket.org/AxCrypt.Desktop.Window-net please visit for
 * updates, contributions and contact with the author. You may also visit
 * http://www.axcrypt.net for more information about the author.
*/

#endregion Coypright and License

using AxCrypt.Abstractions;
using AxCrypt.Api.Model;
using AxCrypt.Common;
using AxCrypt.Core.Crypto;
using AxCrypt.Core.Crypto.Asymmetric;
using AxCrypt.Core.Runtime;
using AxCrypt.Core.Service;
using AxCrypt.Core.UI;
using AxCrypt.Fake;
using Moq;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Core.Test
{
    /// <summary>
    /// Which plan an account gets from <see cref="LicensePolicy"/>: the legacy free plan for unpaid
    /// accounts registered before <see cref="LicensePolicy.LegacyFreeRegistrationCutoffUtc"/>, the
    /// viewer plan for later signups and expired subscriptions, and paid plans unchanged.
    /// </summary>
    [TestFixture]
    public class TestLicensePolicyPlans
    {
        private const string UserEmail = "plan.test@axcrypt.net";

        private static readonly DateTime Cutoff = new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime RegisteredBeforeCutoff = Cutoff.AddDays(-30);
        private static readonly DateTime RegisteredAfterCutoff = Cutoff.AddDays(1);

        private DateTime _originalCutoff;
        private bool _originalTrustAccountsWithoutRegistrationDate;
        private UserAccount _account;

        [SetUp]
        public void Setup()
        {
            SetupAssembly.AssemblySetup();
            SetupAssembly.AssemblySetupCrypto(CryptoImplementation.BouncyCastle);

            _originalCutoff = LicensePolicy.LegacyFreeRegistrationCutoffUtc;
            _originalTrustAccountsWithoutRegistrationDate = LicensePolicy.TrustAccountsWithoutRegistrationDate;
            LicensePolicy.LegacyFreeRegistrationCutoffUtc = Cutoff;
            LicensePolicy.TrustAccountsWithoutRegistrationDate = true;

            IAsymmetricKeyPair licenseAuthorityKeys = new UserKeyPair(EmailAddress.Parse("license@axcrypt.net"), 512).KeyPair;
            Mock<ILicenseAuthority> licenseAuthority = new Mock<ILicenseAuthority>();
            licenseAuthority.Setup(authority => authority.PrivateKeyAsync()).ReturnsAsync(licenseAuthorityKeys.PrivateKey);
            licenseAuthority.Setup(authority => authority.PublicKeyAsync()).ReturnsAsync(licenseAuthorityKeys.PublicKey);
            TypeMap.Register.Singleton<ILicenseAuthority>(() => licenseAuthority.Object);

            TypeMap.Register.New<LogOnIdentity, IAccountService>((LogOnIdentity identity) => CreateAccountService());

            // Offline, so a failed signature check doesn't look up the license authority key online.
            New<AxCryptOnlineState>().IsOffline = true;
        }

        [TearDown]
        public void Teardown()
        {
            LicensePolicy.LegacyFreeRegistrationCutoffUtc = _originalCutoff;
            LicensePolicy.TrustAccountsWithoutRegistrationDate = _originalTrustAccountsWithoutRegistrationDate;
            SetupAssembly.AssemblyTeardown();
        }

        [Test]
        public async Task RegisteredBeforeCutoffAndNeverPaidGetsLegacyFreePlan()
        {
            await GivenSignedFreeAccountAsync(RegisteredBeforeCutoff, hadAnyPaidSubscription: false);

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            AssertLegacyFreePlan(capabilities);
        }

        [Test]
        public async Task RegisteredOnCutoffGetsViewerPlan()
        {
            await GivenSignedFreeAccountAsync(Cutoff, hadAnyPaidSubscription: false);

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            AssertViewerPlan(capabilities);
        }

        [Test]
        public async Task RegisteredAfterCutoffGetsViewerPlan()
        {
            await GivenSignedFreeAccountAsync(RegisteredAfterCutoff, hadAnyPaidSubscription: false);

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            AssertViewerPlan(capabilities);
        }

        [Test]
        public async Task RegisteredBeforeCutoffButPaidBeforeGetsViewerPlan()
        {
            await GivenSignedFreeAccountAsync(RegisteredBeforeCutoff, hadAnyPaidSubscription: true);

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            AssertViewerPlan(capabilities);
        }

        [Test]
        public async Task NoRegistrationDateKeepsLegacyFreePlanWhileTrusted()
        {
            GivenFreeAccount(registeredUtc: null, hadAnyPaidSubscription: false);

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            AssertLegacyFreePlan(capabilities);
        }

        [Test]
        public async Task NoRegistrationDateGetsViewerPlanWhenNotTrusted()
        {
            LicensePolicy.TrustAccountsWithoutRegistrationDate = false;
            GivenFreeAccount(registeredUtc: null, hadAnyPaidSubscription: false);

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            AssertViewerPlan(capabilities);
        }

        [Test]
        public async Task NoRegistrationDateAndPaidBeforeGetsViewerPlan()
        {
            GivenFreeAccount(registeredUtc: null, hadAnyPaidSubscription: true);

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            AssertViewerPlan(capabilities);
        }

        [Test]
        public async Task RegistrationDateWithoutSignatureIsTreatedLikeNoDate()
        {
            GivenFreeAccount(RegisteredAfterCutoff, hadAnyPaidSubscription: false);

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            AssertLegacyFreePlan(capabilities);
        }

        [Test]
        public async Task EditedRegistrationDateGetsViewerPlan()
        {
            await GivenSignedFreeAccountAsync(RegisteredAfterCutoff, hadAnyPaidSubscription: false);
            _account.RegisteredUtc = RegisteredBeforeCutoff;

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            AssertViewerPlan(capabilities);
        }

        [Test]
        public async Task EditedPaidHistoryGetsViewerPlan()
        {
            await GivenSignedFreeAccountAsync(RegisteredBeforeCutoff, hadAnyPaidSubscription: true);
            _account.HadAnyPaidSubscription = false;

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            AssertViewerPlan(capabilities);
        }

        [Test]
        public async Task SignatureThatIsNotBase64GetsViewerPlan()
        {
            GivenFreeAccount(RegisteredBeforeCutoff, hadAnyPaidSubscription: false);
            _account.RegistrationSignature = "not a signature";

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            AssertViewerPlan(capabilities);
        }

        [Test]
        public async Task ExpiredPremiumGetsViewerPlanEvenWhenRegisteredBeforeCutoff()
        {
            await GivenSignedPaidAccountAsync(SubscriptionLevel.Premium, DateTime.UtcNow.AddDays(-1));

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            AssertViewerPlan(capabilities);
        }

        [Test]
        public async Task ActivePremiumIsUnchanged()
        {
            await GivenSignedPaidAccountAsync(SubscriptionLevel.Premium, DateTime.UtcNow.AddDays(30));

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            Assert.That(capabilities.Has(LicenseCapability.Premium), Is.True);
            Assert.That(capabilities.Has(LicenseCapability.EncryptNewFiles), Is.True);
            Assert.That(capabilities.Has(LicenseCapability.ShareKeys), Is.True);
            Assert.That(capabilities.Has(LicenseCapability.Viewer), Is.False);
        }

        [Test]
        public async Task ActiveBusinessIsUnchanged()
        {
            await GivenSignedPaidAccountAsync(SubscriptionLevel.Business, DateTime.UtcNow.AddDays(30));

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            Assert.That(capabilities.Has(LicenseCapability.Business), Is.True);
            Assert.That(capabilities.Has(LicenseCapability.Premium), Is.True);
            Assert.That(capabilities.Has(LicenseCapability.EncryptNewFiles), Is.True);
        }

        [Test]
        public async Task ActivePasswordManagerIsViewerPlusUnlimitedPasswords()
        {
            await GivenSignedPaidAccountAsync(SubscriptionLevel.PasswordManager, DateTime.UtcNow.AddDays(30));

            LicenseCapabilities capabilities = await RefreshedCapabilitiesAsync();

            Assert.That(capabilities.Has(LicenseCapability.PasswordManager), Is.True);
            Assert.That(capabilities.Has(LicenseCapability.CreateUnlimitedPasswords), Is.True);
            Assert.That(capabilities.Has(LicenseCapability.Viewer), Is.True);
            Assert.That(capabilities.Has(LicenseCapability.EncryptNewFiles), Is.False);
        }

        [Test]
        public async Task SignedOutUserGetsLegacyFreeCapabilities()
        {
            LicensePolicy policy = new LicensePolicy();

            await policy.RefreshAsync(LogOnIdentity.Empty);

            AssertLegacyFreePlan(policy.Capabilities);
        }

        private static void AssertLegacyFreePlan(LicenseCapabilities capabilities)
        {
            Assert.That(capabilities.Has(LicenseCapability.EncryptNewFiles), Is.True, "Legacy free users can encrypt (metered).");
            Assert.That(capabilities.Has(LicenseCapability.ShareKeys), Is.True, "Legacy free users can share keys (metered).");
            Assert.That(capabilities.Has(LicenseCapability.Viewer), Is.False);
            Assert.That(capabilities.Has(LicenseCapability.Premium), Is.False);
        }

        private static void AssertViewerPlan(LicenseCapabilities capabilities)
        {
            Assert.That(capabilities.Has(LicenseCapability.Viewer), Is.True);
            Assert.That(capabilities.Has(LicenseCapability.EncryptNewFiles), Is.False, "Viewer users can only open and decrypt.");
            Assert.That(capabilities.Has(LicenseCapability.ShareKeys), Is.False, "Viewer users can't share keys.");
            Assert.That(capabilities.Has(LicenseCapability.Premium), Is.False);
        }

        private void GivenFreeAccount(DateTime? registeredUtc, bool hadAnyPaidSubscription)
        {
            _account = new UserAccount(UserEmail, SubscriptionLevel.Free, DateTime.MinValue, AccountStatus.Verified, Offers.None)
            {
                RegisteredUtc = registeredUtc,
                HadAnyPaidSubscription = hadAnyPaidSubscription,
            };
        }

        private async Task GivenSignedFreeAccountAsync(DateTime registeredUtc, bool hadAnyPaidSubscription)
        {
            GivenFreeAccount(registeredUtc, hadAnyPaidSubscription);
            _account.RegistrationSignature = await new LicenseValidation().SignRegistrationAsync(_account);
        }

        private async Task GivenSignedPaidAccountAsync(SubscriptionLevel level, DateTime expirationUtc)
        {
            _account = new UserAccount(UserEmail, level, expirationUtc, AccountStatus.Verified, Offers.None)
            {
                RegisteredUtc = RegisteredBeforeCutoff,
                HadAnyPaidSubscription = false,
            };
            _account.Signature = await new LicenseValidation().SignAsync(_account);
            _account.RegistrationSignature = await new LicenseValidation().SignRegistrationAsync(_account);
        }

        private static async Task<LicenseCapabilities> RefreshedCapabilitiesAsync()
        {
            LicensePolicy policy = new LicensePolicy();
            await policy.RefreshAsync(new LogOnIdentity(EmailAddress.Parse(UserEmail), new Passphrase("password")));
            return policy.Capabilities;
        }

        private IAccountService CreateAccountService()
        {
            Mock<IAccountService> accountService = new Mock<IAccountService>();
            accountService.Setup(service => service.AccountAsync()).ReturnsAsync(() => _account);
            return accountService.Object;
        }
    }
}
