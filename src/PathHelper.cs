// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.Archive.Localized;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;

namespace Microsoft.PowerShell.Archive
{
    internal class PathHelper
    {
        private readonly PSCmdlet _cmdlet;

        private const string FileSystemProviderName = "FileSystem";

        internal PathHelper(PSCmdlet cmdlet)
        {
            _cmdlet = cmdlet;
        }

        internal List<ArchiveAddition> GetArchiveAdditions(HashSet<string> fullyQualifiedPaths)
        {
            List<ArchiveAddition> archiveAdditions = new List<ArchiveAddition>(fullyQualifiedPaths.Count);
            foreach (var path in fullyQualifiedPaths)
            {
                // Assume each path is valid, fully qualified, and existing
                Debug.Assert(Path.Exists(path));
                Debug.Assert(Path.IsPathFullyQualified(path));
                AddAdditionForFullyQualifiedPath(path, archiveAdditions);
            }
            return archiveAdditions;
        }

        /// <summary>
        /// Adds an ArchiveAddition object for a path to a list of ArchiveAddition objects
        /// </summary>
        /// <param name="path">The fully qualified path</param>
        /// <param name="additions">The list where to add the ArchiveAddition object for the path</param>
        /// <param name="shouldPreservePathStructure">If true, relative path structure will be preserved. If false, relative path structure will NOT be preserved.</param>
        private void AddAdditionForFullyQualifiedPath(string path, List<ArchiveAddition> additions)
        {
            Debug.Assert(Path.Exists(path));
            FileSystemInfo fileSystemInfo;

            if (Directory.Exists(path))
            {
                // If the path is a directory, ensure it does not have a trailing DirectorySeperatorChar and if it does, remove it
                // This will make path comparisons easier because the cmdlet won't have to consider whether or not a path has a DirectorySeperatorChar at the end
                // i.e., we will avoid the scenario where 1 path has a trailing DirectorySeperatorChar and the other does not (this is not a big deal, but makes life easier)
                if (path.EndsWith(Path.DirectorySeparatorChar)) {
                    path = path.Substring(0, path.Length - 1);
                }
                fileSystemInfo = new DirectoryInfo(path);
            }
            else
            {
                fileSystemInfo = new FileInfo(path);
            }

            // Get the entry name of the file or directory in the archive
            // The cmdlet will preserve the directory structure as long as the path is relative to the working directory
            var entryName = GetEntryName(fileSystemInfo, out bool doesPreservePathStructure);
            additions.Add(new ArchiveAddition(entryName: entryName, fileSystemInfo: fileSystemInfo));

            // Recurse through the child items and add them to additions
            if (fileSystemInfo.Attributes.HasFlag(FileAttributes.Directory) && fileSystemInfo is DirectoryInfo directoryInfo) {
                AddDescendentEntries(directoryInfo: directoryInfo, additions: additions, shouldPreservePathStructure: doesPreservePathStructure);
            }
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
                foreach (var childFileSystemInfo in directoryInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    string entryName;
                    // If the cmdlet should preserve the path structure, then use the relative path
                    if (shouldPreservePathStructure)
                    {
                        entryName = GetEntryName(childFileSystemInfo, out bool doesPreservePathStructure);
                        Debug.Assert(doesPreservePathStructure);
                    }
                    // Otherwise, get the entry name using the prefix 
                    else
                    {
                        entryName = GetEntryNameUsingPrefix(path: childFileSystemInfo.FullName, prefix: pathPrefix);
                    }
                    // Add an entry for each descendent of the directory                 
                    additions.Add(new ArchiveAddition(entryName: entryName, fileSystemInfo: childFileSystemInfo));
                }
            } 
            // Write a non-terminating error if a securityException occurs
            catch (System.Security.SecurityException securityException)
            {
                var errorId = nameof(ErrorCode.InsufficientPermissionsToAccessPath);
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
        private string GetEntryName(FileSystemInfo fileSystemInfo, out bool doesPreservePathStructure)
        {
            string entryName;
            doesPreservePathStructure = false;
            // If the path is relative to the current working directory, return the relative path as name
            if (TryGetPathRelativeToCurrentWorkingDirectory(path: fileSystemInfo.FullName, out var relativePath))
            {
                Debug.Assert(relativePath is not null);
                doesPreservePathStructure = true;
                entryName = relativePath;
            }
            // Otherwise, return the name of the directory or file
            else 
            {
                entryName = fileSystemInfo.Name;
            }
            
            if (fileSystemInfo.Attributes.HasFlag(FileAttributes.Directory) && !entryName.EndsWith(Path.DirectorySeparatorChar))
            {
                entryName += System.IO.Path.DirectorySeparatorChar;
            }


            return entryName;
        }

        /// <summary>
        /// Determines the entry name of a file or directory based on its path and a prefix.
        /// The prefix is removed from the path and the remaining portion is the entry name.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private static string GetEntryNameUsingPrefix(string path, string prefix)
        {
            if (prefix == string.Empty) return path;

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

        /// <summary>
        /// Gets the prefix from the path to a directory. This prefix is necessary to determine the entry names of all
        /// descendents of the directory.
        /// </summary>
        /// <param name="directoryInfo"></param>
        /// <returns></returns>
        private static string GetPrefixForPath(System.IO.DirectoryInfo directoryInfo)
        {
            // Get the parent directory of the path
            if (directoryInfo.Parent is null)
            {
                return string.Empty;
            }
            var prefix = directoryInfo.Parent.FullName;
            if (!prefix.EndsWith(System.IO.Path.DirectorySeparatorChar))
            {
                prefix += System.IO.Path.DirectorySeparatorChar;
            }
            return prefix;
        }
        
        /// <summary>
        /// Tries to get a path relative to the current working directory as long as the relative path does not contain ".."
        /// </summary>
        /// <param name="path"></param>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        private bool TryGetPathRelativeToCurrentWorkingDirectory(string path, out string? relativePathToWorkingDirectory)
        {
            Debug.Assert(!string.IsNullOrEmpty(path));
            string? workingDirectoryRoot = Path.GetPathRoot(_cmdlet.SessionState.Path.CurrentFileSystemLocation.Path);
            string? pathRoot = Path.GetPathRoot(path);
            if (workingDirectoryRoot != pathRoot) {
                relativePathToWorkingDirectory = null;
                return false;
            }
            string relativePath = Path.GetRelativePath(_cmdlet.SessionState.Path.CurrentFileSystemLocation.Path, path);
            relativePathToWorkingDirectory = relativePath.Contains("..") ? null : relativePath;
            return relativePathToWorkingDirectory is not null;
        }

        internal System.Collections.ObjectModel.Collection<string>? GetResolvedPathFromPSProviderPath(string path, HashSet<string> nonexistentPaths) {
            // Keep the exception at the top, then when an error occurs, use the exception to create an ErrorRecord
            Exception? exception = null;
            System.Collections.ObjectModel.Collection<string>? fullyQualifiedPaths = null;
            try
            {
                // Resolve path
                var resolvedPaths = _cmdlet.SessionState.Path.GetResolvedProviderPathFromPSPath(path, out var providerInfo);

                // If the path is from the filesystem, set it to fullyQualifiedPaths so it can be returned
                // Otherwise, create an exception so an error will be written
                if (providerInfo.Name != FileSystemProviderName)
                {
                    var exceptionMsg = ErrorMessages.GetErrorMessage(ErrorCode.InvalidPath);
                    exception = new ArgumentException(exceptionMsg);
                } else {
                    fullyQualifiedPaths = resolvedPaths;
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
            // If a path can't be found, write an error
            catch (System.Management.Automation.ItemNotFoundException)
            {
                nonexistentPaths.Add(path);
            }

            // If an exception was caught, write a non-terminating error
            if (exception is not null)
            {
                var errorRecord = new ErrorRecord(exception: exception, errorId: nameof(ErrorCode.InvalidPath), errorCategory: ErrorCategory.InvalidArgument, 
                    targetObject: path);
                _cmdlet.WriteError(errorRecord);
            }

            return fullyQualifiedPaths;
        }

        // Resolves a literal path. Does not check if the path exists.
        // If an exception occurs with a provider, it throws a terminating error
        internal string? GetUnresolvedPathFromPSProviderPath(string path) {
            // Keep the exception at the top, then when an error occurs, use the exception to create an ErrorRecord
            Exception? exception = null;
            string? fullyQualifiedPath = null;
            try
            {
                // Resolve path
                var resolvedPath = _cmdlet.SessionState.Path.GetUnresolvedProviderPathFromPSPath(path, out var providerInfo, out var psDriveInfo);

                // If the path is from the filesystem, set fullyQualifiedPath to resolvedPath so it can be returned
                // Otherwise, create an exception so an error will be written
                if (providerInfo.Name != FileSystemProviderName)
                {
                    var exceptionMsg = ErrorMessages.GetErrorMessage(ErrorCode.InvalidPath);
                    exception = new ArgumentException(exceptionMsg);
                }
                else
                {
                    fullyQualifiedPath = resolvedPath;
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

            // If an exception was caught, write a non-terminating error of throwError == false. Otherwise, throw a terminating errror
            if (exception is not null)
            {
                var errorRecord = new ErrorRecord(exception: exception, errorId: nameof(ErrorCode.InvalidPath), errorCategory: ErrorCategory.InvalidArgument, 
                    targetObject: path);
                _cmdlet.ThrowTerminatingError(errorRecord);
            }

            return fullyQualifiedPath;
        }

        // Resolves a literal path. If it does not exist, it adds the path to nonexistentPaths.
        // If an exception occurs with a provider, it writes a non-terminating error
        internal string? GetUnresolvedPathFromPSProviderPath(string path, HashSet<string> nonexistentPaths) {
            // Keep the exception at the top, then when an error occurs, use the exception to create an ErrorRecord
            Exception? exception = null;
            string? fullyQualifiedPath = null;
            try
            {
                // Resolve path
                var resolvedPath = _cmdlet.SessionState.Path.GetUnresolvedProviderPathFromPSPath(path, out var providerInfo, out var psDriveInfo);

                // If the path is from the filesystem, set fullyQualifiedPath to resolvedPath so it can be returned
                // Otherwise, create an exception so an error will be written
                if (providerInfo.Name != FileSystemProviderName)
                {
                    var exceptionMsg = ErrorMessages.GetErrorMessage(ErrorCode.InvalidPath);
                    exception = new ArgumentException(exceptionMsg);
                }
                // If the path does not exist, create an exception 
                else if (!Path.Exists(resolvedPath)) {
                    nonexistentPaths.Add(resolvedPath);
                }
                else
                {
                    fullyQualifiedPath = resolvedPath;
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

            // If an exception was caught, write a non-terminating error
            if (exception is not null)
            {
                var errorRecord = new ErrorRecord(exception: exception, errorId: nameof(ErrorCode.InvalidPath), errorCategory: ErrorCategory.InvalidArgument, 
                    targetObject: path);
                _cmdlet.WriteError(errorRecord);
            }

            return fullyQualifiedPath;
        }
    }
}
