using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;

namespace Microsoft.PowerShell.Archive
{
    internal class PathHelper
    {
        private PSCmdlet _cmdlet;

        internal PathHelper(PSCmdlet cmdlet)
        {
            _cmdlet = cmdlet;
        }

        internal List<ArchiveEntry>? GetEntryRecordsForPath(string path, bool hasWildcards)
        {
            var resolvedPaths = _cmdlet.InvokeProvider.ChildItem.GetNames(new string[] { path }, ReturnContainers.ReturnAllContainers, true, uint.MaxValue, true, hasWildcards);
            foreach (var entry in resolvedPaths)
            {
                
                _cmdlet.WriteObject(entry);
            }

            return null;
        }

        private List<ArchiveEntry>? GetArchiveEntriesForLiteralPath(string path)
        {
            //Get the unresolved path
            string unresolvedPath = _cmdlet.GetUnresolvedProviderPathFromPSPath(path);

            //Check if it exists
            if (System.IO.Directory.Exists(unresolvedPath))
            {
                //Get all descendents
                var resolvedPaths = _cmdlet.InvokeProvider.ChildItem.GetNames(new string[] { path }, returnContainers: ReturnContainers.ReturnAllContainers, recurse: true, depth: uint.MaxValue, force: true, literalPath: false);
            } else if (!System.IO.File.Exists(unresolvedPath))
            {
                //Throw an error
            }


            //Return archive entries
            return null;
        }
    }
}
