using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;

namespace Microsoft.PowerShell.Archive
{
    [Cmdlet("Compress", "Archive", SupportsShouldProcess = true)]
    [OutputType(typeof(System.IO.FileInfo))]
    public class CompressArchiveCommand : PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Path", ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PathWithForce", ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "PathWithUpdate", ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string[]? Path { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "LiteralPath", ValueFromPipeline = false, ValueFromPipelineByPropertyName = true)]
        [Parameter(Mandatory = true, ParameterSetName = "LiteralPathWithForce", ValueFromPipeline = false, ValueFromPipelineByPropertyName = true)]
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

        [Parameter(Mandatory = true, ParameterSetName = "PathWithForce", ValueFromPipeline = false, ValueFromPipelineByPropertyName = false)]
        [Parameter(Mandatory = true, ParameterSetName = "LiteralPathWithForce", ValueFromPipeline = false, ValueFromPipelineByPropertyName = false)]
        public SwitchParameter Force { get; set; }

        [Parameter()]
        public SwitchParameter PassThru { get; set; } = false;

        [Parameter()]
        [ValidateNotNullOrEmpty]
        public System.IO.Compression.CompressionLevel CompressionLevel { get; set; }

        private List<string> _sourcePaths;

        public CompressArchiveCommand()
        {
            _sourcePaths = new List<string>();
        }

        protected override void BeginProcessing()
        {
            base.BeginProcessing();
            ResolvePath(DestinationPath);
        }

        protected override void ProcessRecord()
        {
            if (ParameterSetName.StartsWith("Path")) _sourcePaths.AddRange(Path);
            else _sourcePaths.AddRange(LiteralPath);
        }

        protected override void EndProcessing()
        {
            PathHelper pathHelper = new PathHelper(this);

            //Get archive entries, validation is performed by PathHelper
            List<ArchiveEntry> archiveEntries = pathHelper.GetEntryRecordsForPath(_sourcePaths.ToArray(), ParameterSetName.StartsWith("LiteralPath"));

            //
        }

        protected override void StopProcessing()
        {
            base.StopProcessing();
        }

        private string ResolvePath(string path)
        {
            //Get unresolved path
            var unresolvedPath = GetUnresolvedProviderPathFromPSPath(path);

            //Get resolved path
            try
            {
                var resolvedPath = GetResolvedProviderPathFromPSPath(path, out var providerInfo);

                if (resolvedPath.Count > 1 || providerInfo.Name != "FileSystem")
                {
                    //Throw an error: duplicate paths
                }

                if (resolvedPath.Count == 1 && resolvedPath[0] != unresolvedPath)
                {
                    //Throw an error: duplicate paths
                }

                if (resolvedPath.Count == 1 && resolvedPath[0] == unresolvedPath) return unresolvedPath;

            } catch (Exception ex)
            {
                
            }

            ////If unresolvedPath doesn't exist, throw an error
            //if (!System.IO.File.Exists(unresolvedPath))
            //{
            //    //Throw an error: path not found
            //    var errorMsg = String.Format(ErrorMessages.PathNotFoundMessage, path);
            //    var exception = new System.InvalidOperationException(errorMsg);
            //    var errorRecord = new ErrorRecord(exception, "PathNotFound", ErrorCategory.InvalidArgument, path);
            //    ThrowTerminatingError(errorRecord);
            //}

            return null;
        }
    }
}
