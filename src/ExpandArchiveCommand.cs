using Microsoft.PowerShell.Archive.Localized;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Enumeration;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.Archive
{
    [Cmdlet("Expand", "Archive", SupportsShouldProcess = true)]
    [OutputType(typeof(System.IO.FileSystemInfo))]
    public class ExpandArchiveCommand: ArchiveCommandBase
    {
        private enum ParameterSet {
            Path,
            LiteralPath
        }

        [Parameter(Position=0, Mandatory = true, ParameterSetName = nameof(ParameterSet.Path), ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; } = String.Empty;

        [Parameter(Mandatory = true, ParameterSetName = nameof(ParameterSet.LiteralPath))]
        [ValidateNotNullOrEmpty]
        public string LiteralPath { get; set; } = String.Empty;

        [Parameter(Position = 2)]
        [ValidateNotNullOrEmpty]
        public string? DestinationPath { get; set; }

        [Parameter]
        public ExpandArchiveWriteMode WriteMode { get; set; } = ExpandArchiveWriteMode.Expand;

        [Parameter()]
        public ArchiveFormat? Format { get; set; } = null;

        [Parameter]
        public SwitchParameter PassThru { get; set; }

        #region PrivateMembers

        private PathHelper _pathHelper;

        private System.IO.FileSystemInfo? _destinationPathInfo;

        private bool _didCreateOutput;

        private string? _sourcePath;

        #endregion

        public ExpandArchiveCommand()
        {
            _didCreateOutput = false;
            _pathHelper = new PathHelper(cmdlet: this);
            _destinationPathInfo = null;
        }

        protected override void BeginProcessing()
        {
            
        }

        protected override void ProcessRecord()
        {
            
        }

        protected override void EndProcessing()
        {
            // Resolve Path or LiteralPath
            bool checkForWildcards = ParameterSetName == nameof(ParameterSet.Path);
            string path = checkForWildcards ? Path : LiteralPath;
            ValidateSourcePath(path);
            Debug.Assert(_sourcePath is not null);

            // Determine archive format based on sourcePath
            Format = DetermineArchiveFormat(destinationPath: _sourcePath, archiveFormat: Format);

            try
            {
                // Get an archive from source path -- this is where we will switch between different types of archives
                using IArchive? archive = ArchiveFactory.GetArchive(format: Format ?? ArchiveFormat.Zip, archivePath: _sourcePath, archiveMode: ArchiveMode.Extract, compressionLevel: System.IO.Compression.CompressionLevel.NoCompression);

                if (DestinationPath is null)
                {
                    // If DestinationPath was not specified, try to determine it automatically based on the source path
                    // We should do this here because the destination path depends on whether the archive contains a single top-level directory or not
                    DestinationPath = DetermineDestinationPath(archive);
                } else {
                    // Resolve DestinationPath and validate it
                    DestinationPath = _pathHelper.GetUnresolvedPathFromPSProviderPath(path: DestinationPath, pathMustExist: true);
                }
                ValidateDestinationPath();
                Debug.Assert(DestinationPath is not null);

                // If the destination path is a file that needs to be overwriten, delete it
                if (File.Exists(DestinationPath) && WriteMode == ExpandArchiveWriteMode.Overwrite)
                {
                    if (ShouldProcess(target: DestinationPath, action: "Overwrite"))
                    {
                        File.Delete(DestinationPath);
                        System.IO.Directory.CreateDirectory(DestinationPath);
                    }
                }

                // If the destination path does not exist, create it
                if (!Directory.Exists(DestinationPath) && ShouldProcess(target: DestinationPath, action: "Create"))
                {
                    System.IO.Directory.CreateDirectory(DestinationPath);
                }

                // Get the next entry in the archive and process it
                var nextEntry = archive.GetNextEntry();
                while (nextEntry != null)
                {
                    ProcessArchiveEntry(nextEntry);
                    nextEntry = archive.GetNextEntry();
                }


            } catch (System.UnauthorizedAccessException unauthorizedAccessException)
            {
                // TODO: Change this later to write an error
                throw unauthorizedAccessException;
            }
        }

        protected override void StopProcessing()
        {
            // Do clean up if the user abruptly stops execution
        }

        #region PrivateMethods

        private void ProcessArchiveEntry(IEntry entry)
        {
            Debug.Assert(DestinationPath is not null);

            // The location of the entry post-expanding of the archive
            string postExpandPath = GetPostExpansionPath(entryName: entry.Name, destinationPath: _destinationPathInfo.FullName);

            // If postExpandPath has a terminating `/`, remove it (there is case where overwriting a file may fail because of this)
            if (postExpandPath.EndsWith(System.IO.Path.DirectorySeparatorChar))
            {
                postExpandPath = postExpandPath.Remove(postExpandPath.Length - 1);
            }

            // If the entry name is invalid, write a non-terminating error and stop processing the entry
            if (IsPathInvalid(postExpandPath))
            {
                var errorRecord = ErrorMessages.GetErrorRecord(ErrorCode.InvalidPath, postExpandPath);
                WriteError(errorRecord);
                return;
            }
            
            System.IO.FileSystemInfo postExpandPathInfo = new System.IO.FileInfo(postExpandPath);

            // Use this variable to keep track if there is a collision
            // If the postExpandPath is a file, then no matter if the entry is a file or directory, it is a collision
            bool hasCollision = postExpandPathInfo.Exists;

            if (System.IO.Directory.Exists(postExpandPath))
            {
                var directoryInfo = new System.IO.DirectoryInfo(postExpandPath);

                // If the entry is a directory and postExpandPath is a directory, no collision occurs (because there is no need to overwrite directories)
                hasCollision = !entry.IsDirectory;

                // If postExpandPath is an existing directory containing files and/or directories, then write an error
                if (hasCollision && directoryInfo.GetFileSystemInfos().Length > 0)
                {
                    var errorRecord = ErrorMessages.GetErrorRecord(ErrorCode.DestinationIsNonEmptyDirectory, postExpandPath);
                    WriteError(errorRecord);
                    return;
                }
                // If postExpandPath is the same as the working directory, then write an error
                if (hasCollision && postExpandPath == SessionState.Path.CurrentFileSystemLocation.ProviderPath)
                {
                    var errorRecord = ErrorMessages.GetErrorRecord(ErrorCode.CannotOverwriteWorkingDirectory, postExpandPath);
                    WriteError(errorRecord);
                    return;
                }
                postExpandPathInfo = directoryInfo;
            }

            // Throw an error if the cmdlet is not in Overwrite mode but the postExpandPath exists
            if (hasCollision && WriteMode != ExpandArchiveWriteMode.Overwrite)
            {
                var errorRecord = ErrorMessages.GetErrorRecord(ErrorCode.DestinationExists, postExpandPath);
                WriteError(errorRecord);
                return;
            }

            string expandAction = hasCollision ? "Overwrite and Expand" : "Expand";
            if (ShouldProcess(target: postExpandPath, action: expandAction))
            {
                if (hasCollision)
                {
                    postExpandPathInfo.Delete();
                }
                // Only expand the entry if there is a need to expand
                // There is a need to expand unless the entry is a directory and the postExpandPath is also a directory
                if (!(entry.IsDirectory && postExpandPathInfo.Attributes.HasFlag(FileAttributes.Directory) && postExpandPathInfo.Exists))
                {
                    entry.ExpandTo(postExpandPath);
                }
            }
        }

        private void ValidateDestinationPath()
        {
            Debug.Assert(DestinationPath is not null);

            // Throw an error if DestinationPath exists and the cmdlet is not in Overwrite mode 
            if (File.Exists(DestinationPath) && WriteMode == ExpandArchiveWriteMode.Expand) {
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: ErrorCode.CannotDetermineDestinationPath, errorItem: DestinationPath);
                ThrowTerminatingError(errorRecord);
            }

            // Ensure sourcePath is not the same as the destination path when the cmdlet is in overwrite mode
            // When the cmdlet is not in overwrite mode, other errors will be thrown when validating DestinationPath before it even gets to this line
            if (_sourcePath == DestinationPath && WriteMode == ExpandArchiveWriteMode.Overwrite)
            {
                ErrorCode errorCode = (ParameterSetName == nameof(ParameterSet.Path)) ? ErrorCode.SamePathAndDestinationPath : ErrorCode.SameLiteralPathAndDestinationPath;
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: errorCode, errorItem: DestinationPath);
                ThrowTerminatingError(errorRecord);
            }
        }

        private void ValidateSourcePath(string path)
        {
            // Resolve path
            if (ParameterSetName == nameof(ParameterSet.Path)) {
                // Set nonexistentPaths to null because we don't want to capture any nonexistent paths
                var resolvedPaths = _pathHelper.GetResolvedPathFromPSProviderPath(path: path, pathMustExist: true);
                Debug.Assert(resolvedPaths is not null);

                // If the path resolves to multiple paths, throw a terminating error
                if (resolvedPaths.Count > 1) {
                    var errorRecord = ErrorMessages.GetErrorRecord(ErrorCode.PathResolvedToMultiplePaths, path);
                    ThrowTerminatingError(errorRecord);
                }

                // Set _sourcePath to the first & only path in resolvedPaths
                _sourcePath = resolvedPaths[0];
            } else {
                // Set nonexistentPaths to null because we don't want to capture any nonexistent paths
                var resolvedPath = _pathHelper.GetUnresolvedPathFromPSProviderPath(path: path, pathMustExist: true);
                Debug.Assert(resolvedPath is not null);
                // Set _sourcePath to resolvedPath
                _sourcePath = resolvedPath;
            }

            // Throw a terminating error if _sourcePath is a directory
            if (Directory.Exists(_sourcePath))
            {
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: ErrorCode.DestinationExistsAsDirectory, errorItem: _sourcePath);
                ThrowTerminatingError(errorRecord);
            }
        }

        private string GetPostExpansionPath(string entryName, string destinationPath)
        {
            // Normalize entry name - on Windows, replace forwardslash with backslash
            string normalizedEntryName = entryName.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);
            return System.IO.Path.Combine(destinationPath, normalizedEntryName);
        }

        private bool IsPathInvalid(string path)
        {
            foreach (var invalidCharacter in System.IO.Path.GetInvalidPathChars())
            {
                if (path.Contains(invalidCharacter))
                {
                    return true;
                }
            }
            return false;
        }
        
        // Used to determine what the DestinationPath should be when it is not specified
        private string DetermineDestinationPath(IArchive archive)
        {
            var workingDirectory = SessionState.Path.CurrentFileSystemLocation.ProviderPath;
            string? destinationDirectory = null;
            
            // If the archive has a single top-level directory only, the destination will be: "working directory"
            // This makes it easier for the cmdlet to expand the directory without needing addition checks
            if (archive.HasTopLevelDirectory())
            {
                destinationDirectory = workingDirectory;
            }
            // Otherwise, the destination path will be: "working directory/archive file name"
            else
            {
                var filename = System.IO.Path.GetFileName(archive.Path);
                // If filename does have an exension, remove the extension and set the filename minus extension as destinationDirectory
                if (System.IO.Path.GetExtension(filename) != string.Empty)
                {
                    destinationDirectory = System.IO.Path.ChangeExtension(path: filename, extension: string.Empty);
                }
            }

            if (destinationDirectory is null)
            {
                var errorRecord = ErrorMessages.GetErrorRecord(ErrorCode.CannotDetermineDestinationPath);
                ThrowTerminatingError(errorRecord);
            }
            Debug.Assert(destinationDirectory is not null);
            return System.IO.Path.Combine(workingDirectory, destinationDirectory);
        }

        #endregion
    }
}
