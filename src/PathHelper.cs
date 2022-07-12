using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
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
                var resolvedPaths = _cmdlet.GetResolvedProviderPathFromPSPath(path, out var providerInfo);

                //Check if the path if from the filesystem
                if (providerInfo.Name != "FileSystem")
                {
                    //Add the path to the set of non-filesystem paths
                    nonfilesystemPaths.Add(path);
                    continue;
                }

                //Go through each resolved path and add it to the list of entries
                foreach (var resolvedPath in resolvedPaths)
                {
                    //Get the prefix
                    string prefix = System.IO.Path.GetDirectoryName(resolvedPath) ?? String.Empty;

                    AddDescendentEntriesIfPathIsFolder(path: resolvedPath, prefix: prefix, entries: entries);

                    //Add an entry for the item
                    entries.Add(new ArchiveEntry(name: GetEntryName(path: resolvedPath, prefix: prefix), fullPath: resolvedPath));
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
                string unresolvedPath = _cmdlet.GetUnresolvedProviderPathFromPSPath(path);

                //Get the prefix of the path
                string prefix = System.IO.Path.GetDirectoryName(unresolvedPath) ?? String.Empty;

                // If unresolvedPath is not a file or folder, throw a path not found error
                // If it is a folder, add its descendents to the list of ArchiveEntry
                if (!AddDescendentEntriesIfPathIsFolder(path: unresolvedPath, prefix: prefix, entries: entries) && !System.IO.File.Exists(unresolvedPath)) ThrowPathNotFoundError(path);

                //Add an entry for the item
                entries.Add(new ArchiveEntry(name: GetEntryName(path: unresolvedPath, prefix: prefix), fullPath: unresolvedPath));
            }

            //Check for duplicate paths
            var duplicates = GetDuplicatePaths(entries);
            if (duplicates.Count() > 0) ThrowDuplicatePathsError(duplicates);

            //Return archive entries
            return entries;
        }

        private bool AddDescendentEntriesIfPathIsFolder(string path, string prefix, List<ArchiveEntry> entries)
        {
            if (System.IO.Directory.Exists(path))
            {
                //Get all descendents
                var childPaths = _cmdlet.InvokeProvider.ChildItem.GetNames(new string[] { path }, returnContainers: ReturnContainers.ReturnAllContainers,
                    recurse: true, depth: uint.MaxValue, force: true, literalPath: false);

                foreach (var childPath in childPaths)
                {
                    //Add an entry for each child path
                    entries.Add(new ArchiveEntry(name: GetEntryName(path: childPath, prefix: prefix), fullPath: childPath));
                }

                return true;
            }
            return false;
        }

        private string GetEntryName(string path, string prefix)
        {
            if (prefix == String.Empty) return path;

            //If the path does not start with the prefix, throw an exception
            if (!path.StartsWith(prefix))
            {
                throw new ArgumentException($"{path} does not begin with {prefix}");
            }

            string entryName = path.Substring(path.Length - prefix.Length + 1);

            //Normalize entryName to use forwardslashes instead of backslashes
            entryName.Replace(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

            //If the path is a folder, ensure entryName has a forwardslash at the end
            if (System.IO.Directory.Exists(path) && !path.EndsWith(System.IO.Path.AltDirectorySeparatorChar)) path += System.IO.Path.AltDirectorySeparatorChar;

            return entryName;
        }

        private IEnumerable<string> GetDuplicatePaths(List<ArchiveEntry> entries)
        {
            return entries.GroupBy(x => x.FullPath)
                    .Where(group => group.Count() > 1)
                    .Select(x => x.Key);
        }

        private void ThrowPathNotFoundError(string path)
        {
            var errorMsg = String.Format(ErrorMessages.PathNotFoundMessage, path);
            var exception = new System.InvalidOperationException(errorMsg);
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

        private void ThrowDuplicatePathsError(IEnumerable<string> paths)
        {
            string commaSeperatedPaths = String.Join(',', paths);
            var errorMsg = String.Format(ErrorMessages.DuplicatePathsMessage, commaSeperatedPaths);
            var exception = new System.InvalidOperationException(errorMsg);
            var errorRecord = new ErrorRecord(exception, "DuplicatePath", ErrorCategory.InvalidArgument, commaSeperatedPaths);
            _cmdlet.ThrowTerminatingError(errorRecord);
        }
    }
}
