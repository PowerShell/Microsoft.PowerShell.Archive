// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.Archive.Localized;
using System;
using System.Management.Automation;

namespace Microsoft.PowerShell.Archive
{
    internal static class ErrorMessages
    {
        internal static ErrorRecord GetErrorRecord(ErrorCode errorCode, string errorItem)
        {
            var errorMsg = string.Format(GetErrorMessage(errorCode: errorCode), errorItem);
            var exception = new ArgumentException(errorMsg);
            return new ErrorRecord(exception, errorCode.ToString(), ErrorCategory.InvalidArgument, errorItem);
        }

        internal static ErrorRecord GetErrorRecord(ErrorCode errorCode)
        {
            var errorMsg = GetErrorMessage(errorCode: errorCode);
            var exception = new ArgumentException(errorMsg);
            return new ErrorRecord(exception, errorCode.ToString(), ErrorCategory.InvalidArgument, null);
        }

        internal static string GetErrorMessage(ErrorCode errorCode)
        {
            return errorCode switch
            {
                ErrorCode.PathNotFound => Messages.PathNotFoundMessage,
                ErrorCode.InvalidPath => Messages.InvalidPathMessage,
                ErrorCode.DuplicatePaths => Messages.DuplicatePathsMessage,
                ErrorCode.DestinationExists => Messages.DestinationExistsMessage,
                ErrorCode.DestinationExistsAsDirectory => Messages.DestinationExistsAsDirectoryMessage,
                ErrorCode.ArchiveReadOnly => Messages.ArchiveIsReadOnlyMessage,
                ErrorCode.ArchiveDoesNotExist => Messages.ArchiveDoesNotExistMessage,
                ErrorCode.ArchiveIsNonEmptyDirectory => Messages.ArchiveIsNonEmptyDirectory,
                ErrorCode.SamePathAndDestinationPath => Messages.SamePathAndDestinationPathMessage,
                ErrorCode.SameLiteralPathAndDestinationPath => Messages.SameLiteralPathAndDestinationPathMessage,
                ErrorCode.InsufficientPermissionsToAccessPath => Messages.InsufficientPermssionsToAccessPathMessage,
                ErrorCode.OverwriteDestinationPathFailed => Messages.OverwriteDestinationPathFailed,
                ErrorCode.CannotOverwriteWorkingDirectory => Messages.CannotOverwriteWorkingDirectoryMessage,
                _ => throw new ArgumentOutOfRangeException(nameof(errorCode))
            };
        }
    }

    internal enum ErrorCode
    {
        // Used when a path does not resolve to a file or directory on the filesystem
        PathNotFound,
        // Used when a path is invalid (e.g., if the path is for a non-filesystem provider)
        InvalidPath,
        // Used when when a path has been supplied to the cmdlet at least twice
        DuplicatePaths,
        // Used when DestinationPath is an existing file (used in Compress-Archive & Expand-Archive)
        DestinationExists,
        // Used when DestinationPath is an existing directory
        DestinationExistsAsDirectory,
        // Used when DestinationPath is a non-empty directory and Action Overwrite is specified
        ArchiveIsNonEmptyDirectory,
        // Used when Compress-Archive cmdlet is in Update mode but the archive is read-only
        ArchiveReadOnly,
        // Used when DestinationPath does not exist and the Compress-Archive cmdlet is in Update mode
        ArchiveDoesNotExist,
        // Used when Path and DestinationPath are the same
        SamePathAndDestinationPath,
        // Used when LiteralPath and DestinationPath are the same
        SameLiteralPathAndDestinationPath,
        // Used when the user does not have sufficient permissions to access a path
        InsufficientPermissionsToAccessPath,
        // Used when the cmdlet could not overwrite DestinationPath
        OverwriteDestinationPathFailed,
        // Used when the user enters the working directory as DestinationPath and it is an existing folder and -WriteMode Overwrite is specified
        // Used in Compress-Archive, Expand-Archive
        CannotOverwriteWorkingDirectory,
        // Expand-Archive: used when a path resolved to multiple paths when only one was needed
        PathResolvedToMultiplePaths,
    }
}
