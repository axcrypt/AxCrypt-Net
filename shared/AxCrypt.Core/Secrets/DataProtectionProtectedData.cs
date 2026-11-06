using AxCrypt.Abstractions;
using AxCrypt.Core.Extensions;
using AxCrypt.Core.Header;
using Microsoft.AspNetCore.DataProtection;
using System;
using System.Security.Cryptography;

namespace AxCrypt.Core.Secrets
{
    /// <summary>
    /// Cross-platform <see cref="IProtectedData"/> backed by ASP.NET Core Data Protection.
    /// Use on servers (Linux/Docker) where Windows DPAPI (System.Security.Cryptography.ProtectedData) is not supported.
    /// </summary>
    /// <remarks>
    /// Data protected here is stored in the shared database and must be readable by every host (MVC, Web API, tools),
    /// so every host must build the provider with <see cref="ApplicationName"/> and the same persisted key ring.
    /// </remarks>
    public class DataProtectionProtectedData : IProtectedData
    {
        /// <summary>
        /// The Data Protection application name every host must use for the key ring behind this class.
        /// </summary>
        public const string ApplicationName = "AxCrypt.ProtectedData";

        private const string Purpose = "AxCrypt.IProtectedData";

        private static readonly byte[] _axCryptGuid = AxCrypt1Guid.GetBytes();

        // A Windows DPAPI blob starts with dwVersion = 1 followed by the provider GUID df9d8cd0-1501-11d1-8c7a-00c04fc297eb.
        private static readonly byte[] _dpapiBlobHeader =
        {
            0x01, 0x00, 0x00, 0x00,
            0xD0, 0x8C, 0x9D, 0xDF, 0x01, 0x15, 0xD1, 0x11, 0x8C, 0x7A, 0x00, 0xC0, 0x4F, 0xC2, 0x97, 0xEB,
        };

        private readonly IDataProtector _protector;

        public DataProtectionProtectedData(IDataProtectionProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }
            _protector = provider.CreateProtector(Purpose);
        }

        /// <summary>
        /// True if the bytes are a Windows DPAPI blob, which cannot be decrypted off the Windows machine that made it.
        /// </summary>
        public static bool IsWindowsDpapiBlob(byte[] data)
        {
            if (data == null || data.Length < _dpapiBlobHeader.Length)
            {
                return false;
            }
            for (int i = 0; i < _dpapiBlobHeader.Length; ++i)
            {
                if (data[i] != _dpapiBlobHeader[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// True if the base64 text decodes to a Windows DPAPI blob.
        /// </summary>
        public static bool IsWindowsDpapiBlob(string base64)
        {
            if (string.IsNullOrEmpty(base64))
            {
                return false;
            }
            try
            {
                return IsWindowsDpapiBlob(Convert.FromBase64String(base64));
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public byte[] Protect(byte[] userData, byte[] optionalEntropy = null)
        {
            if (userData == null)
            {
                throw new ArgumentNullException(nameof(userData));
            }
            return ProtectorFor(optionalEntropy).Protect(userData);
        }

        public byte[] Unprotect(byte[] encryptedData, byte[] optionalEntropy = null)
        {
            if (encryptedData == null)
            {
                throw new ArgumentNullException(nameof(encryptedData));
            }

            // AxCrypt-encrypted data is never machine-protected; callers rely on null to fall back to passphrase decryption.
            if (encryptedData.Locate(_axCryptGuid, 0, _axCryptGuid.Length) == 0)
            {
                return null;
            }

            try
            {
                return ProtectorFor(optionalEntropy).Unprotect(encryptedData);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        private IDataProtector ProtectorFor(byte[] optionalEntropy)
        {
            // Data Protection has no entropy parameter; a sub-purpose gives the same isolation.
            return optionalEntropy == null || optionalEntropy.Length == 0
                ? _protector
                : _protector.CreateProtector(Convert.ToBase64String(optionalEntropy));
        }
    }
}
