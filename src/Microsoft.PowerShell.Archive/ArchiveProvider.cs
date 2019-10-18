using System;
using System.Management.Automation;
using System.Management.Automation.Provider;

namespace Microsoft.PowerShell.Archive
{
    #region ArchiveProvider
    public class ArchiveProvider :  NavigationCmdletProvider
    {
        protected override bool IsValidPath(string path)
        {
            return false;
        }
        
    }
    #endregion ArchiveProvider
}
