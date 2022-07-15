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

        internal static string PathResolvesToMultiplePathsMessage = "The path {0} resolves to multiple possible paths.";

        internal static string ArchiveExistsMessage = "The destination path {0} already exists";

        internal static string ArchiveExistsAsDirectoryMessage = "The destination path {0} is a directory";

        internal static string ArchiveIsReadOnlyMessage = "The archive at {0} is read-only.";

        internal static ErrorRecord GetErrorRecordForArgumentException(ErrorCode errorCode, string errorItem)
        {
            var errorMsg = String.Format(GetErrorMessage(errorCode: errorCode), errorItem);
            var exception = new ArgumentException(errorMsg);
            return new ErrorRecord(exception, errorCode.ToString(), ErrorCategory.InvalidArgument, errorItem);
        }

        internal static string GetErrorMessage(ErrorCode errorCode)
        {
            return errorCode switch
            {
                ErrorCode.PathNotFound => PathNotFoundMessage,
                ErrorCode.InvalidPath => InvalidPathMessage,
                ErrorCode.DuplicatePaths => DuplicatePathsMessage,
                ErrorCode.ArchiveExists => ArchiveExistsMessage,
                ErrorCode.ArchiveExistsAsDirectory => ArchiveExistsAsDirectoryMessage,
                ErrorCode.ArchiveReadOnly => ArchiveIsReadOnlyMessage,
                ErrorCode.PathResolvesToMultiplePaths => PathResolvesToMultiplePathsMessage,
                _ => throw new NotImplementedException("Error code has not been implemented")
            };
        }
    }

    internal enum ErrorCode
    {
        PathNotFound,
        InvalidPath,
        DuplicatePaths,
        ArchiveExists,
        ArchiveExistsAsDirectory,
        ArchiveReadOnly,
        PathResolvesToMultiplePaths,
        ArchiveDoesNotExist
    }
}
