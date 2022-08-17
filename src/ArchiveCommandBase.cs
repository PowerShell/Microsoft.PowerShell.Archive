using Microsoft.PowerShell.Archive.Localized;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.Archive
{
    /// <summary>
    /// This class is meant to be a base class for all cmdlets in the archive module
    /// </summary>
    public class ArchiveCommandBase : PSCmdlet
    {
        protected ArchiveFormat DetermineArchiveFormat(string destinationPath, ArchiveFormat? archiveFormat)
        {
            // Check if cmdlet is able to determine the format of the archive based on the extension of DestinationPath
            bool ableToDetermineArchiveFormat = ArchiveFactory.TryGetArchiveFormatFromExtension(path: destinationPath, archiveFormat: out var archiveFormatBasedOnExt);
            // If the user did not specify which archive format to use, try to determine it automatically
            if (archiveFormat is null)
            {
                if (ableToDetermineArchiveFormat)
                {
                    archiveFormat = archiveFormatBasedOnExt;
                }
                else
                {
                    // If the archive format could not be determined, use zip by default and emit a warning
                    var warningMsg = String.Format(Messages.ArchiveFormatCouldNotBeDeterminedWarning, destinationPath);
                    WriteWarning(warningMsg);
                    archiveFormat = ArchiveFormat.Zip;
                }
                // Write a verbose message saying that Format is not specified and a format was determined automatically
                string verboseMessage = String.Format(Messages.ArchiveFormatDeterminedVerboseMessage, archiveFormat);
                WriteVerbose(verboseMessage);
            }
            // If the user did specify which archive format to use, emit a warning if DestinationPath does not match the chosen archive format
            else
            {
                if (archiveFormat is null || archiveFormat.Value != archiveFormat.Value)
                {
                    var warningMsg = String.Format(Messages.ArchiveExtensionDoesNotMatchArchiveFormatWarning, destinationPath);
                    WriteWarning(warningMsg);
                }
            }

            // archiveFormat is never null at this point
            return archiveFormat.Value;
        }
    }
}
