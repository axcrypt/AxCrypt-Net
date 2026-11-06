#region Coypright and License

/*
 * AxCrypt - Copyright 2026, AxCrypt AB All Rights Reserved
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
*/

#endregion Coypright and License

using AxCrypt.Abstractions;
using AxCrypt.Api.Model;
using AxCrypt.Common;
using AxCrypt.Core.Crypto;
using AxCrypt.Core.Extensions;
using AxCrypt.Core.Service;
using AxCrypt.Core.Session;
using AxCrypt.Core.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Core.Runtime
{
    /// <summary>
    /// Handle license policy based on the current licensing and subscription status. All instance methods are thread safe.
    /// </summary>
    public class LicensePolicy : IDisposable, IEquatable<LicensePolicy>
    {
        /// <summary>
        /// Free accounts registered before this date (UTC) that never had a paid subscription
        /// keep the legacy free plan (<see cref="FreeCapabilitySet"/>). Everyone else without an
        /// active subscription gets the viewer plan (<see cref="ViewerCapabilitySet"/>).
        /// </summary>
        public static DateTime LegacyFreeRegistrationCutoffUtc { get; set; } = new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Whether a free account without a registration date keeps the legacy free plan. True until
        /// every server sends a signed registration date; then set it to false so that removing the
        /// date from the saved account no longer grants the legacy free plan.
        /// </summary>
        public static bool TrustAccountsWithoutRegistrationDate { get; set; } = true;

        /// <summary>
        /// Legacy free plan, only for accounts registered before
        /// <see cref="LegacyFreeRegistrationCutoffUtc"/> that never paid. Encrypting and key
        /// sharing are allowed but metered by the app (entitlement usage limits).
        /// </summary>
        protected static readonly HashSet<LicenseCapability> FreeCapabilitySet = new HashSet<LicenseCapability>(new LicenseCapability[]
        {
            LicenseCapability.StandardEncryption,
            LicenseCapability.AccountKeyBackup,
            LicenseCapability.CommunitySupport,
            LicenseCapability.EncryptNewFiles,
            LicenseCapability.EditExistingFiles,
            LicenseCapability.ShareSecretFree,
            LicenseCapability.KeySharing,
            LicenseCapability.ShareKeys,
        });

        /// <summary>
        /// Viewer plan: open and decrypt existing files only, with no encrypting new files,
        /// editing encrypted files or key sharing. Applies to free accounts registered on or
        /// after <see cref="LegacyFreeRegistrationCutoffUtc"/> and to expired subscriptions.
        /// </summary>
        protected static readonly HashSet<LicenseCapability> ViewerCapabilitySet = new HashSet<LicenseCapability>(new LicenseCapability[]
        {
            LicenseCapability.StandardEncryption,
            LicenseCapability.AccountKeyBackup,
            LicenseCapability.CommunitySupport,
            LicenseCapability.Viewer,
            LicenseCapability.ShareSecretFree,
        });

        /// <summary>
        /// Password Manager plan: unlimited password manager on top of the viewer capabilities.
        /// </summary>
        protected static readonly HashSet<LicenseCapability> PasswordManagerCapabilitySet = new HashSet<LicenseCapability>(ViewerCapabilitySet.Concat(new LicenseCapability[]
        {
            LicenseCapability.PasswordManagement,
            LicenseCapability.PasswordGeneration,
            LicenseCapability.PasswordManager,
            LicenseCapability.ShareSecretPasswordManager,
            LicenseCapability.CreateUnlimitedPasswords,
        }));

        protected static readonly HashSet<LicenseCapability> PremiumCapabilitySet = new HashSet<LicenseCapability>(FreeCapabilitySet.Concat(new LicenseCapability[]
        {
            LicenseCapability.SecureWipe,
            LicenseCapability.StrongerEncryption,
            LicenseCapability.RandomRename,
            LicenseCapability.SecureFolders,
            LicenseCapability.CloudStorageAwareness,
            LicenseCapability.PasswordManagement,
            LicenseCapability.PasswordGeneration,
            LicenseCapability.DirectSupport,
            LicenseCapability.IncludeSubfolders,
            LicenseCapability.Premium,
            LicenseCapability.InactivitySignOut,
            LicenseCapability.ShareSecretPremium,
            LicenseCapability.SendUnlimitedMessages,
            LicenseCapability.FindFiles,
            LicenseCapability.Vault,
            LicenseCapability.TextEncryptionPremium,
            LicenseCapability.CreateUnlimitedPasswords,
            LicenseCapability.InviteUsers,
        }));

        protected static readonly HashSet<LicenseCapability> BusinessCapabilitySet = new HashSet<LicenseCapability>(PremiumCapabilitySet.Concat(new LicenseCapability[]
        {
            LicenseCapability.Business,
            LicenseCapability.GroupSharing,
            LicenseCapability.TextEncryptionBusiness,
        }));

        public LicensePolicy() : this(true)
        {
        }

        private bool _handleNotifications;

        protected LicensePolicy(bool handleNotifications)
        {
            _handleNotifications = handleNotifications;
            if (handleNotifications)
            {
                New<SessionNotify>().AddPriorityCommand(LicensePolicy_CommandAsync);
            }
        }

        private async Task LicensePolicy_CommandAsync(SessionNotification notification)
        {
            switch (notification.NotificationType)
            {
                case SessionNotificationType.RefreshLicensePolicy:
                case SessionNotificationType.SignIn:
                    await RefreshAsync(notification.Identity);
                    break;

                case SessionNotificationType.SessionStart:
                case SessionNotificationType.SignOut:
                    await RefreshAsync(LogOnIdentity.Empty);
                    break;

                default:
                    break;
            }
        }

        private LicenseCapabilities _mostRecentCapabilities = null;

        public async Task RefreshAsync(LogOnIdentity identity)
        {
            Expiration = await SubscriptionExpirationAsync(identity).Free();

            Capabilities = await CapabilitiesAsync(identity).Free();
            if (Capabilities != _mostRecentCapabilities)
            {
                _mostRecentCapabilities = Capabilities;
                await New<SessionNotify>().NotifyAsync(new SessionNotification(SessionNotificationType.LicensePolicyChanged, identity, Capabilities));
            }
        }

        private async Task<UserAccount> UserAccountAsync(LogOnIdentity identity)
        {
            return await New<LogOnIdentity, IAccountService>(identity).AccountAsync().Free();
        }

        /// <summary>
        /// Gets the time left at this subscription level
        /// </summary>
        /// <value>
        /// The time left offline.
        /// </value>
        private async Task<TimeSpan> TimeUntilSubscriptionExpiration(LogOnIdentity identity)
        {
            DateTime utcNow = New<INow>().Utc;
            if (utcNow >= await SubscriptionExpirationAsync(identity).Free())
            {
                return TimeSpan.Zero;
            }

            TimeSpan untilExpiration = await SubscriptionExpirationAsync(identity).Free() - utcNow;
            return untilExpiration;
        }

        private async Task<LicenseCapabilities> CapabilitiesAsync(LogOnIdentity identity)
        {
            SubscriptionLevel level = await SubscriptionLevelAsync(identity).Free();
            switch (level)
            {
                case SubscriptionLevel.Business:
                    if (await TimeUntilSubscriptionExpiration(identity).Free() > TimeSpan.Zero)
                    {
                        return BusinessCapabilities;
                    }
                    break;

                case SubscriptionLevel.Premium:
                    if (await TimeUntilSubscriptionExpiration(identity).Free() > TimeSpan.Zero)
                    {
                        return PremiumCapabilities;
                    }
                    break;

                case SubscriptionLevel.PasswordManager:
                    if (await TimeUntilSubscriptionExpiration(identity).Free() > TimeSpan.Zero)
                    {
                        return PasswordManagerCapabilities;
                    }
                    break;

                default:
                    break;
            }

            // No active subscription: the legacy free plan for existing free users,
            // the viewer plan for new signups and expired subscriptions.
            return await IsLegacyFreeAccountAsync(identity, level).Free() ? FreeCapabilities : ViewerCapabilities;
        }

        /// <summary>
        /// True when an account without an active subscription keeps the legacy free plan:
        /// it never had a paid subscription and was registered before
        /// <see cref="LegacyFreeRegistrationCutoffUtc"/>.
        /// </summary>
        private async Task<bool> IsLegacyFreeAccountAsync(LogOnIdentity identity, SubscriptionLevel level)
        {
            if (identity.UserEmail == EmailAddress.Empty)
            {
                return true;
            }

            // A paid level that reached this point has expired.
            if (level == SubscriptionLevel.Premium || level == SubscriptionLevel.Business || level == SubscriptionLevel.PasswordManager)
            {
                return false;
            }

            UserAccount account = await UserAccountAsync(identity).Free();

            // Server without signed registration dates yet: the paid history cannot be verified
            // either. A missing signature is treated like a missing date (removing it grants
            // nothing that removing the date would not), while a wrong signature is rejected below.
            if (!account.RegisteredUtc.HasValue || string.IsNullOrEmpty(account.RegistrationSignature))
            {
                return TrustAccountsWithoutRegistrationDate && !account.HadAnyPaidSubscription;
            }

            // The date and paid history come from the saved account offline, so they are only
            // trusted when the license authority signed them. Edited values give the viewer plan.
            if (!await new LicenseValidation().IsRegistrationValidAsync(account).Free())
            {
                return false;
            }

            if (account.HadAnyPaidSubscription)
            {
                return false;
            }

            return LicenseValidation.ToUtc(account.RegisteredUtc.Value) < LegacyFreeRegistrationCutoffUtc;
        }

        private async Task<SubscriptionLevel> SubscriptionLevelAsync(LogOnIdentity identity)
        {
            if (identity.UserEmail == EmailAddress.Empty)
            {
                return SubscriptionLevel.Unknown;
            }
            return (await (await UserAccountAsync(identity).Free()).ValidatedLevelAsync().Free());
        }

        private async Task<DateTime> SubscriptionExpirationAsync(LogOnIdentity identity)
        {
            if (identity.UserEmail == EmailAddress.Empty)
            {
                return DateTime.MaxValue;
            }
            return (await UserAccountAsync(identity).Free()).LevelExpiration;
        }

        protected virtual LicenseCapabilities FreeCapabilities { get { return new LicenseCapabilities(FreeCapabilitySet); } }

        protected virtual LicenseCapabilities ViewerCapabilities { get { return new LicenseCapabilities(ViewerCapabilitySet); } }

        protected virtual LicenseCapabilities PasswordManagerCapabilities { get { return new LicenseCapabilities(PasswordManagerCapabilitySet); } }

        protected virtual LicenseCapabilities PremiumCapabilities { get { return new LicenseCapabilities(PremiumCapabilitySet); } }

        protected virtual LicenseCapabilities BusinessCapabilities { get { return new LicenseCapabilities(BusinessCapabilitySet); } }

        private LicenseCapabilities _currentLicenseCapabilities = null;

        public virtual LicenseCapabilities Capabilities
        {
            get
            {
                if (_currentLicenseCapabilities == null)
                {
                    _currentLicenseCapabilities = FreeCapabilities;
                }
                return _currentLicenseCapabilities;
            }
            protected set
            {
                _currentLicenseCapabilities = value;
            }
        }

        public DateTime Expiration { get; private set; } = DateTime.MaxValue;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || disposed)
            {
                return;
            }

            if (_handleNotifications)
            {
                New<SessionNotify>().RemovePriorityCommand(LicensePolicy_CommandAsync);
                _handleNotifications = false;
            }
            disposed = true;
        }

        #region IEquatable<LicensePolicy> Members

        public bool Equals(LicensePolicy other)
        {
            if ((object)other == null)
            {
                return false;
            }
            if (Capabilities != other.Capabilities)
            {
                return false;
            }
            return true;
        }

        #endregion IEquatable<LicensePolicy> Members

        public override bool Equals(object obj)
        {
            LicensePolicy other = obj as LicensePolicy;
            if (other == null)
            {
                return false;
            }

            return Equals(other);
        }

        public override int GetHashCode()
        {
            return Capabilities.GetHashCode();
        }

        public static bool operator ==(LicensePolicy left, LicensePolicy right)
        {
            if (Object.ReferenceEquals(left, right))
            {
                return true;
            }
            if ((object)left == null)
            {
                return false;
            }
            return left.Equals(right);
        }

        public static bool operator !=(LicensePolicy left, LicensePolicy right)
        {
            return !(left == right);
        }
    }
}