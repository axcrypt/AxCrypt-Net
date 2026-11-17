namespace AxCrypt.Core.Runtime
{
    public enum LicenseCapability
    {
        SecureWipe,
        StrongerEncryption,
        KeySharing,
        RandomRename,
        SecureFolders,
        CloudStorageAwareness,
        PasswordManagement,
        PasswordGeneration,
        DirectSupport,
        StandardEncryption,
        AccountKeyBackup,
        CommunitySupport,
        Premium,
        IncludeSubfolders,
        EncryptNewFiles,
        EditExistingFiles,
        InactivitySignOut,
        Viewer,
        Business,
        PasswordManager,

        ShareSecretFree,
        ShareSecretPasswordManager,
        ShareSecretPremium,
        ShareSecretBusiness,

        CreateUnlimitedPasswords,

        GroupSharing,
        SendUnlimitedMessages,

        FindFiles,
        Vault,

        TextEncryptionPremium,
        TextEncryptionBusiness,

        // Append new values at the end only: values are compared by name and
        // ordinal, so inserting in the middle would shift existing members.

        /// <summary>Start a new key share of encrypted files from the UI. Distinct from
        /// <see cref="KeySharing"/>, which free users keep so that re-encrypting a file
        /// shared with them preserves the existing key holders.</summary>
        ShareKeys,

        /// <summary>Invite other users to AxCrypt from the app.</summary>
        InviteUsers,
    }
}