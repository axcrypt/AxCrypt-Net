using System;
using System.Collections.Generic;
using System.Text;

namespace AxCrypt.Core.Runtime
{
    public interface IInMemoryFileSystem
    {
        void Mount();
        void Unmount();

        string Storage { get; }

        /// <summary>
        /// True when the platform driver this implementation needs is actually
        /// present on the machine. Check before calling <see cref="Mount"/> —
        /// mounting without the driver throws.
        /// </summary>
        bool IsInstalled { get; }
    }
}
