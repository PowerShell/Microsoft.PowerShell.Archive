using Microsoft.PowerShell.Archive.Localized;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net.NetworkInformation;
using System.Text;

namespace Microsoft.PowerShell.Archive
{
    // TODO: Add exception handling
    internal class PathHelper
    {
        private PSCmdlet _cmdlet;

        private const string FileSystemProviderName = "FileSystem";

        internal PathHelper(PSCmdlet cmdlet)
        {
            _cmdlet = cmdlet;
        }

        /// <summary>
        /// Get a list of ArchiveAddition objects from an array of paths depending on whether we want to use the path literally or not.
        /// </summary>
        /// <param name="paths">An array of paths, relative or absolute -- they do not necessarily have to be fully qualifed paths.</param>
        /// <param name="literalPath">If true, wildcard characters in each path are expanded. If false, wildcard characters are not expanded.</param>
        /// <returns></returns>
        internal List<ArchiveAddition> GetArchiveAdditionsForPath(string[] paths, bool literalPath)
        {
            List<ArchiveAddition> additions = new List<ArchiveAddition>();

            // Used to keep track of non-filesystem paths
            HashSet<string> nonfilesystemPaths = new HashSet<string>();

            foreach (var path in paths)
            {
                // Based on the value of literalPath, call the appropriate method
                if (literalPath)
                {
                    AddArchiveAdditionForUserEnteredLiteralPath(path: path, archiveAdditions: additions, nonfilesystemPaths: nonfilesystemPaths);
                } else
                {
                    AddArchiveAdditionForUserEnteredNonLiteralPath(path: path, archiveAdditions: additions, nonfilesystemPaths: nonfilesystemPaths);
                }
            }

            // If there is at least 1 non-filesystem path, throw an invalid path error
            if (nonfilesystemPaths.Count > 0)
            {
                // Get an error record and throw it
                var commaSperatedPaths = String.Join(separator: ',', values: nonfilesystemPaths);
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: ErrorCode.InvalidPath, errorItem: commaSperatedPaths);
                _cmdlet.ThrowTerminatingError(errorRecord: errorRecord);
            }

            // If there are duplicate paths, throw an error
            var duplicates = GetDuplicatePaths(additions);
            if (duplicates.Count() > 0)
            {
                // Get an error record and throw it
                var commaSperatedPaths = String.Join(separator: ',', values: duplicates);
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: ErrorCode.DuplicatePaths, errorItem: commaSperatedPaths);
                _cmdlet.ThrowTerminatingError(errorRecord: errorRecord);
            }

            return additions;
        }

        /// <summary>
        /// Resolves a user-entered path while expanding wildcards, creates an ArchiveAddition object for it, and its to the list of ArchiveAddition objects
        /// </summary>
        /// <param name="path"></param>
        /// <param name="archiveAdditions"></param>
        /// <param name="nonfilesystemPaths"></param>
        private void AddArchiveAdditionForUserEnteredNonLiteralPath(string path, List<ArchiveAddition> archiveAdditions, HashSet<string> nonfilesystemPaths)
        {
            // Resolve the path -- I don't think we need to handle exceptions here as no special behavior occurs when an exception occurs
            var resolvedPaths = _cmdlet.SessionState.Path.GetResolvedProviderPathFromPSPath(path: path, provider: out var providerInfo);

            // Check if the path if from the filesystem
            if (providerInfo?.Name != FileSystemProviderName)
            {
                // If not, add the path to the set of non-filesystem paths. We will throw an error later so we can show the user all invalid paths at once
                nonfilesystemPaths.Add(path);
                return;
            }

            // Check if the cmdlet can preserve paths based on path variable
            bool shouldPreservePathStructure = CanPreservePathStructure(path);

            // Go through each resolved path and add an ArchiveAddition for it to additions
            for (int i = 0; i < resolvedPaths.Count; i++)
            {
                var resolvedPath = resolvedPaths[i];
                AddAdditionForFullyQualifiedPath(path: resolvedPath, additions: archiveAdditions, shouldPreservePathStructure: shouldPreservePathStructure);
            }
        }

        /// <summary>
        /// Resolves a user-entered path without expanding wildcards, creates an ArchiveAddition object for it, and its to the list of ArchiveAddition objects
        /// </summary>
        /// <param name="path"></param>
        /// <param name="archiveAdditions"></param>
        /// <param name="nonfilesystemPaths"></param>
        private void AddArchiveAdditionForUserEnteredLiteralPath(string path, List<ArchiveAddition> archiveAdditions, HashSet<string> nonfilesystemPaths)
        {
            // Resolve the path -- gets the fully qualified path
            // I don't think we need to handle exceptions for the call below as the cmdlet does not have any special behaviors when the call below throws an exception
            string fullyQualifiedPath = _cmdlet.SessionState.Path.GetUnresolvedProviderPathFromPSPath(path, out var providerInfo, out var psDriveInfo);

            // Check if the path is from the filesystem
            if (providerInfo.Name != FileSystemProviderName)
            {
                nonfilesystemPaths.Add(path);
                return;
            }

            // Check if we can preserve the path structure -- this is based on the original path the user entered (not fully qualified)
            bool canPreservePathStructure = CanPreservePathStructure(path: path);

            // Add an ArchiveAddition for the path to the list of additions
            AddAdditionForFullyQualifiedPath(path: fullyQualifiedPath, additions: archiveAdditions, shouldPreservePathStructure: canPreservePathStructure);
        }

        /// <summary>
        /// Adds an ArchiveAddition object for a path to a list of ArchiveAddition objects
        /// </summary>
        /// <param name="path">The fully qualified path</param>
        /// <param name="additions">The list where to add the ArchiveAddition object for the path</param>
        /// <param name="shouldPreservePathStructure">If true, relative path structure will be preserved. If false, relative path structure will NOT be preserved.</param>
        private void AddAdditionForFullyQualifiedPath(string path, List<ArchiveAddition> additions, bool shouldPreservePathStructure)
        {
            var additionType = ArchiveAddition.ArchiveAdditionType.File;
            if (System.IO.Directory.Exists(path))
            {
                // Add directory seperator to end if it does not already have it
                if (!path.EndsWith(System.IO.Path.DirectorySeparatorChar)) path += System.IO.Path.DirectorySeparatorChar;
                // Recurse through the child items and add them to additions
                AddDescendentEntries(path: path, additions: additions, shouldPreservePathStructure: shouldPreservePathStructure);
                additionType = ArchiveAddition.ArchiveAdditionType.Directory;
            }
            else if (!System.IO.File.Exists(path))
            {
                // Throw an error if the path does not exist
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: ErrorCode.PathNotFound, errorItem: path);
                _cmdlet.ThrowTerminatingError(errorRecord: errorRecord);
            }

            // Add an entry for the item
            additions.Add(new ArchiveAddition(entryName: GetEntryName(path: path, shouldPreservePathStructure: shouldPreservePathStructure), fullPath: path, type: additionType));
        }

        /// <summary>
        /// Creates an ArchiveAdditon object for each child item of the directory and adds it to a list of ArchiveAddition objects
        /// </summary>
        /// <param name="path">A fully qualifed path referring to a directory</param>
        /// <param name="additions">Where the ArchiveAddtion object for each child item of the directory will be added</param>
        /// <param name="shouldPreservePathStructure">See above</param>
        private void AddDescendentEntries(string path, List<ArchiveAddition> additions, bool shouldPreservePathStructure)
        {
            try
            {
                var directoryInfo = new System.IO.DirectoryInfo(path);
                foreach (var childPath in directoryInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    // childPath can either be a file or directory, and nothing else
                    ArchiveAddition.ArchiveAdditionType type = ArchiveAddition.ArchiveAdditionType.File;
                    if (childPath is System.IO.DirectoryInfo)
                    {
                        type = ArchiveAddition.ArchiveAdditionType.Directory;
                    }
                    
                    // Add an entry for each child path
                    var entryName = GetEntryName(path: childPath.FullName, shouldPreservePathStructure: shouldPreservePathStructure);
                    additions.Add(new ArchiveAddition(entryName: entryName, fullPath: childPath.FullName, type: type));
                }
            } 
            // Throw a non-terminating error if a securityException occurs
            catch (System.Security.SecurityException securityException)
            {
                var errorId = ErrorCode.InsufficientPermissionsToAccessPath.ToString();
                var errorRecord = new ErrorRecord(securityException, errorId: errorId, ErrorCategory.SecurityError, targetObject: path);
                _cmdlet.WriteError(errorRecord);
            }
            // Throw a terminating error if a directoryNotFoundException occurs
            catch (System.IO.DirectoryNotFoundException)
            {
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: ErrorCode.PathNotFound, errorItem: path);
                _cmdlet.ThrowTerminatingError(errorRecord);
            }
        }

        /// <summary>
        /// Get the archive path from a fully qualified path
        /// </summary>
        /// <param name="path">A fully qualified path</param>
        /// <param name="shouldPreservePathStructure"></param>
        /// <returns></returns>
        private string GetEntryName(string path, bool shouldPreservePathStructure)
        {
            // If the path is relative to the current working directory, return the relative path as name
            if (shouldPreservePathStructure && TryGetPathRelativeToCurrentWorkingDirectory(path, out var relativePath))
            {
                return relativePath;
            }
            // Otherwise, return the name of the directory or file
            if (path.EndsWith(System.IO.Path.DirectorySeparatorChar))
            {
                // Get substring from second-last directory seperator char till end
                int secondLastIndex = path.LastIndexOf(System.IO.Path.DirectorySeparatorChar, path.Length - 2);
                if (secondLastIndex == -1) return path;
                else return path.Substring(secondLastIndex + 1);
            }
            else
            {
                return System.IO.Path.GetFileName(path);
            }
        }

        /// <summary>
        /// Get the duplicate fully qualified paths from a list of ArchiveAdditions
        /// </summary>
        /// <param name="additions"></param>
        /// <returns></returns>
        private IEnumerable<string> GetDuplicatePaths(List<ArchiveAddition> additions)
        {
            return additions.GroupBy(x => x.FullPath)
                    .Where(group => group.Count() > 1)
                    .Select(x => x.Key);
        }

        /// <summary>
        /// Resolve a path that may contain wildcard characters and could be a literal or non-literal path to a single fully qualified path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        internal string ResolveToSingleFullyQualifiedPath(string path)
        {
            // Currently, all this function does is return the literal fully qualified path of a path

            // First, get non-literal path
            string fullyQualifiedPath = _cmdlet.SessionState.Path.GetUnresolvedProviderPathFromPSPath(path, out var providerInfo, out var psDriveInfo);

            // If the path is not from the filesystem, throw an error
            if (providerInfo.Name != FileSystemProviderName)
            {
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: ErrorCode.InvalidPath, errorItem: path);
                _cmdlet.ThrowTerminatingError(errorRecord);
            }

            return fullyQualifiedPath;
        }

        /// <summary>
        /// Determines if the relative path structure can be preserved
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private bool CanPreservePathStructure(string path)
        {
            return System.IO.Path.IsPathRooted(path);
        }

        /// <summary>
        /// Tries to get a path relative to the current working directory as long as the relative path does not contain ".."
        /// </summary>
        /// <param name="path"></param>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        private bool TryGetPathRelativeToCurrentWorkingDirectory(string path, out string relativePath)
        {
            relativePath = System.IO.Path.GetRelativePath(_cmdlet.SessionState.Path.CurrentFileSystemLocation.Path, path);
            return !relativePath.Contains("..");
        }
    }
}
