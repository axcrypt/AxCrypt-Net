using AxCrypt.Api.Model;
using AxCrypt.Core.Crypto.Asymmetric;
using AxCrypt.Core.Extensions;
using AxCrypt.Core.Session;
using AxCrypt.Core.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Core.Runtime
{
    public class LicenseValidation
    {
        public async Task<string> SignAsync(UserAccount userAccount)
        {
            if (userAccount == null)
            {
                throw new ArgumentNullException(nameof(userAccount));
            }

            if (userAccount.SubscriptionLevel <= SubscriptionLevel.Free)
            {
                return string.Empty;
            }

            IAsymmetricPrivateKey privateKey = await New<ILicenseAuthority>().PrivateKeyAsync();
            byte[] signature = new Signer(privateKey).Sign(SignedFields(userAccount));

            return Convert.ToBase64String(signature);
        }

        public async Task<SubscriptionLevel> ValidateLevelAsync(UserAccount userAccount)
        {
            if (userAccount == null)
            {
                throw new ArgumentNullException(nameof(userAccount));
            }

            if (userAccount.SubscriptionLevel <= SubscriptionLevel.Free)
            {
                return userAccount.SubscriptionLevel;
            }
            if (userAccount.Signature == string.Empty)
            {
                return SubscriptionLevel.Unknown;
            }

            IAsymmetricPublicKey publicKey = await New<ILicenseAuthority>().PublicKeyAsync();
            if (new Verifier(publicKey).Verify(Convert.FromBase64String(userAccount.Signature), SignedFields(userAccount)))
            {
                return userAccount.SubscriptionLevel;
            }

            using (KnownPublicKeys knownKeys = New<KnownPublicKeys>())
            {
                publicKey = (await knownKeys.GetAsync(EmailAddress.Parse(New<UserSettings>().LicenseAuthorityEmail), New<KnownIdentities>().DefaultEncryptionIdentity))?.PublicKey;
                if (publicKey != null && new Verifier(publicKey).Verify(Convert.FromBase64String(userAccount.Signature), SignedFields(userAccount)))
                {
                    return userAccount.SubscriptionLevel;
                }
            }

            return SubscriptionLevel.Unknown;
        }

        /// <summary>
        /// Signs the fields that decide whether a free account keeps the legacy free plan: the
        /// registration date and whether the account ever paid. Kept apart from <see cref="SignAsync"/>
        /// so the license signature that existing clients verify does not change. Free accounts are
        /// signed too. Returns an empty string when the account has no registration date.
        /// </summary>
        public async Task<string> SignRegistrationAsync(UserAccount userAccount)
        {
            if (userAccount == null)
            {
                throw new ArgumentNullException(nameof(userAccount));
            }

            if (!userAccount.RegisteredUtc.HasValue)
            {
                return string.Empty;
            }

            IAsymmetricPrivateKey privateKey = await New<ILicenseAuthority>().PrivateKeyAsync();
            byte[] signature = new Signer(privateKey).Sign(RegistrationSignedFields(userAccount));

            return Convert.ToBase64String(signature);
        }

        /// <summary>
        /// True when <see cref="UserAccount.RegistrationSignature"/> was made by the license authority
        /// for this account's registration date and paid history, i.e. they were not edited on the device.
        /// </summary>
        public async Task<bool> IsRegistrationValidAsync(UserAccount userAccount)
        {
            if (userAccount == null)
            {
                throw new ArgumentNullException(nameof(userAccount));
            }

            if (!userAccount.RegisteredUtc.HasValue || string.IsNullOrEmpty(userAccount.RegistrationSignature))
            {
                return false;
            }

            try
            {
                byte[] signature = Convert.FromBase64String(userAccount.RegistrationSignature);
                string[] fields = RegistrationSignedFields(userAccount);

                IAsymmetricPublicKey publicKey = await New<ILicenseAuthority>().PublicKeyAsync();
                if (new Verifier(publicKey).Verify(signature, fields))
                {
                    return true;
                }

                using (KnownPublicKeys knownKeys = New<KnownPublicKeys>())
                {
                    publicKey = (await knownKeys.GetAsync(EmailAddress.Parse(New<UserSettings>().LicenseAuthorityEmail), New<KnownIdentities>().DefaultEncryptionIdentity))?.PublicKey;
                    return publicKey != null && new Verifier(publicKey).Verify(signature, fields);
                }
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// The date as UTC. Dates without a kind are already UTC (the server sends and stores UTC).
        /// </summary>
        public static DateTime ToUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static string[] RegistrationSignedFields(UserAccount ua)
        {
            string email = ua.UserName;
            string registered = ToUtc(ua.RegisteredUtc.Value).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            string hadAnyPaidSubscription = ua.HadAnyPaidSubscription ? "paid" : "never-paid";
            return new string[] { email, "registered", registered, hadAnyPaidSubscription, };
        }

        private static string[] SignedFields(UserAccount ua)
        {
            string email = ua.UserName;
            string level = ua.SubscriptionLevel.ToString();
            string expiration = ua.LevelExpiration.ToString("u");
            return new string[] { email, level, expiration, };
        }
    }
}