using System;
using System.Collections.Generic;
using System.Text;
using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Core.Runtime
{
    public class VirtualFileWorkFolder : FileWorkFolder
    {

        private static string resolvePath(string fallbackPath)
        {
            string retPath = fallbackPath;
            try
            {
                retPath = New<IInMemoryFileSystem>().Storage + "\\";
            }
            catch { }
            return retPath;
        }
        public VirtualFileWorkFolder(string path) : base(resolvePath(path))
        {

        }
    }
}
