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

        private HashSet<string> _sourcePaths;

        private HashSet<string> _duplicatePaths;

        public CompressArchiveCommand()
        {
            _sourcePaths = new HashSet<string>();
            _duplicatePaths = new HashSet<string>();
        }

        protected override void BeginProcessing()
        {
            base.BeginProcessing();
            ResolvePath(DestinationPath);
        }

        protected override void ProcessRecord()
        {
            //Validate paths
            string[]? paths;
            paths = ParameterSetName.StartsWith("Path") ? ResolvePathWithWildcards(Path) : ResolvePathWithoutWildcards(LiteralPath);

            foreach (var path in paths)
            {
                //Add path to source paths
                if (!_sourcePaths.Add(path))
                {
                    //If the set already contains the path, add it to the set of duplicates
                    _duplicatePaths.Add(path);
                }
            }

            
        }

        protected override void EndProcessing()
        {
            //If there are duplicate paths, throw an error
            if (_duplicatePaths.Count > 0)
            {
                var errorMsg = String.Format(ErrorMessages.DuplicatePathsMessage, _duplicatePaths.ToString());
                var exception = new System.ArgumentException(errorMsg);
                ErrorRecord errorRecord = new ErrorRecord(exception, "DuplicatePathFound", ErrorCategory.InvalidArgument, _duplicatePaths);
                ThrowTerminatingError(errorRecord);

            }
        }

        protected override void StopProcessing()
        {
            base.StopProcessing();
        }

        private string[] ResolvePathWithWildcards(string[] paths)
        {
            List<string> outputPaths = new List<string>();
            foreach (var path in paths)
            {
                var resolvedPaths = GetResolvedProviderPathFromPSPath(path, out var providerInfo);
                if (providerInfo.Name != "FileSystem")
                {
                    //Throw an error
                }
                outputPaths.AddRange(resolvedPaths);
            }
            
            return outputPaths.ToArray();
        }

        private string[] ResolvePathWithoutWildcards(string[] paths)
        {
            string[] outputPaths = new string[paths.Length];
            for (int i=0; i<paths.Length; i++)
            {
                var path = paths[i];
                var resolvedPath = GetUnresolvedProviderPathFromPSPath(path);
                if (!System.IO.File.Exists(resolvedPath) && !System.IO.Directory.Exists(resolvedPath))
                {
                    //Throw an error
                    var errorMsg = String.Format(ErrorMessages.PathNotFoundMessage, path);
                    var exception = new System.InvalidOperationException(errorMsg);
                    var errorRecord = new ErrorRecord(exception, "PathNotFound", ErrorCategory.InvalidArgument, path);
                    ThrowTerminatingError(errorRecord);
                }
                outputPaths[i] = resolvedPath;
            }

            return outputPaths;
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
