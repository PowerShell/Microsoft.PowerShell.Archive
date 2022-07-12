using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;

namespace Microsoft.PowerShell.Archive
{
    internal static class ErrorMessages
    {
        internal static string PathNotFoundMessage = "The path {0} could not be found";

        internal static string DuplicatePathsMessage = "The path(s) {0} have been specified more than once.";

        internal static string InvalidPathMessage = "The path(s) {0} are invalid.";
    }
}
