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

        internal List<ArchiveEntry> GetEntryRecordsForPath(string[] paths, bool literalPath)
        {
            if (literalPath) return GetArchiveEntriesForLiteralPath(paths);
            else return GetArchiveEntriesForNonLiteralPaths(paths);
        }

        private List<ArchiveEntry> GetArchiveEntriesForNonLiteralPaths(string[] paths)
        {
            List<ArchiveEntry> entries = new List<ArchiveEntry>();

            //Used to keep track of non-filesystem paths
            HashSet<string> nonfilesystemPaths = new HashSet<string>();

            foreach (var path in paths)
            {
                //Resolve the path
                var resolvedPaths = GetResolvedProviderPathFromPSPath(path, out var providerInfo, mustExist: true);

                //Check if the path if from the filesystem
                if (providerInfo?.Name != "FileSystem")
                {
                    //Add the path to the set of non-filesystem paths
                    nonfilesystemPaths.Add(path);
                    continue;
                }

                //Check if the entered path is relative to the current working directory
                bool shouldPreservePathStructure = CanPreservePathStructure(path);

                //Go through each resolved path and add it to the list of entries
                for (int i=0; i<resolvedPaths.Count; i++)
                {
                    var resolvedPath = resolvedPaths[i];
                    //Get the prefix
                    //string prefix = System.IO.Path.GetDirectoryName(resolvedPath) ?? String.Empty;

                    AddEntryForFullyQualifiedPath(resolvedPath, entries, shouldPreservePathStructure);
                }
            }

            //Throw an invalid path error
            if (nonfilesystemPaths.Count > 0) ThrowInvalidPathError(nonfilesystemPaths);

            //Check for duplicate paths
            var duplicates = GetDuplicatePaths(entries);
            if (duplicates.Count() > 0) ThrowDuplicatePathsError(duplicates);

            return entries;
        }

        private List<ArchiveEntry> GetArchiveEntriesForLiteralPath(string[] paths)
        {
            List<ArchiveEntry> entries = new List<ArchiveEntry>();

            foreach (var path in paths)
            {
                //Get the unresolved path
                string unresolvedPath = GetUnresolvedProviderPathFromPSPath(path);

                // TODO: Factor out this part -- adding an entry

                //Get the prefix of the path
                string prefix = System.IO.Path.GetDirectoryName(unresolvedPath) ?? String.Empty;

                // If unresolvedPath is not a file or folder, throw a path not found error
                // If it is a folder, add its descendents to the list of ArchiveEntry
                if (System.IO.Directory.Exists(unresolvedPath))
                {
                    //Add directory seperator to end 
                    if (!unresolvedPath.EndsWith(System.IO.Path.DirectorySeparatorChar)) unresolvedPath += System.IO.Path.DirectorySeparatorChar;
                    AddDescendentEntries(path: unresolvedPath, entries: entries, shouldPreservePathStructure: true);
                } else if (!System.IO.File.Exists(unresolvedPath))
                {
                    ThrowPathNotFoundError(path);
                }

                //Add an entry for the item
                entries.Add(new ArchiveEntry(name: GetEntryName(path: unresolvedPath, prefix: prefix), fullPath: unresolvedPath));
            }

            //Check for duplicate paths
            var duplicates = GetDuplicatePaths(entries);
            if (duplicates.Count() > 0) ThrowDuplicatePathsError(duplicates);

            //Return archive entries
            return entries;
        }

        private void AddEntryForFullyQualifiedPath(string path, List<ArchiveEntry> entries, bool shouldPreservePathStructure)
        {
            // If unresolvedPath is not a file or folder, throw a path not found error
            // If it is a folder, add its descendents to the list of ArchiveEntry
            if (System.IO.Directory.Exists(path))
            {
                //Add directory seperator to end 
                if (!path.EndsWith(System.IO.Path.DirectorySeparatorChar)) path += System.IO.Path.DirectorySeparatorChar;
                AddDescendentEntries(path: path, entries: entries, shouldPreservePathStructure: shouldPreservePathStructure);
            }
            else if (!System.IO.File.Exists(path))
            {
                ThrowPathNotFoundError(path);
            }

            //Add an entry for the item
            entries.Add(new ArchiveEntry(name: GetEntryName(path: path, shouldPreservePathStructure: shouldPreservePathStructure), fullPath: path));
        }

        private void AddDescendentEntries(string path, List<ArchiveEntry> entries, bool shouldPreservePathStructure)
        {
            try
            {
                var directoryInfo = new System.IO.DirectoryInfo(path);
                foreach (var childPath in directoryInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    //Add an entry for each child path
                    entries.Add(new ArchiveEntry(name: GetEntryName(path: childPath.FullName, shouldPreservePathStructure: shouldPreservePathStructure), fullPath: childPath.Name));
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

        private IEnumerable<string> GetDuplicatePaths(List<ArchiveEntry> entries)
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

            //Second, get literal path
            var literalPaths = GetResolvedProviderPathFromPSPath(path, out var providerInfo, mustExist: false);
            if (literalPaths != null)
            {
                //Ensure the literal paths came from the filesystem
                if (providerInfo != null && providerInfo?.Name != "FileSystem") ThrowInvalidPathError(path);

                //If there are >1 literalPaths, throw an error
                if (literalPaths.Count > 1) ThrowResolvesToMultiplePathsError(path);

                //If there is one item in literalPaths, compare it to nonLiteralPath
                if (literalPaths[0] != nonLiteralPath) ThrowResolvesToMultiplePathsError(path);
            }

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
