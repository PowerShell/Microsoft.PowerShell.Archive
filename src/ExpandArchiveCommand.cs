using Microsoft.PowerShell.Archive.Localized;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
        [Parameter(Position=0, Mandatory = true, ParameterSetName = "Path", ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; } = String.Empty;

        [Parameter(Mandatory = true, ParameterSetName = "LiteralPath", ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string LiteralPath { get; set; } = String.Empty;

        [Parameter(Position = 2, Mandatory = true)]
        public string DestinationPath { get; set; } = String.Empty;

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

        #endregion

        public ExpandArchiveCommand()
        {
            _didCreateOutput = false;
            _pathHelper = new PathHelper(cmdlet: this);
            _destinationPathInfo = null;
        }

        protected override void BeginProcessing()
        {
            // Resolve DestinationPath
            _destinationPathInfo = _pathHelper.ResolveToSingleFullyQualifiedPath(path: DestinationPath, hasWildcards: false);
            DestinationPath = _destinationPathInfo.FullName;

            ValidateDestinationPath();
        }

        protected override void ProcessRecord()
        {
            
        }

        protected override void EndProcessing()
        {
            // Resolve Path or LiteralPath
            bool checkForWildcards = ParameterSetName.StartsWith("Path");
            string path = ParameterSetName.StartsWith("Path") ? Path : LiteralPath;
            System.IO.FileSystemInfo sourcePath = _pathHelper.ResolveToSingleFullyQualifiedPath(path: path, hasWildcards: checkForWildcards);

            ValidateSourcePath(sourcePath);

            // Determine archive format based on sourcePath
            Format = DetermineArchiveFormat(destinationPath: sourcePath.FullName, archiveFormat: Format);

            // Get an archive from source path -- this is where we will switch between different types of archives
            using IArchive? archive = ArchiveFactory.GetArchive(format: Format ?? ArchiveFormat.zip, archivePath: sourcePath.FullName, archiveMode: ArchiveMode.Extract, compressionLevel: System.IO.Compression.CompressionLevel.NoCompression);
            try
            {
                // If the destination path is a file that needs to be overwriten, delete it
                if (_destinationPathInfo.Exists && !_destinationPathInfo.Attributes.HasFlag(FileAttributes.Directory) && WriteMode == ExpandArchiveWriteMode.Overwrite)
                {
                    if (ShouldProcess(target: _destinationPathInfo.FullName, action: "Overwrite"))
                    {
                        _destinationPathInfo.Delete();
                        System.IO.Directory.CreateDirectory(_destinationPathInfo.FullName);
                        _destinationPathInfo = new System.IO.DirectoryInfo(_destinationPathInfo.FullName);
                    }
                }

                // If the destination path does not exist, create it
                if (!_destinationPathInfo.Exists && ShouldProcess(target: _destinationPathInfo.FullName, action: "Create"))
                {
                    System.IO.Directory.CreateDirectory(_destinationPathInfo.FullName);
                    _destinationPathInfo = new System.IO.DirectoryInfo(_destinationPathInfo.FullName);
                }

                // Get the next entry in the archive
                var nextEntry = archive.GetNextEntry();
                while (nextEntry != null)
                {
                    // TODO: Refactor this part

                    // The location of the entry post-expanding of the archive
                    string postExpandPath = GetPostExpansionPath(entryName: nextEntry.Name, destinationPath: _destinationPathInfo.FullName);

                    // If the entry name is invalid, write a non-terminating error
                    if (IsPathInvalid(postExpandPath))
                    {
                        var errorRecord = ErrorMessages.GetErrorRecord(ErrorCode.InvalidPath, postExpandPath);
                        WriteError(errorRecord);
                        continue;
                    }
                    
                    System.IO.FileSystemInfo postExpandPathInfo = new System.IO.FileInfo(postExpandPath);

                    if (!postExpandPathInfo.Exists && System.IO.Directory.Exists(postExpandPath))
                    {
                        var directoryInfo = new System.IO.DirectoryInfo(postExpandPath);
                        // If postExpandPath is an existing directory containing files and/or directories, then write an error
                        if (directoryInfo.GetFileSystemInfos().Length > 0)
                        {
                            var errorRecord = ErrorMessages.GetErrorRecord(ErrorCode.DestinationIsNonEmptyDirectory, postExpandPath);
                            WriteError(errorRecord);
                            continue;
                        }
                        postExpandPathInfo = directoryInfo;
                    }

                    // Throw an error if the cmdlet is not in Overwrite mode but the postExpandPath exists
                    if (postExpandPathInfo.Exists && WriteMode != ExpandArchiveWriteMode.Overwrite)
                    {
                        var errorRecord = ErrorMessages.GetErrorRecord(ErrorCode.DestinationExists, postExpandPath);
                        WriteError(errorRecord);
                        continue;
                    }

                    if (postExpandPathInfo.Exists && ShouldProcess(target: _destinationPathInfo.FullName, action: "Expand and Overwrite"))
                    {
                        postExpandPathInfo.Delete();
                        nextEntry.ExpandTo(_destinationPathInfo.FullName);
                    } else if (ShouldProcess(target: _destinationPathInfo.FullName, action: "Expand"))
                    {
                        nextEntry.ExpandTo(_destinationPathInfo.FullName);
                    }
                    

                    nextEntry = archive.GetNextEntry();
                }


            } catch
            {

            }
        }

        protected override void StopProcessing()
        {
            // Do clean up if the user abruptly stops execution
        }

        #region PrivateMethods

        private void ValidateDestinationPath()
        {
            ErrorCode? errorCode = null;

            // In this case, DestinationPath does not exist
            if (!_destinationPathInfo.Exists)
            {
                // Do nothing
            }
            // Check if DestinationPath is an existing directory
            else if (_destinationPathInfo.Attributes.HasFlag(FileAttributes.Directory))
            {
                // Throw an error if the DestinationPath is the current working directory and the cmdlet is in Overwrite mode
                if (WriteMode == ExpandArchiveWriteMode.Overwrite && _destinationPathInfo.FullName == SessionState.Path.CurrentFileSystemLocation.ProviderPath)
                {
                    errorCode = ErrorCode.CannotOverwriteWorkingDirectory;
                }
            }
            // If DestinationPath is an existing file
            else
            {
                // Throw an error if DestinationPath exists and the cmdlet is not in Overwrite mode 
                if (WriteMode == ExpandArchiveWriteMode.Expand)
                {
                    errorCode = ErrorCode.DestinationExists;
                }
            }

            if (errorCode != null)
            {
                // Throw an error -- since we are validating DestinationPath, the problem is with DestinationPath
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: errorCode.Value, errorItem: _destinationPathInfo.FullName);
                ThrowTerminatingError(errorRecord);
            }
        }

        private void ValidateSourcePath(System.IO.FileSystemInfo sourcePath)
        {
            // Throw a terminating error if sourcePath does not exist
            if (!sourcePath.Exists)
            {
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: ErrorCode.PathNotFound, errorItem: sourcePath.FullName);
                ThrowTerminatingError(errorRecord);
            }

            // Throw a terminating error if sourcePath is a directory
            if (sourcePath.Attributes.HasFlag(FileAttributes.Directory))
            {
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: ErrorCode.DestinationExistsAsDirectory, errorItem: sourcePath.FullName);
                ThrowTerminatingError(errorRecord);
            }

            // Ensure sourcePath is not the same as the destination path when the cmdlet is in overwrite mode
            // When the cmdlet is not in overwrite mode, other errors will be thrown when validating DestinationPath before it even gets to this line
            if (PathHelper.ArePathsSame(sourcePath, _destinationPathInfo) && WriteMode == ExpandArchiveWriteMode.Overwrite)
            {
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: ErrorCode.SamePathAndDestinationPath, errorItem: sourcePath.FullName);
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

        #endregion
    }
}
