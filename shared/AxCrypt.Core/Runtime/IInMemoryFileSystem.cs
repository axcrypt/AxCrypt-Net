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
    }
}
