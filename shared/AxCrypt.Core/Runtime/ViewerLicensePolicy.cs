using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AxCrypt.Core.Runtime
{
    public class ViewerLicensePolicy : LicensePolicy
    {
        protected override LicenseCapabilities FreeCapabilities
        {
            get
            {
                return new LicenseCapabilities(ViewerCapabilitySet);
            }
        }
    }
}