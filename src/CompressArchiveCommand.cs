// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Management.Automation;
using System.Runtime.InteropServices;

using Microsoft.PowerShell.Archive.Localized;

namespace Microsoft.PowerShell.Archive
{
    [Cmdlet("Compress", "Archive", SupportsShouldProcess = true)]
    [OutputType(typeof(FileInfo))]
    public sealed class CompressArchiveCommand : PSCmdlet {
        // TODO: Add tar support

        private enum ParameterSet
        {
            Path,
            LiteralPath
        }

        /// <summary>
        /// The Path parameter - specifies paths of files or directories from the filesystem to add to or update in the archive.
        /// This parameter does expand wildcard characters.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = nameof(ParameterSet.Path), ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string[]? Path { get; set; }

        /// <summary>
        /// The LiteralPath parameter - specifies paths of files or directories from the filesystem to add to or update in the archive.
        /// This parameter does not expand wildcard characters.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = nameof(ParameterSet.LiteralPath), ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("PSPath", "LP")]
        public string[]? LiteralPath { get; set; }

        /// <summary>
        /// The DestinationPath parameter - specifies the location of the archive in the filesystem.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        [NotNull]
        public string? DestinationPath { get; set; }

        [Parameter()]
        public WriteMode WriteMode { get; set; }

        [Parameter()]
        public SwitchParameter PassThru { get; set; }

        [Parameter()]
        [ValidateNotNullOrEmpty]
        public CompressionLevel CompressionLevel { get; set; }

        [Parameter()]
        public ArchiveFormat? Format { get; set; } = null;

        private readonly PathHelper _pathHelper;

        private bool _didCreateNewArchive;

        // Stores paths
        private HashSet<string>? _paths;

        // This is used so the cmdlet can show all nonexistent paths at once to the user
        private HashSet<string> _nonexistentPaths;

        // Keeps track of duplicate paths so the cmdlet can show them all at once to the user
        private HashSet<string> _duplicatePaths;

        // Keeps track of whether any source path is equal to the destination path
        // Since we are already checking for duplicates, only a bool is necessary and not a List or a HashSet
        // Only 1 path could be equal to the destination path after filtering for duplicates
        private bool _isSourcePathEqualToDestinationPath;

        public CompressArchiveCommand()
        {
            _pathHelper = new PathHelper(this);
            Messages.Culture = new System.Globalization.CultureInfo("en-US");
            _didCreateNewArchive = false;
            _paths = new HashSet<string>( RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            _nonexistentPaths = new HashSet<string>( RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            _duplicatePaths = new HashSet<string>( RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        }

        protected override void BeginProcessing()
        {
            // This resolves the path to a fully qualified path and handles provider exceptions
            DestinationPath = _pathHelper.GetUnresolvedPathFromPSProviderPath(path: DestinationPath, pathMustExist: false);
            ValidateDestinationPath();
        }

        protected override void ProcessRecord()
        {
            if (ParameterSetName == nameof(ParameterSet.Path))
            {
                Debug.Assert(Path is not null);
                foreach (var path in Path) {
                    var resolvedPaths = _pathHelper.GetResolvedPathFromPSProviderPathWhileCapturingNonexistentPaths(path, _nonexistentPaths);
                    if (resolvedPaths is not null) {
                        foreach (var resolvedPath in resolvedPaths) {
                            // Add resolvedPath to _path
                            AddPathToPaths(pathToAdd: resolvedPath);
                        }
                    }
                }
                
            }
            else
            {
                Debug.Assert(LiteralPath is not null);
                foreach (var path in LiteralPath) {
                    var unresolvedPath = _pathHelper.GetUnresolvedPathFromPSProviderPathWhileCapturingNonexistentPaths(path, _nonexistentPaths);
                    if (unresolvedPath is not null) {
                        // Add unresolvedPath to _path
                        AddPathToPaths(pathToAdd: unresolvedPath);
                    }
                }
            }
        }

        protected override void EndProcessing()
        {
            // If there are non-existent paths, throw a terminating error
            if (_nonexistentPaths.Count > 0) {
                // Get a comma-seperated string containg the non-existent paths
                string commaSeperatedNonExistentPaths = string.Join(',', _nonexistentPaths);
                var errorRecord = ErrorMessages.GetErrorRecord(ErrorCode.PathNotFound, commaSeperatedNonExistentPaths);
                ThrowTerminatingError(errorRecord);
            }

            // If there are duplicate paths, throw a terminating error
            if (_duplicatePaths.Count > 0) {
                 // Get a comma-seperated string containg the non-existent paths
                string commaSeperatedDuplicatePaths = string.Join(',', _nonexistentPaths);
                var errorRecord = ErrorMessages.GetErrorRecord(ErrorCode.DuplicatePaths, commaSeperatedDuplicatePaths);
                ThrowTerminatingError(errorRecord);
            }

            // If a source path is the same as the destination path, throw a terminating error
            // We don't want to overwrite the file or directory that we want to add to the archive.
            if (_isSourcePathEqualToDestinationPath) {
                var errorCode = ParameterSetName == nameof(ParameterSet.Path) ? ErrorCode.SamePathAndDestinationPath : ErrorCode.SameLiteralPathAndDestinationPath;
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode);
                ThrowTerminatingError(errorRecord);
            }

            // Get archive entries
            // If a path causes an exception (e.g., SecurityException), _pathHelper should handle it
            Debug.Assert(_paths is not null);
            List<ArchiveAddition> archiveAdditions = _pathHelper.GetArchiveAdditions(_paths);

            // Remove references to _paths, Path, and LiteralPath to free up memory
            // The user could have supplied a lot of paths, so we should do this
            Path = null;
            LiteralPath = null;
            _paths = null;

            // Warn the user if there are no items to add for some reason (e.g., no items matched the filter)
            if (archiveAdditions.Count == 0)
            {
                WriteWarning(Messages.NoItemsToAddWarning);
            }

            // Get the ArchiveMode for the archive to be created or updated
            ArchiveMode archiveMode = WriteMode == WriteMode.Update ? ArchiveMode.Update : ArchiveMode.Create;

            // Don't create the archive object yet because the user could have specified -WhatIf or -Confirm
            IArchive? archive = null;
            try
            {
                if (ShouldProcess(target: DestinationPath, action: Messages.Create))
                {
                    // If the WriteMode is overwrite, delete the existing archive
                    if (WriteMode == WriteMode.Overwrite)
                    {
                        DeleteDestinationPathIfExists();
                    }

                    // Create an archive -- this is where we will switch between different types of archives
                    archive = ArchiveFactory.GetArchive(format: Format ?? ArchiveFormat.Zip, archivePath: DestinationPath, archiveMode: archiveMode, compressionLevel: CompressionLevel);
                    _didCreateNewArchive = archiveMode != ArchiveMode.Update;
                }
                
                long numberOfAdditions = archiveAdditions.Count;
                long numberOfAddedItems = 0;
                // Messages.ProgressDisplay does not need to be formatted here because progressRecord.StautsDescription will be updated in the for-loop
                var progressRecord = new ProgressRecord(activityId: 1, activity: "Compress-Archive", statusDescription: Messages.ProgressDisplay);

                foreach (ArchiveAddition entry in archiveAdditions)
                {
                    // Update progress
                    var percentComplete = numberOfAddedItems / (float)numberOfAdditions * 100f;

                    progressRecord.StatusDescription = string.Format(Messages.ProgressDisplay, $"{percentComplete:0.0}");
                    progressRecord.PercentComplete = (int)percentComplete;
                    WriteProgress(progressRecord);

                    if (ShouldProcess(target: entry.FileSystemInfo.FullName, action: Messages.Add))
                    {
                        // Warn the user if the LastWriteTime of the file/directory is before 1980
                        if (entry.FileSystemInfo.LastWriteTime.Year < 1980 && Format == ArchiveFormat.Zip) {
                            WriteWarning(string.Format(Messages.LastWriteTimeBefore1980Warning, entry.FileSystemInfo.FullName));
                        }

                        archive?.AddFileSystemEntry(entry);
                        // Write a verbose message saying this item was added to the archive
                        var addedItemMessage = string.Format(Messages.AddedItemToArchiveVerboseMessage, entry.FileSystemInfo.FullName);
                        WriteVerbose(addedItemMessage);
                    }
                    // Keep track of number of items added to the archive
                    numberOfAddedItems++;
                }

                // Once all items in the archive are processed, show progress as 100%
                // This code is here and not in the loop because we want it to run even if there are no items to add to the archive
                progressRecord.StatusDescription = string.Format(Messages.ProgressDisplay, "100.0");
                progressRecord.PercentComplete = 100;
                WriteProgress(progressRecord);
            }
            finally
            {
                archive?.Dispose();
            }

            // If -PassThru is specified, write a System.IO.FileInfo object
            if (PassThru)
            {
                WriteObject(new FileInfo(DestinationPath));
            }
        }

        protected override void StopProcessing()
        {
            // If a new output archive was created, delete it (this does not delete an archive if -WriteMode Update is specified)
            if (_didCreateNewArchive)
            {
                DeleteDestinationPathIfExists();
            }
        }

        /// <summary>
        /// Validate DestinationPath parameter and determine the archive format based on the extension of DestinationPath
        /// </summary>
        private void ValidateDestinationPath()
        {
            ErrorCode? errorCode = null;

            if (System.IO.Path.Exists(DestinationPath))
            {
                // Check if DestinationPath is an existing directory
                if (Directory.Exists(DestinationPath))
                {
                    // Throw an error if DestinationPath exists and the cmdlet is not in Update mode or Overwrite is not specified 
                    if (WriteMode == WriteMode.Create)
                    {
                        errorCode = ErrorCode.ArchiveExistsAsDirectory;
                    }
                    // Throw an error if the DestinationPath is a directory and the cmdlet is in Update mode
                    else if (WriteMode == WriteMode.Update)
                    {
                        errorCode = ErrorCode.ArchiveExistsAsDirectory;
                    }
                    // Throw an error if the DestinationPath is the current working directory and the cmdlet is in Overwrite mode
                    else if (WriteMode == WriteMode.Overwrite && DestinationPath == SessionState.Path.CurrentFileSystemLocation.ProviderPath)
                    {
                        errorCode = ErrorCode.CannotOverwriteWorkingDirectory;
                    }
                    // Throw an error if the DestinationPath is a directory with at 1 least item and the cmdlet is in Overwrite mode
                    else if (WriteMode == WriteMode.Overwrite && Directory.GetFileSystemEntries(DestinationPath).Length > 0)
                    {
                        errorCode = ErrorCode.ArchiveIsNonEmptyDirectory;
                    }
                }
                // If DestinationPath is an existing file
                else
                {
                    // Throw an error if DestinationPath exists and the cmdlet is not in Update mode or Overwrite is not specified 
                    if (WriteMode == WriteMode.Create)
                    {
                        errorCode = ErrorCode.ArchiveExists;
                    }
                    // Throw an error if the cmdlet is in Update mode but the archive is read only
                    else if (WriteMode == WriteMode.Update && File.GetAttributes(DestinationPath).HasFlag(FileAttributes.ReadOnly))
                    {
                        errorCode = ErrorCode.ArchiveReadOnly;
                    }
                }
            }
            // Throw an error if DestinationPath does not exist and cmdlet is in Update mode
            else if (WriteMode == WriteMode.Update)
            {
                errorCode = ErrorCode.ArchiveDoesNotExist;
            }

            if (errorCode is not null)
            {
                // Throw an error -- since we are validating DestinationPath, the problem is with DestinationPath
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: errorCode.Value, errorItem: DestinationPath);
                ThrowTerminatingError(errorRecord);
            }

            // Determine archive format based on the extension of DestinationPath
            DetermineArchiveFormat();
        }

        private void DeleteDestinationPathIfExists()
        {
            try
            {
                // No need to ensure DestinationPath has no children when deleting it
                // because ValidateDestinationPath should have already done this
                if (File.Exists(DestinationPath))
                {
                    File.Delete(DestinationPath);
                } 
                else if (Directory.Exists(DestinationPath))
                {
                    Directory.Delete(DestinationPath);
                }
            }
            // Throw a terminating error if an IOException occurs
            catch (IOException ioException)
            {
                var errorRecord = new ErrorRecord(ioException, errorId: nameof(ErrorCode.OverwriteDestinationPathFailed), 
                    errorCategory: ErrorCategory.InvalidOperation, targetObject: DestinationPath);
                ThrowTerminatingError(errorRecord);
            }
            // Throw a terminating error if an UnauthorizedAccessException occurs
            catch (System.UnauthorizedAccessException unauthorizedAccessException)
            {
                var errorRecord = new ErrorRecord(unauthorizedAccessException, errorId: nameof(ErrorCode.InsufficientPermissionsToAccessPath),
                    errorCategory: ErrorCategory.PermissionDenied, targetObject: DestinationPath);
                ThrowTerminatingError(errorRecord);
            }
        }

        private void DetermineArchiveFormat()
        {
            // Check if cmdlet is able to determine the format of the archive based on the extension of DestinationPath
            bool ableToDetermineArchiveFormat = ArchiveFactory.TryGetArchiveFormatFromExtension(path: DestinationPath, archiveFormat: out var archiveFormat);
            // If the user did not specify which archive format to use, try to determine it automatically
            if (Format is null)
            {
                if (ableToDetermineArchiveFormat)
                {
                    Format = archiveFormat;
                }
                else
                {
                    // If the archive format could not be determined, use zip by default and emit a warning
                    var warningMsg = string.Format(Messages.ArchiveFormatCouldNotBeDeterminedWarning, DestinationPath);
                    WriteWarning(warningMsg);
                    Format = ArchiveFormat.Zip;
                }
                // Write a verbose message saying that Format is not specified and a format was determined automatically
                string verboseMessage = string.Format(Messages.ArchiveFormatDeterminedVerboseMessage, Format);
                WriteVerbose(verboseMessage);
            }
            // If the user did specify which archive format to use, emit a warning if DestinationPath does not match the chosen archive format
            else
            {
                if (archiveFormat is null || archiveFormat.Value != Format.Value)
                {
                    var warningMsg = string.Format(Messages.ArchiveExtensionDoesNotMatchArchiveFormatWarning, DestinationPath);
                    WriteWarning(warningMsg);
                }
            }
        }

        // Adds a path to _paths variable
        // If the path being added is a duplicate, it adds it _duplicatePaths (if it is not already there)
        // If the path is the same as the destination path, it sets _isSourcePathEqualToDestinationPath to true
        private void AddPathToPaths(string pathToAdd) {
            if (!_paths.Add(pathToAdd)) {
                _duplicatePaths.Add(pathToAdd);
            } else if (!_isSourcePathEqualToDestinationPath && pathToAdd == DestinationPath) {
                _isSourcePathEqualToDestinationPath = true;
            }  
        }
    }
}
