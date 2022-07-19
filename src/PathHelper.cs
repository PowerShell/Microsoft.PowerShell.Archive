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
    //To-do: Add exception handling

    internal class PathHelper
    {
        private PSCmdlet _cmdlet;

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
            if (literalPath) return GetArchiveEntriesForLiteralPath(paths);
            else return GetArchiveEntriesForNonLiteralPaths(paths);
        }

        /// <summary>
        /// Get a list of ArchiveAddition objects from an array of paths by expanding wildcard characters in each path (if found).
        /// </summary>
        /// <param name="paths">See above</param>
        /// <returns>See summary</returns>
        private List<ArchiveAddition> GetArchiveEntriesForNonLiteralPaths(string[] paths)
        {
            List<ArchiveAddition> additions = new List<ArchiveAddition>();

            //Used to keep track of non-filesystem paths
            HashSet<string> nonfilesystemPaths = new HashSet<string>();

            foreach (var path in paths)
            {
                // Resolve the path
                // GetResolvedProviderPathFromPSPath should not return null even if the path does not exist, but if it does, something went horribly wrong, so throw an exception
                var resolvedPaths = GetResolvedProviderPathFromPSPath(path, out var providerInfo, mustExist: true) ?? throw new InvalidOperationException(message: Messages.GetResolvedPathFromPSPathProviderReturnedNullMessage);

                // Check if the path if from the filesystem
                if (providerInfo?.Name != "FileSystem")
                {
                    // If not, add the path to the set of non-filesystem paths. We will throw an error later so we can show the user all invalid paths at once
                    nonfilesystemPaths.Add(path);
                    continue;
                }

                // Check if the cmdlet can preserve paths based on path variable
                bool shouldPreservePathStructure = CanPreservePathStructure(path);

                // Go through each resolved path and add an ArchiveAddition for it to additions
                for (int i=0; i<resolvedPaths.Count; i++)
                {
                    var resolvedPath = resolvedPaths[i];
                    AddAdditionForFullyQualifiedPath(path: resolvedPath, additions: additions, shouldPreservePathStructure: shouldPreservePathStructure);
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
        /// Get a list of ArchiveAddition objects from an array of paths by NOT expanding wildcard characters.
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        private List<ArchiveAddition> GetArchiveEntriesForLiteralPath(string[] paths)
        {
            List<ArchiveAddition> additions = new List<ArchiveAddition>();

            foreach (var path in paths)
            {
                // Resolve the path -- gets the fully qualified path
                string fullyQualifiedPath = GetUnresolvedProviderPathFromPSPath(path);

                // Check if we can preserve the path structure -- this is based on the original path the user entered (not fully qualified)
                bool canPreservePathStructure = CanPreservePathStructure(path: path);

                // Add an ArchiveAddition for the path to the list of additions
                AddAdditionForFullyQualifiedPath(path: fullyQualifiedPath, additions: additions, shouldPreservePathStructure: canPreservePathStructure);
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

            // Return archive additions
            return additions;
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
                    ArchiveAddition.ArchiveAdditionType? type = null;
                    if (childPath is System.IO.FileInfo)
                    {
                        type = ArchiveAddition.ArchiveAdditionType.File;
                    }
                    if (childPath is System.IO.DirectoryInfo)
                    {
                        type = ArchiveAddition.ArchiveAdditionType.Directory;
                    } else
                    {
                        // Throw a terminating error if the childPath is neither a file or a directory -- seems impossible, but done for safety
                        // TODO: Throw an error
                    }
                    // Add an entry for each child path

                    additions.Add(new ArchiveAddition(entryName: GetEntryName(path: childPath.FullName, shouldPreservePathStructure: shouldPreservePathStructure), fullPath: childPath.Name));
                }
            }
            catch (System.Management.Automation.ItemNotFoundException itemNotFoundException)
            {
                //Throw a path not found error
                ErrorRecord errorRecord = new ErrorRecord(itemNotFoundException, "PathNotFound", ErrorCategory.InvalidArgument, path);
                _cmdlet.ThrowTerminatingError(errorRecord);
            }
        }

        private string GetEntryName(string path, bool shouldPreservePathStructure)
        {
            //If the path is relative to the current working directory, return the relative path as name
            if (shouldPreservePathStructure && IsPathRelativeToCurrentWorkingDirectory(path, out var relativePath))
            {
                return relativePath;
            }
            //Otherwise, return the name of the directory or file
            if (path.EndsWith(System.IO.Path.DirectorySeparatorChar))
            {
                //Get substring from second-last directory seperator char till end
                int secondLastIndex = path.LastIndexOf(System.IO.Path.DirectorySeparatorChar, path.Length - 2);
                if (secondLastIndex == -1) return path;
                else return path.Substring(secondLastIndex + 1);
            }
            else
            {
                return System.IO.Path.GetFileName(path);
            }
        }

        private string GetEntryName(string path, string prefix)
        {
            if (prefix == String.Empty) return path;

            //If the path does not start with the prefix, throw an exception
            if (!path.StartsWith(prefix))
            {
                throw new ArgumentException($"{path} does not begin with {prefix}");
            }

            if (path.Length <= prefix.Length) throw new ArgumentException($"The length of {path} is shorter than or equal to the length of {prefix}");

            string entryName = path.Substring(prefix.Length + 1);

            //Normalize entryName to use forwardslashes instead of backslashes
            entryName = entryName.Replace(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

            return entryName;
        }

        private IEnumerable<string> GetDuplicatePaths(List<ArchiveAddition> entries)
        {
            return entries.GroupBy(x => x.FullPath)
                    .Where(group => group.Count() > 1)
                    .Select(x => x.Key);
        }

        private System.Collections.ObjectModel.Collection<string>? GetResolvedProviderPathFromPSPath(string path, out ProviderInfo? providerInfo, bool mustExist)
        {
            try
            {
                ProviderInfo info;
                var resolvedPaths = _cmdlet.GetResolvedProviderPathFromPSPath(path, out info);
                providerInfo = info;
                return resolvedPaths;
            }
            catch (ProviderNotFoundException providerNotFoundException)
            {
                //Throw an invalid path error
                ThrowInvalidPathError(path, providerNotFoundException);
            }
            catch (System.Management.Automation.DriveNotFoundException driveNotFoundException)
            {
                ThrowInvalidPathError(path, driveNotFoundException);
            }
            catch (System.Management.Automation.ProviderInvocationException providerInvocationException)
            {
                ThrowInvalidPathError(path, providerInvocationException);
            }
            catch (NotSupportedException providerNotSupportedException)
            {
                ThrowInvalidPathError(path, providerNotSupportedException);
            } 
            catch (InvalidOperationException invalidOperationException)
            {
                ThrowInvalidPathError(path, invalidOperationException);
            } 
            catch (ItemNotFoundException itemNotFoundException)
            {
                if (mustExist) ThrowPathNotFoundError(path, itemNotFoundException);
            }
            providerInfo = null;
            return null;
        }

        private string? GetUnresolvedProviderPathFromPSPath(string path)
        {
            try
            {
                return _cmdlet.SessionState.Path.GetUnresolvedProviderPathFromPSPath(path);
            }
            catch (ProviderNotFoundException providerNotFoundException)
            {
                //Throw an invalid path error
                ThrowInvalidPathError(path, providerNotFoundException);
            }
            catch (System.Management.Automation.DriveNotFoundException driveNotFoundException)
            {
                ThrowInvalidPathError(path, driveNotFoundException);
            }
            catch (System.Management.Automation.ProviderInvocationException providerInvocationException)
            {
                ThrowInvalidPathError(path, providerInvocationException);
            }
            catch (NotSupportedException providerNotSupportedException)
            {
                ThrowInvalidPathError(path, providerNotSupportedException);
            }
            catch (InvalidOperationException invalidOperationException)
            {
                ThrowInvalidPathError(path, invalidOperationException);
            }
            return null;
        }

        // TODO: Add directory seperator char at end
        internal string ResolveToSingleFullyQualifiedPath(string path)
        {
            //path can be literal or non-literal
            //First, get non-literal path
            string nonLiteralPath = GetUnresolvedProviderPathFromPSPath(path) ?? throw new ArgumentException($"Path {path} was resolved to null");

            /*//Second, get literal path
            var literalPaths = GetResolvedProviderPathFromPSPath(path, out var providerInfo, mustExist: false);
            if (literalPaths != null)
            {
                // Ensure the literal paths came from the filesystem
                if (providerInfo != null && providerInfo?.Name != "FileSystem") ThrowInvalidPathError(path);

                // If there are >1 literalPaths, throw an error
                if (literalPaths.Count > 1) ThrowResolvesToMultiplePathsError(path);

                // If there is one item in literalPaths, compare it to nonLiteralPath
                if (literalPaths[0] != nonLiteralPath) ThrowResolvesToMultiplePathsError(path);
            }*/

            return nonLiteralPath;
        }

        private void ThrowPathNotFoundError(string path)
        {
            var errorMsg = String.Format(ErrorMessages.PathNotFoundMessage, path);
            var exception = new System.InvalidOperationException(errorMsg);
            var errorRecord = new ErrorRecord(exception, "PathNotFound", ErrorCategory.InvalidArgument, path);
            _cmdlet.ThrowTerminatingError(errorRecord);
        }

        private void ThrowPathNotFoundError(string path, Exception innerException)
        {
            var errorMsg = String.Format(ErrorMessages.PathNotFoundMessage, path);
            var exception = new System.ArgumentException(errorMsg);
            var errorRecord = new ErrorRecord(exception, "PathNotFound", ErrorCategory.InvalidArgument, path);
            _cmdlet.ThrowTerminatingError(errorRecord);
        }

        private void ThrowInvalidPathError(HashSet<string> paths)
        {
            string commaSeperatedPaths = String.Join(',', paths);
            var errorMsg = String.Format(ErrorMessages.InvalidPathMessage, commaSeperatedPaths);
            var exception = new System.InvalidOperationException(errorMsg);
            var errorRecord = new ErrorRecord(exception, "InvalidPath", ErrorCategory.InvalidArgument, commaSeperatedPaths);
            _cmdlet.ThrowTerminatingError(errorRecord);
        }

        private void ThrowInvalidPathError(string path)
        {
            var errorMsg = String.Format(ErrorMessages.InvalidPathMessage, path);
            var exception = new System.ArgumentException(errorMsg);
            var errorRecord = new ErrorRecord(exception, "InvalidPath", ErrorCategory.InvalidArgument, path);
            _cmdlet.ThrowTerminatingError(errorRecord);
        }

        private void ThrowInvalidPathError(string path, Exception innerException)
        {
            var errorMsg = String.Format(ErrorMessages.InvalidPathMessage, path);
            var exception = new System.ArgumentException(errorMsg, innerException);
            var errorRecord = new ErrorRecord(exception, "InvalidPath", ErrorCategory.InvalidArgument, path);
            _cmdlet.ThrowTerminatingError(errorRecord);
        }

        private void ThrowDuplicatePathsError(IEnumerable<string> paths)
        {
            string commaSeperatedPaths = String.Join(',', paths);
            var errorMsg = String.Format(ErrorMessages.DuplicatePathsMessage, commaSeperatedPaths);
            var exception = new System.InvalidOperationException(errorMsg);
            var errorRecord = new ErrorRecord(exception, "DuplicatePathFound", ErrorCategory.InvalidArgument, commaSeperatedPaths);
            _cmdlet.ThrowTerminatingError(errorRecord);
        }

        private void ThrowResolvesToMultiplePathsError(string path)
        {
            var errorMsg = String.Format(ErrorMessages.PathResolvesToMultiplePathsMessage, path);
            var exception = new System.ArgumentException(errorMsg);
            var errorRecord = new ErrorRecord(exception, "DuplicatePathFound", ErrorCategory.InvalidArgument, path);
            _cmdlet.ThrowTerminatingError(errorRecord);
        }

        private bool CanPreservePathStructure(string path)
        {
            return System.IO.Path.IsPathRooted(path);
        }

        private bool IsPathRelativeToCurrentWorkingDirectory(string path, out string relativePath)
        {
            // TODO: Add exception handling
            relativePath = System.IO.Path.GetRelativePath(_cmdlet.SessionState.Path.CurrentFileSystemLocation.Path, path);
            return !relativePath.Contains("..");
        }
    }
}
