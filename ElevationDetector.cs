using System;
using System.Security.Principal;

namespace PureNote
{
    public static class ElevationDetector
    {
        public static string Detect()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    if (identity.IsSystem) return "System";

                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    if (principal.IsInRole(WindowsBuiltInRole.Administrator)) return "Administrator";

                    return "Standard";
                }
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }
    }
}
