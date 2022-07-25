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
            // Keep the exception at the top, then when an error occurs, use the exception to create an ErrorRecord
            Exception? exception = null;
            try
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
            catch (System.Management.Automation.ProviderNotFoundException providerNotFoundException)
            {
                exception = providerNotFoundException;
            }
            catch (System.Management.Automation.DriveNotFoundException driveNotFoundException)
            {
                exception = driveNotFoundException;
            }
            catch (System.Management.Automation.ProviderInvocationException providerInvocationException)
            {
                exception = providerInvocationException;
            }
            catch (System.Management.Automation.PSNotSupportedException notSupportedException)
            {
                exception = notSupportedException;
            }
            catch (System.Management.Automation.PSInvalidOperationException invalidOperationException)
            {
                exception = invalidOperationException;
            }
            // Throw a terminating error if the path could not be found
            catch (System.Management.Automation.ItemNotFoundException notFoundException)
            {
                var errorRecord = new ErrorRecord(exception: notFoundException, errorId: ErrorCode.PathNotFound.ToString(), errorCategory: ErrorCategory.InvalidArgument,
                    targetObject: path);
                _cmdlet.ThrowTerminatingError(errorRecord);
            }

            // If an exception (besides ItemNotFoundException) was caught, write a non-terminating error
            if (exception != null)
            {
                var errorRecord = new ErrorRecord(exception: exception, errorId: ErrorCode.InvalidPath.ToString(), errorCategory: ErrorCategory.InvalidArgument,
                    targetObject: path);
                _cmdlet.WriteError(errorRecord);
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
            // Keep the exception at the top, then when an error occurs, use the exception to create an ErrorRecord
            Exception? exception = null;
            try
            {
                // Resolve the path -- gets the fully qualified path
                string fullyQualifiedPath = _cmdlet.SessionState.Path.GetUnresolvedProviderPathFromPSPath(path, out var providerInfo, out var psDriveInfo);

                // Check if the path is from the filesystem
                if (providerInfo.Name != FileSystemProviderName)
                {
                    nonfilesystemPaths.Add(path);
                    return;
                }

                // We do not need to check if the path exists here because it is done in AddAdditionForFullyQualifiedMethod call

                // Check if we can preserve the path structure -- this is based on the original path the user entered (not fully qualified)
                bool canPreservePathStructure = CanPreservePathStructure(path: path);

                // Add an ArchiveAddition for the path to the list of additions
                AddAdditionForFullyQualifiedPath(path: fullyQualifiedPath, additions: archiveAdditions, shouldPreservePathStructure: canPreservePathStructure);
            } 
            catch (System.Management.Automation.ProviderNotFoundException providerNotFoundException)
            {
                exception = providerNotFoundException;
            } 
            catch (System.Management.Automation.DriveNotFoundException driveNotFoundException)
            {
                exception = driveNotFoundException;
            } 
            catch (System.Management.Automation.ProviderInvocationException providerInvocationException)
            {
                exception = providerInvocationException;
            } 
            catch (System.Management.Automation.PSNotSupportedException notSupportedException)
            {
                exception = notSupportedException;
            } 
            catch (System.Management.Automation.PSInvalidOperationException invalidOperationException)
            {
                exception = invalidOperationException;
            }

            // If an exception was caught, write a non-terminating error
            if (exception != null)
            {
                var errorRecord = new ErrorRecord(exception: exception, errorId: ErrorCode.InvalidPath.ToString(), errorCategory: ErrorCategory.InvalidArgument, 
                    targetObject: path);
                _cmdlet.WriteError(errorRecord);
            }
        }

        /// <summary>
        /// Adds an ArchiveAddition object for a path to a list of ArchiveAddition objects
        /// </summary>
        /// <param name="path">The fully qualified path</param>
        /// <param name="additions">The list where to add the ArchiveAddition object for the path</param>
        /// <param name="shouldPreservePathStructure">If true, relative path structure will be preserved. If false, relative path structure will NOT be preserved.</param>
        private void AddAdditionForFullyQualifiedPath(string path, List<ArchiveAddition> additions, bool shouldPreservePathStructure)
        {
            System.IO.FileSystemInfo fileSystemInfo = new System.IO.FileInfo(path);
            if (System.IO.Directory.Exists(path))
            {
                // Add directory seperator to end if it does not already have it
                if (!path.EndsWith(System.IO.Path.DirectorySeparatorChar)) path += System.IO.Path.DirectorySeparatorChar;
                // Recurse through the child items and add them to additions
                var directoryInfo = new System.IO.DirectoryInfo(path);
                AddDescendentEntries(directoryInfo: directoryInfo, additions: additions, shouldPreservePathStructure: shouldPreservePathStructure);
                fileSystemInfo = directoryInfo;
            }
            else if (!System.IO.File.Exists(path))
            {
                // Throw an error if the path does not exist
                var errorRecord = ErrorMessages.GetErrorRecord(errorCode: ErrorCode.PathNotFound, errorItem: path);
                _cmdlet.ThrowTerminatingError(errorRecord: errorRecord);
            }

            // Add an entry for the item
            var entryName = GetEntryName(fileSystemInfo: fileSystemInfo, shouldPreservePathStructure: shouldPreservePathStructure);
            additions.Add(new ArchiveAddition(entryName: entryName, fileSystemInfo: fileSystemInfo));
        }

        /// <summary>
        /// Creates an ArchiveAdditon object for each child item of the directory and adds it to a list of ArchiveAddition objects
        /// </summary>
        /// <param name="path">A fully qualifed path referring to a directory</param>
        /// <param name="additions">Where the ArchiveAddtion object for each child item of the directory will be added</param>
        /// <param name="shouldPreservePathStructure">See above</param>
        private void AddDescendentEntries(System.IO.DirectoryInfo directoryInfo, List<ArchiveAddition> additions, bool shouldPreservePathStructure)
        {
            try
            {
                // pathPrefix is used to construct the entry names of the descendents of the directory
                var pathPrefix = GetPrefixForPath(directoryInfo: directoryInfo);
                foreach (var childPath in directoryInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    /* childPath can either be a file or directory, and nothing else
                    //ArchiveAddition.ArchiveAdditionType type = ArchiveAddition.ArchiveAdditionType.File;
                    if (childPath is System.IO.DirectoryInfo)
                    {
                        type = ArchiveAddition.ArchiveAdditionType.Directory;
                    }*/
                    
                    // Add an entry for each child path
                    var entryName = GetEntryName(path: childPath.FullName, prefix: pathPrefix);
                    additions.Add(new ArchiveAddition(entryName: entryName, fileSystemInfo: childPath));
                }
            } 
            // Write a non-terminating error if a securityException occurs
            catch (System.Security.SecurityException securityException)
            {
                var errorId = ErrorCode.InsufficientPermissionsToAccessPath.ToString();
                var errorRecord = new ErrorRecord(securityException, errorId: errorId, ErrorCategory.SecurityError, targetObject: directoryInfo);
                _cmdlet.WriteError(errorRecord);
            }
        }

        /// <summary>
        /// Get the archive path from a fully qualified path
        /// </summary>
        /// <param name="path">A fully qualified path</param>
        /// <param name="shouldPreservePathStructure"></param>
        /// <returns></returns>
        private string GetEntryName(System.IO.FileSystemInfo fileSystemInfo, bool shouldPreservePathStructure)
        {
            // If the path is relative to the current working directory, return the relative path as name
            if (shouldPreservePathStructure && TryGetPathRelativeToCurrentWorkingDirectory(path: fileSystemInfo.FullName, out var relativePath))
            {
                return relativePath;
            }
            // Otherwise, return the name of the directory or file
            var entryName = fileSystemInfo.Name;
            if (fileSystemInfo.Attributes.HasFlag(FileAttributes.Directory) && !entryName.EndsWith(System.IO.Path.DirectorySeparatorChar))
            {
                entryName += System.IO.Path.DirectorySeparatorChar;
            }
            return entryName;
        }

        private string GetEntryName(string path, string prefix)
        {
            if (prefix == String.Empty) return path;

            // If the path does not start with the prefix, throw an exception
            if (!path.StartsWith(prefix))
            {
                throw new ArgumentException($"{path} does not begin with {prefix}");
            }

            // If the path is the same length as the prefix
            if (path.Length == prefix.Length)
            {
                throw new ArgumentException($"The length of {path} is shorter than or equal to the length of {prefix}");
            }

            string entryName = path.Substring(prefix.Length);

            return entryName;
        }

        private string GetPrefixForPath(System.IO.DirectoryInfo directoryInfo)
        {
            // Get the parent directory of the path
            if (directoryInfo.Parent is null)
            {
                return String.Empty;
            }
            var prefix = directoryInfo.Parent.FullName;
            if (!prefix.EndsWith(System.IO.Path.DirectorySeparatorChar))
            {
                prefix += System.IO.Path.DirectorySeparatorChar;
            }
            return prefix;
        }

        /// <summary>
        /// Get the duplicate fully qualified paths from a list of ArchiveAdditions
        /// </summary>
        /// <param name="additions"></param>
        /// <returns></returns>
        private IEnumerable<string> GetDuplicatePaths(List<ArchiveAddition> additions)
        {
            return additions.GroupBy(x => x.FileSystemInfo.FullName)
                    .Where(group => group.Count() > 1)
                    .Select(x => x.Key);
        }

        /// <summary>
        /// Resolve a path that may contain wildcard characters and could be a literal or non-literal path to a single fully qualified path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        internal System.IO.FileSystemInfo ResolveToSingleFullyQualifiedPath(string path)
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

            // Return filesystem info

            return GetFilesystemInfoForPath(fullyQualifiedPath);
        }

        /// <summary>
        /// Determines if the relative path structure can be preserved
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private bool CanPreservePathStructure(string path)
        {
            return !System.IO.Path.IsPathRooted(path);
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
        internal static bool ArePathsSame(string path1, string path2)
        {
            string fullPath1 = System.IO.Path.GetFullPath(path1);
            string fullPath2 = System.IO.Path.GetFullPath(path2);
            return fullPath1 == fullPath2;
        }

        internal static System.IO.FileSystemInfo GetFilesystemInfoForPath(string path)
        {
            // Check if path exists
            if (System.IO.File.Exists(path))
            {
                return new System.IO.FileInfo(path);
            }
            if (System.IO.Directory.Exists(path))
            {
                return new System.IO.DirectoryInfo(path);
            }
            return path.EndsWith(System.IO.Path.DirectorySeparatorChar) ? new System.IO.DirectoryInfo(path) : new System.IO.FileInfo(path);
        }
    }
}
