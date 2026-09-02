using System;
using System.Collections.Generic;
using System.Text;
using AxCrypt.Core.UI;
using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Core.Runtime
{
    public class VirtualFileWorkFolder : FileWorkFolder
    {
        /// <summary>
        /// Decide where temporary (decrypted) files are written.
        ///
        /// The in-memory virtual drive wins when the user has it enabled AND it is
        /// actually mounted — plaintext then lives in RAM and never reaches physical
        /// storage. Otherwise we fall back to the configured temporary file path.
        ///
        /// The user setting is checked FIRST, deliberately: platforms that do not
        /// register IInMemoryFileSystem (macOS, mobile) throw on resolve, and turning
        /// the drive off should not depend on catching that.
        /// </summary>
        private static string resolvePath(string fallbackPath)
        {
            if (!New<UserSettings>().UseVirtualDriveForTemporaryFiles)
            {
                return fallbackPath;
            }

            try
            {
                string storage = New<IInMemoryFileSystem>().Storage;

                // An unmounted drive reports empty storage. Returning it would have
                // produced a bare "\", silently pointing the work folder at the root
                // of the current drive instead of the configured fallback.
                return string.IsNullOrEmpty(storage) ? fallbackPath : storage + "\\";
            }
            catch
            {
                return fallbackPath;
            }
        }

        public VirtualFileWorkFolder(string path) : base(resolvePath(path))
        {
        }
    }
}
