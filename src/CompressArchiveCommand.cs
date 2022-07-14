using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Reflection;

namespace Microsoft.PowerShell.Archive
{
    [Cmdlet("Compress", "Archive", SupportsShouldProcess = true)]
    [OutputType(typeof(System.IO.FileInfo))]
    public class CompressArchiveCommand : PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Path", ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PathWithOverwrite", ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PathWithUpdate", ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string[]? Path { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "LiteralPath", ValueFromPipeline = false, ValueFromPipelineByPropertyName = true)]
        [Parameter(Mandatory = true, ParameterSetName = "LiteralPathWithOverwrite", ValueFromPipeline = false, ValueFromPipelineByPropertyName = true)]
        [Parameter(Mandatory = true, ParameterSetName = "LiteralPathWithUpdate", ValueFromPipeline = false, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("PSPath")]
        public string[]? LiteralPath { get; set; }

        [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = false, ValueFromPipelineByPropertyName = false)]
        [ValidateNotNullOrEmpty]
        public string? DestinationPath { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "PathWithUpdate", ValueFromPipeline = false, ValueFromPipelineByPropertyName = false)]
        [Parameter(Mandatory = true, ParameterSetName = "LiteralPathWithUpdate", ValueFromPipeline = false, ValueFromPipelineByPropertyName = false)]
        public SwitchParameter Update { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "PathWithOverwrite", ValueFromPipeline = false, ValueFromPipelineByPropertyName = false)]
        [Parameter(Mandatory = true, ParameterSetName = "LiteralPathWithOverwrite", ValueFromPipeline = false, ValueFromPipelineByPropertyName = false)]
        public SwitchParameter Overwrite { get; set; }

        [Parameter()]
        public SwitchParameter PassThru { get; set; } = false;

        [Parameter()]
        [ValidateNotNullOrEmpty]
        public System.IO.Compression.CompressionLevel CompressionLevel { get; set; }

        private List<string> _sourcePaths;

        private PathHelper _pathHelper;

        public CompressArchiveCommand()
        {
            _sourcePaths = new List<string>();
            _pathHelper = new PathHelper(this);
        }

        protected override void BeginProcessing()
        {
            base.BeginProcessing();
            // TODO: Add exception handling
            DestinationPath = _pathHelper.ResolveToSingleFullyQualifiedPath(DestinationPath);

            System.IO.FileInfo archiveFileInfo = new System.IO.FileInfo(DestinationPath);
            System.IO.DirectoryInfo directoryInfo = new System.IO.DirectoryInfo(DestinationPath);

            // TODO: Add tests cases for conditions below

            //Throw an error if DestinationPath exists and the cmdlet is not in Update mode or Overwrite is not specified 
            if ((archiveFileInfo.Exists || directoryInfo.Exists) && !Update.IsPresent && !Overwrite.IsPresent)
            {
                ThrowTerminatingError(ErrorMessages.GetErrorRecordForArgumentException(ErrorCode.ArchiveExists, DestinationPath));
            }
            //Throw an error if the cmdlet is in Update mode but the archive is read only
            else if (archiveFileInfo.Exists && Update.IsPresent && archiveFileInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                ThrowTerminatingError(ErrorMessages.GetErrorRecordForArgumentException(ErrorCode.ArchiveReadOnly, DestinationPath));
            }
            //Throw an error if the DestinationPath is a directory and the cmdlet is in Update mode
            else if (directoryInfo.Exists && Update.IsPresent)
            {
                ThrowTerminatingError(ErrorMessages.GetErrorRecordForArgumentException(ErrorCode.ArchiveExistsAsDirectory, DestinationPath));
            }
            //Throw an error if the DestinationPath is a directory with at least item and the cmdlet is in Overwrite mode
            else if (directoryInfo.Exists && Overwrite.IsPresent && directoryInfo.GetFileSystemInfos().Length > 0)
            {
                ThrowTerminatingError(ErrorMessages.GetErrorRecordForArgumentException(ErrorCode.ArchiveExistsAsDirectory, DestinationPath));
            }
        }

        protected override void ProcessRecord()
        {
            if (ParameterSetName.StartsWith("Path")) _sourcePaths.AddRange(Path);
            else _sourcePaths.AddRange(LiteralPath);
        }

        protected override void EndProcessing()
        {
            //Get archive entries, validation is performed by PathHelper
            List<ArchiveEntry> archiveEntries = _pathHelper.GetEntryRecordsForPath(_sourcePaths.ToArray(), ParameterSetName.StartsWith("LiteralPath"));

            //Create a zip archive
            using (var archive = ArchiveFactory.GetArchive(ArchiveFormat.zip, DestinationPath, Update ? ArchiveMode.Update : ArchiveMode.Create, CompressionLevel))
            {
                //Add entries to the archive
                // TODO: Update progress
                foreach (ArchiveEntry entry in archiveEntries)
                {
                    archive.AddFilesytemEntry(entry);
                }
            }
        }

        protected override void StopProcessing()
        {
            base.StopProcessing();
        }
    }
}
