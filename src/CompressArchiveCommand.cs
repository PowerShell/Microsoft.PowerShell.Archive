using Microsoft.PowerShell.Archive.Localized;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace Microsoft.PowerShell.Archive
{
    [Cmdlet("Compress", "Archive", SupportsShouldProcess = true)]
    [OutputType(typeof(System.IO.FileInfo))]
    public class CompressArchiveCommand : PSCmdlet
    {

        // TODO: Add filter parameter
        // TODO: Add format parameter
        // TODO: Add flatten parameter
        // TODO: Add comments to methods

        // TODO: Add warnings for archive file extension
        // TODO: Add tar support

        // TODO: Add comments to ArchiveEntry and for adding filesystem entry to zip

        // TODO: Add error messages for each error code

        /// <summary>
        /// The Path parameter - specifies paths of files or directories from the filesystem to add to or update in the archive.
        /// This parameter does expand wildcard characters.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Path", ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string[]? Path { get; set; }

        /// <summary>
        /// The LiteralPath parameter - specifies paths of files or directories from the filesystem to add to or update in the archive.
        /// This parameter does not expand wildcard characters.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "LiteralPath", ValueFromPipeline = false, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("PSPath")]
        public string[]? LiteralPath { get; set; }

        /// <summary>
        /// The DestinationPath parameter - specifies the location of the archive in the filesystem.
        /// </summary>
        [Parameter(Mandatory = true, Position = 2, ValueFromPipeline = false, ValueFromPipelineByPropertyName = false)]
        [ValidateNotNullOrEmpty]
        [NotNull]
        public string? DestinationPath { get; set; }

        [Parameter()]
        public WriteMode WriteMode { get; set; } = WriteMode.Create;

        [Parameter()]
        public SwitchParameter PassThru { get; set; } = false;

        [Parameter()]
        [ValidateNotNullOrEmpty]
        public System.IO.Compression.CompressionLevel CompressionLevel { get; set; } = System.IO.Compression.CompressionLevel.Optimal;

        [Parameter()]
        public ArchiveFormat? Format { get; set; } = null;

        private List<string>? _sourcePaths;

        private PathHelper _pathHelper;

        public CompressArchiveCommand()
        {
            _sourcePaths = new List<string>();
            _pathHelper = new PathHelper(this);
            Messages.Culture = new System.Globalization.CultureInfo("en-US");
        }

        protected override void BeginProcessing()
        {
            DestinationPath = _pathHelper.ResolveToSingleFullyQualifiedPath(DestinationPath);

            // Validate DestinationPath
            ValidateDestinationPath();

            // Check if cmdlet is able to determine the format of the archive based on the extension of DestinationPath
            bool ableToDetermineArchiveFormat = ArchiveFactory.TryGetArchiveFormatForPath(path: DestinationPath, archiveFormat: out var archiveFormat);
            // If the user did not specify which archive format to use, try to determine it automatically
            if (Format is null)
            {
                if (ableToDetermineArchiveFormat)
                {
                    Format = archiveFormat;
                } else
                {
                    // If the archive format could not be determined, use zip by default and emit a warning
                    var warningMsg = String.Format(Messages.ArchiveFormatCouldNotBeDeterminedWarning, DestinationPath);
                    WriteWarning(warningMsg);
                    Format = ArchiveFormat.zip;
                }
                // Write a verbose message saying that Format is not specified and a format was determined automatically
                string verboseMessage = String.Format(Messages.ArchiveFormatDeterminedVerboseMessage, Format);
                WriteVerbose(verboseMessage);
            }
            // If the user did specify which archive format to use, emit a warning if DestinationPath does not match the chosen archive format
            else
            {
                if (archiveFormat is null || archiveFormat.Value != Format.Value)
                {
                    var warningMsg = String.Format(Messages.ArchiveExtensionDoesNotMatchArchiveFormatWarning, DestinationPath);
                    WriteWarning(warningMsg);
                }
            }

        }

        protected override void ProcessRecord()
        {
            // Add each path from -Path or -LiteralPath to _sourcePaths because they can get lost when the next item in the pipeline is sent
            if (ParameterSetName.StartsWith("Path"))
            {
                _sourcePaths?.AddRange(Path);
            }
            else
            {
                _sourcePaths?.AddRange(LiteralPath);
            }
        }

        protected override void EndProcessing()
        {
            // Get archive entries, validation is performed by PathHelper
            // _sourcePaths should not be null at this stage, but if it is, prevent a NullReferenceException by doing the following
            List<ArchiveAddition> archiveAddtions = _sourcePaths != null ? _pathHelper.GetArchiveAdditionsForPath(_sourcePaths.ToArray(), ParameterSetName.StartsWith("LiteralPath")) : new List<ArchiveAddition>();

            // Remove references to _sourcePaths, Path, and LiteralPath to free up memory
            // The user could have supplied a lot of paths, so we should do this
            Path = null;
            LiteralPath = null;
            _sourcePaths = null;

            // Throw a terminating error if there is a source path as same as DestinationPath.
            // We don't want to overwrite the file or directory that we want to add to the archive.
            var additionsWithSamePathAsDestination = archiveAddtions.Where(addition => addition.FullPath == DestinationPath).ToList();
            if (additionsWithSamePathAsDestination.Count() > 0)
            {
                // Since duplicate checking is performed earlier, there must a single ArchiveAddition such that ArchiveAddition.FullPath == DestinationPath
                var errorCode = ParameterSetName.StartsWith("Path") ? ErrorCode.SamePathAndDestinationPath : ErrorCode.SameLiteralPathAndDestinationPath;
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode, errorItem: additionsWithSamePathAsDestination[0].FullPath);
                ThrowTerminatingError(errorRecord);
            }

            // Warn the user if there are no items to add for some reason (e.g., no items matched the filter)
            if (archiveAddtions.Count == 0)
            {
                WriteWarning(Messages.NoItemsToAddWarning);
            }

            // Get the ArchiveMode for the archive to be created or updated
            ArchiveMode archiveMode = ArchiveMode.Create;
            if (WriteMode == WriteMode.Update)
            {
                archiveMode = ArchiveMode.Update;
            }

            // Don't create the archive object yet because the user could have specified -WhatIf or -Confirm
            IArchive? archive = null;
            try
            {
                if (ShouldProcess(target: DestinationPath, action: "Create"))
                {
                    // If the WriteMode is overwrite, delete the existing archive
                    if (WriteMode == WriteMode.Overwrite)
                    {
                        DeleteDestinationPathIfExists();
                    }

                    // Create an archive -- this is where we will switch between different types of archives
                    archive = ArchiveFactory.GetArchive(format: Format ?? ArchiveFormat.zip, archivePath: DestinationPath, archiveMode: archiveMode, compressionLevel: CompressionLevel);
                }

                // TODO: Update progress
                foreach (ArchiveAddition entry in archiveAddtions)
                {
                    if (ShouldProcess(target: entry.FullPath, action: "Add"))
                    {
                        archive?.AddFilesytemEntry(entry);
                    }
                }
            }
            finally
            {
                archive?.Dispose();
            }
        }

        protected override void StopProcessing()
        {
            base.StopProcessing();
        }

        /// <summary>
        /// Validate DestinationPath parameter
        /// </summary>
        private void ValidateDestinationPath()
        {
            // TODO: Add tests cases for conditions below
            ErrorCode? errorCode = null;

            var archiveAsFile = new System.IO.FileInfo(DestinationPath);
            var archiveAsDirectory = new System.IO.DirectoryInfo(DestinationPath);

            // Check if DestinationPath is an existing file
            if (archiveAsFile.Exists)
            {
                // Throw an error if DestinationPath exists and the cmdlet is not in Update mode or Overwrite is not specified 
                if (WriteMode == WriteMode.Create)
                {
                    errorCode = ErrorCode.ArchiveExists;
                }
                // Throw an error if the cmdlet is in Update mode but the archive is read only
                if (WriteMode == WriteMode.Update && archiveAsFile.Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    errorCode = ErrorCode.ArchiveReadOnly;
                }
            } 
            // Check if DestinationPath is an existing directory
            else if (archiveAsDirectory.Exists)
            {
                // Throw an error if DestinationPath exists and the cmdlet is not in Update mode or Overwrite is not specified 
                if (WriteMode == WriteMode.Create)
                {
                    errorCode = ErrorCode.ArchiveExistsAsDirectory;
                }
                // Throw an error if the DestinationPath is a directory and the cmdlet is in Update mode
                if (WriteMode == WriteMode.Update)
                {
                    errorCode = ErrorCode.ArchiveExistsAsDirectory;
                }
                // Throw an error if the DestinationPath is a directory with at least item and the cmdlet is in Overwrite mode
                if (WriteMode == WriteMode.Overwrite && archiveAsDirectory.GetFileSystemInfos().Length > 0)
                {
                    errorCode = ErrorCode.ArchiveIsNonEmptyDirectory;
                }
            } 
            // In this case, DestinationPath does not exist
            else
            {
                // Throw an error if DestinationPath does not exist and cmdlet is in Update mode
                if (WriteMode == WriteMode.Update)
                {
                    errorCode = ErrorCode.ArchiveDoesNotExist;
                }
            }

            if (errorCode != null)
            {
                // Throw an error -- since we are validating DestinationPath, the problem is with DestinationPath
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: errorCode.Value, errorItem: DestinationPath);
                ThrowTerminatingError(errorRecord);
            }
        }

        private void DeleteDestinationPathIfExists()
        {
            try
            {
                if (System.IO.File.Exists(DestinationPath))
                {
                    System.IO.File.Delete(DestinationPath);
                }
                if (System.IO.Directory.Exists(DestinationPath))
                {
                    System.IO.Directory.Delete(DestinationPath);
                }
            }
            // Throw a terminating error if an IOException occurs
            catch (System.IO.IOException ioException)
            {
                var errorRecord = new ErrorRecord(ioException, errorId: ErrorCode.OverwriteDestinationPathFailed.ToString(), 
                    errorCategory: ErrorCategory.InvalidOperation, targetObject: DestinationPath);
                ThrowTerminatingError(errorRecord);
            }
            // Throw a terminating error if an UnauthorizedAccessException occurs
            catch (System.UnauthorizedAccessException unauthorizedAccessException)
            {
                var errorRecord = new ErrorRecord(unauthorizedAccessException, errorId: ErrorCode.InsufficientPermissionsToAccessPath.ToString(),
                    errorCategory: ErrorCategory.PermissionDenied, targetObject: DestinationPath);
                ThrowTerminatingError(errorRecord);
            }
        }
    }
}
