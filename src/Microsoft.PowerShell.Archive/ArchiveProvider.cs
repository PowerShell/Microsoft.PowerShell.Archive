using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Linq;
using System.Text;

using Microsoft.PowerShell.Commands;

namespace Microsoft.PowerShell.Archive
{
    #region ArchiveProvider : IContentReader, IContentWriter
    [CmdletProvider(ArchiveProvider.ProviderName, ProviderCapabilities.ShouldProcess | ProviderCapabilities.ExpandWildcards )]
    public class ArchiveProvider :  NavigationCmdletProvider, IContentCmdletProvider
    {

        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        public const string ProviderName = "Microsoft.PowerShell.Archive";

        // Workaround for internal class objects
        internal InvocationInfo Context_MyInvocation {
            get {
                return (InvocationInfo)SessionState.PSVariable.Get("MyInvocation").Value;
            }
        }

        internal ArchivePSDriveInfo ArchiveDriveInfo {
            get {
                if (_psDriveInfo != null)
                {
                    return _psDriveInfo;
                }
                return (PSDriveInfo as ArchivePSDriveInfo);
            }
            private set
            {
                _psDriveInfo = value;
            }
        }

        internal ArchivePSDriveInfo _psDriveInfo;

        /// <summary>
        /// Initializes a new instance of the FileSystemProvider class. Since this
        /// object needs to be stateless, the constructor does nothing.
        /// </summary>
        public ArchiveProvider()
        {

        }

        // Todo: private Collection<WildcardPattern> _excludeMatcher = null;

        /// <summary>
        /// Converts all / in the path to \
        /// </summary>
        ///
        /// <param name="path">
        /// The path to normalize.
        /// </param>
        ///
        /// <returns>
        /// The path with all / normalized to \
        /// and resolve the path based off of its Root/Name
        /// </returns>
        private string NormalizePath(string path)
        {

            // [Bug] PSDriveInfo sometimes does not get instantiated with the provider
            // this causes stateful issues with complex providers.
            // Example Duplication of this issue
            //
            // ./<tabkey>
            // and 
            // Get-Item $FileName | Remove-Item
            //
            // Current Workaround searches all Drives with ProviderName
            // and checks relative path and overrides the path lookup.
            
            if (PSDriveInfo == null) {
                if (path.Contains(Path.VolumeSeparatorChar))
                {
                    SessionState.Drive.GetAllForProvider(ProviderName).ToList().ForEach( i => {
                        if ( (path.StartsWith(i.Root)) || (path.StartsWith(i.Name)) )
                        {
                            ArchiveDriveInfo = (i as ArchivePSDriveInfo);
                        }
                    });
                }
            }

            // Null or empty should return null or empty
            if (String.IsNullOrEmpty(path))
            {
                return path;
            }

            if (path.IndexOfAny(Path.GetInvalidPathChars()) != -1)
            {
                TraceSource.NewArgumentException(ArchiveProviderStrings.PathContainsInvalidCharacters);
            }

            if (path.StartsWith($"{ArchiveDriveInfo.Root}"))
            {
                path = path.Remove(0, ArchiveDriveInfo.Root.Length);
            }
            else if (path.StartsWith($"{ArchiveDriveInfo.Name}:") )
            {
                path = path.Remove(0, ArchiveDriveInfo.Name.Length+1);
            }

            path = path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            path = path.TrimStart(Path.AltDirectorySeparatorChar);            

            // Before returning a normalized path
            return path;
        }

        // Todo: private static FileSystemInfo GetFileSystemInfo(string path, ref bool isContainer)
        // Todo: protected override object GetChildNamesDynamicParameters(string path)
        // Todo: protected override object GetChildItemsDynamicParameters(string path, bool recurse)
        // Todo: protected override object CopyItemDynamicParameters(string path, string destination, bool recurse)
        #region ICmdletProviderSupportsHelp members
        // Todo: public string GetHelpMaml(string helpItemName, string path)
        #endregion
        #region CmdletProvider members
        // Todo: protected override ProviderInfo Start(ProviderInfo providerInfo)
        #endregion CmdletProvider members
        #region DriveCmdletProvider members

        /// <summary>
        /// Determines if the specified drive can be mounted.
        /// </summary>
        ///
        /// <param name="drive">
        /// The drive that is going to be mounted.
        /// </param>
        ///
        /// <returns>
        /// The same drive that was passed in, if the drive can be mounted.
        /// null if the drive cannot be mounted.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// drive is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// drive root is null or empty.
        /// </exception>
		protected override PSDriveInfo NewDrive(PSDriveInfo drive)
		{
            // verify parameters

            if (drive == null)
            {
                throw TraceSource.NewArgumentNullException("drive");
            }

            if (String.IsNullOrEmpty(drive.Root))
            {
                throw TraceSource.NewArgumentException("drive.Root");
            }

            FileInfo archiveInfo = new FileInfo(drive.Root);

			if (!File.Exists(archiveInfo.FullName))
			{
				throw new Exception("file not found");
			}

            drive = new PSDriveInfo(drive.Name, drive.Provider, archiveInfo.FullName, drive.Description, drive.Credential, drive.DisplayRoot);
            ArchivePSDriveInfo newDrive = new ArchivePSDriveInfo(drive);

            // Build folder paths on initialize
            newDrive.buildFolderPaths();

            return base.NewDrive( newDrive );
		}

        // Todo: private void MapNetworkDrive(PSDriveInfo drive)
        // Todo: private void WinMapNetworkDrive(PSDriveInfo drive)
        // Todo: private bool IsNetworkMappedDrive(PSDriveInfo drive)
        // Todo: protected override PSDriveInfo RemoveDrive(PSDriveInfo drive)
        // Todo: private PSDriveInfo WinRemoveDrive(PSDriveInfo drive)
        // Todo: private bool IsSupportedDriveForPersistence(PSDriveInfo drive)
        // Todo: private static string WinGetUNCForNetworkDrive(string driveName)
        // Todo: private static string WinGetSubstitutedPathForNetworkDosDevice(string driveName)
        // Todo: protected override Collection<PSDriveInfo> InitializeDefaultDrives()
        #endregion DriveCmdletProvider methods
        #region ItemCmdletProvider methods
        // Todo: protected override object GetItemDynamicParameters(string path)

        /// <summary>
        /// Determines if the specified path is syntactically and semantically valid.
        /// An example path looks like this
        ///     C:\WINNT\Media\chimes.wav.
        /// </summary>
        /// <param name="path">
        /// The fully qualified path to validate.
        /// </param>
        /// <returns>
        /// True if the path is valid, false otherwise.
        /// </returns>
        protected override bool IsValidPath(string path)
		{
            // Path passed should be fully qualified path.

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            // Normalize the path
            path = NormalizePath(path);
            // path = EnsureDriveIsRooted(path);

            // Make sure the path is either drive rooted or UNC Path
            if (!IsAbsolutePath(path) && !PathIsUnc(path))
            {
                return false;
            }

            // Exceptions should only deal with exceptional circumstances,
            // but unfortunately, FileInfo offers no Try() methods that
            // let us check if we _could_ open the file.
            try
            {
                ArchiveItemInfo testFile = new ArchiveItemInfo(ArchiveDriveInfo, path);
            }
            catch (Exception e)
            {
                if ((e is ArgumentNullException) ||
                    (e is ArgumentException) ||
                    (e is System.Security.SecurityException) ||
                    (e is UnauthorizedAccessException) ||
                    (e is PathTooLongException) ||
                    (e is NotSupportedException))
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
			return false;
		}

        /// <summary>
        /// Expand a provider path that contains wildcards to a list of provider paths that the
        /// path represents. Only called for providers that declare the ExpandWildcards capability.
        /// </summary>
        ///
        /// <param name="path">
        /// The path to expand. Expansion must be consistent with the wildcarding rules of PowerShell's WildcardPattern class.
        /// </param>
        /// 
        /// <returns>
        /// A list of provider paths that this path expands to. They must all exist.
        /// </returns>
        ///
        protected override string[] ExpandPath(string path)
        {
            path = NormalizePath(path);
            IEnumerable<ArchiveItemInfo> ArchiveItemInfoList = ArchiveDriveInfo.GetItem(path, true, true);
            return ArchiveItemInfoList.Select(i => i.FullName).ToArray();
        }
        
        /// <summary>
        /// Gets the item at the specified path.
        /// </summary>
        /// <param name="path">
        /// A fully qualified path representing a file or directory in the
        /// file system.
        /// </param>
        /// <returns>
        /// Nothing.  FileInfo and DirectoryInfo objects are written to the
        /// context's pipeline.
        /// </returns>
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        /// </exception>
        protected override void GetItem(string path)
        {

            path = NormalizePath(path);

            // Validate the argument
            bool isContainer = false;

            if (string.IsNullOrEmpty(path))
            {
                // The parameter was null, throw an exception
                throw TraceSource.NewArgumentException("path");
            }

            try
            {
                
                IEnumerable<ArchiveItemInfo> result = ArchiveDriveInfo.GetItem(path, true, true);

                if (result != null)
                {
                    // Otherwise, return the item itself.
                    foreach (ArchiveItemInfo i in result) {
                        WriteItemObject(i, i.FullName, isContainer);
                    }
                    //
                    //WriteItemObject(result, path, )
                }
                else
                {
                    string error = String.Format(ArchiveProviderStrings.ItemNotFound, path);
                    Exception e = new IOException(error);
                    WriteError(new ErrorRecord(
                        e,
                        "ItemNotFound",
                        ErrorCategory.ObjectNotFound,
                        path));
                }
            }
            catch (IOException ioError)
            {
                // IOException contains specific message about the error occured and so no need for errordetails.
                ErrorRecord er = new ErrorRecord(ioError, "GetItemIOError", ErrorCategory.ReadError, path);
                WriteError(er);
            }
            catch (UnauthorizedAccessException accessException)
            {
                WriteError(new ErrorRecord(accessException, "GetItemUnauthorizedAccessError", ErrorCategory.PermissionDenied, path));
            }
        }

        // Todo: private FileSystemInfo GetFileSystemItem(string path, ref bool isContainer, bool showHidden)

        /// <summary>
        /// Invokes the item at the path using ShellExecute semantics.
        /// </summary>
        ///
        /// <param name="path">
        /// The item to invoke.
        /// </param>
        ///
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        /// </exception>
        protected override void InvokeDefaultAction(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                throw TraceSource.NewArgumentException("path");
            }

            path = NormalizePath(path);

            string action = ArchiveProviderStrings.InvokeItemAction;

            string resource = String.Format(ArchiveProviderStrings.InvokeItemResourceFileTemplate, path);

            if (ShouldProcess(resource, action))
            {
                var invokeProcess = new System.Diagnostics.Process();
                invokeProcess.StartInfo.FileName = path;

                bool invokeDefaultProgram = false;


                if (IsItemContainer(path))
                {

                    // Path points to a directory. We have to use xdg-open/open on Linux/macOS.
                    invokeDefaultProgram = true;
                    path = ArchiveDriveInfo.Root;
                }
                else if (Path.GetExtension(path) == ".ps1") {
                    
                    IEnumerable<ArchiveItemInfo> archiveItemInfoList = ArchiveDriveInfo.GetItem(path, false, true);
                    Object[] scriptargs = null;
                    foreach (ArchiveItemInfo archiveItemInfo in archiveItemInfoList)
                    {
                        string script = archiveItemInfo.ReadToEnd();
                        ScriptBlock scriptBlock = ScriptBlock.Create(script);
                        var result = SessionState.InvokeCommand.InvokeScript(SessionState, scriptBlock, scriptargs);
                        WriteItemObject(result, archiveItemInfo.FullName, false);
                    }
                }

                if (invokeDefaultProgram)
                {
                    const string quoteFormat = "\"{0}\"";
                                       
                    if (Platform.IsLinux) {
                        invokeProcess.StartInfo.FileName = "xdg-open";
                        invokeProcess.StartInfo.Arguments = path;
                    }
                    if (Platform.IsMacOS) {
                        invokeProcess.StartInfo.FileName = "open";
                        invokeProcess.StartInfo.Arguments = path;
                    }
                    if (Platform.IsWindows)
                    {
                        // Use ShellExecute when it's not a headless SKU
                        // 
                        invokeProcess.StartInfo.UseShellExecute = Platform.IsWindowsDesktop;
                        invokeProcess.StartInfo.FileName = path;
                    }
                    //if (NativeCommandParameterBinder.NeedQuotes(path))
                    {
                        // Assume true
                        path = string.Format(CultureInfo.InvariantCulture, quoteFormat, path);
                    }
                    invokeProcess.Start();
                }
            }
        } // InvokeDefaultAction
        #endregion ItemCmdletProvider members
        #region ContainerCmdletProvider members
        #region GetChildItems
        /// <summary>
        /// Gets the child items of a given directory.
        /// </summary>
        ///
        /// <param name="path">
        /// The full path of the directory to enumerate.
        /// </param>
        ///
        /// <param name="recurse">
        /// If true, recursively enumerates the child items as well.
        /// </param>
        ///
        /// <param name="depth">
        /// Limits the depth of recursion; uint.MaxValue performs full recursion.
        /// </param>
        ///
        /// <returns>
        /// Nothing.  FileInfo and DirectoryInfo objects that match the filter are written to the
        /// context's pipeline.
        /// </returns>
        ///
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        /// </exception>
        protected override void GetChildItems(
            string path,
            bool recurse,
            uint depth)
        {
            GetPathItems(path, recurse, depth, false, ReturnContainers.ReturnMatchingContainers);
        } // GetChildItems
        #endregion GetChildItems
        #region GetChildNames
        /// <summary>
        /// Gets the path names for all children of the specified
        /// directory that match the given filter.
        /// </summary>
        ///
        /// <param name="path">
        /// The full path of the directory to enumerate.
        /// </param>
        ///
        /// <param name="returnContainers">
        /// Determines if all containers should be returned or only those containers that match the
        /// filter(s).
        /// </param>
        ///
        /// <returns>
        /// Nothing.  Child names are written to the context's pipeline.
        /// </returns>
        ///
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        /// </exception>
        protected override void GetChildNames(
            string path,
            ReturnContainers returnContainers)
        {
            GetPathItems(path, false, uint.MaxValue, true, returnContainers);
        } // GetChildNames

        #endregion GetChildNames
        protected override bool ConvertPath(
            string path,
            string filter,
            ref string updatedPath,
            ref string updatedFilter)
        {
            // In order to support Wildcards?
            WriteWarning($"ConvertPath ({path}, {filter})");

            // Don't handle full paths, paths that the user is already trying to
            // filter, or paths they are trying to escape.
            if ((!string.IsNullOrEmpty(filter)) ||
                (path.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)) ||
                (path.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal)) ||
                (path.Contains("`"))
                )
            {
                return false;
            }

            // We can never actually modify the PowerShell path, as the
            // Win32 filtering support returns items that match the short
            // filename OR long filename.
            //
            // This creates tons of seemingly incorrect matches, such as:
            //
            // *~*:   Matches any file with a long filename
            // *n*:   Matches all files with a long filename, but have been
            //        mapped to a [6][~n].[3] disambiguation bucket
            // *.abc: Matches all files that have an extension that begins
            //        with ABC, since their extension is truncated in the
            //        short filename
            // *.*:   Matches all files and directories, even if they don't
            //        have a dot in their name

            // Our algorithm here is pretty simple. The filesystem can handle
            // * and ? in PowerShell wildcards, just not character ranges [a-z].
            // We replace character ranges with the single-character wildcard, '?'.
            updatedPath = path;
            updatedFilter = System.Text.RegularExpressions.Regex.Replace(path, "\\[.*?\\]", "?");
            WriteWarning($"ConvertPath ({updatedPath}, {updatedFilter})");
            return true;
        }

        private void GetPathItems(
            string path,
            bool recurse,
            uint depth,
            bool nameOnly,
            ReturnContainers returnContainers)
        {

            // Verify parameters
            if (String.IsNullOrEmpty(path))
            {
                throw TraceSource.NewArgumentException("path");
            }

            bool isDirectory = IsItemContainer(path);
            bool exists = ItemExists(path);
            
            path = NormalizePath(path);

            if (IsItemContainer(path))
            {
                path += Path.AltDirectorySeparatorChar;
            }

            if (exists)
            {
                //path = String.IsNullOrEmpty(path) || !path.StartsWith(ArchiveDriveInfo.Name) ? $"{ArchiveDriveInfo.Name}:\\{path}" : path;

                if (isDirectory)
                {
                    if (!path.Contains("*"))
                    {
                        path += "*";
                    }

                    path = path.TrimStart(Path.AltDirectorySeparatorChar);
                    
                    //Console.WriteLine($"GetPathItems '{path}'");
                    // Only the Root directory is looked at for this scenario. 
                    List<ArchiveItemInfo> fileInfoItems = ArchiveDriveInfo.GetItem(path, true, true).ToList();

                    if (fileInfoItems.Count == 0)
                    {
                        return;
                    }

                    // Sort the files
                    fileInfoItems = fileInfoItems.OrderBy(c => c.FullName, StringComparer.CurrentCultureIgnoreCase).ToList();


                    foreach (ArchiveItemInfo fileInfo in fileInfoItems)
                    {
                        if (nameOnly)
                        {
                            WriteItemObject(
                                fileInfo.Name,
                                fileInfo.FullName,
                                fileInfo.IsContainer);
                        }
                        else
                        {
                            WriteItemObject(fileInfo, fileInfo.FullName, fileInfo.IsContainer);
                        }
                    }
                    
                }
                else
                {
                    // Maybe the path is a file name so try a FileInfo instead
                    ArchiveItemInfo fileInfo = new ArchiveItemInfo(ArchiveDriveInfo, path);

                    if (nameOnly)
                    {
                        WriteItemObject(
                            fileInfo.Name,
                            fileInfo.FullName,
                            false);
                    }
                    else
                    {
                        WriteItemObject(fileInfo, fileInfo.FullName, false);
                    }

                }

            }
            else
            {
                Console.WriteLine("Please help me out. Submit an issue with what you did in order to get this to trigger");
                Console.WriteLine("https://github.com/romero126/PS1C");

                String error = String.Format(ArchiveProviderStrings.ItemDoesNotExist, path);
                Exception e = new IOException(error);
                WriteError(new ErrorRecord(
                    e,
                    "ItemDoesNotExist",
                    ErrorCategory.ObjectNotFound,
                    path));
                return;
            }
        }

        // Todo: private void Dir(
        // Todo: private FlagsExpression<FileAttributes> FormatAttributeSwitchParameters()
        // Todo: public static string Mode(PSObject instance)
        #region RenameItem
        /// <summary>
        /// Renames a file or directory.
        /// </summary>
        ///
        /// <param name="path">
        /// The current full path to the file or directory.
        /// </param>
        ///
        /// <param name="newName">
        /// The new full path to the file or directory.
        /// </param>
        ///
        /// <returns>
        /// Nothing.  The renamed DirectoryInfo or FileInfo object is
        /// written to the context's pipeline.
        /// </returns>
        ///
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        ///     newName is null or empty
        /// </exception>
        protected override void RenameItem(string path, string newName)
        {

            // Check the parameters
            if (String.IsNullOrEmpty(path))
            {
                throw TraceSource.NewArgumentException("path");
            }

            path = NormalizePath(path);

            if (String.IsNullOrEmpty(newName))
            {
                throw TraceSource.NewArgumentException("newName");
            }

            // newName = NormalizePath(newName);

            // Clean up "newname" to fix some common usability problems:
            // Rename .\foo.txt .\bar.txt
            // Rename c:\temp\foo.txt c:\temp\bar.txt
            if (newName.StartsWith(".\\", StringComparison.OrdinalIgnoreCase) ||
                newName.StartsWith("./", StringComparison.OrdinalIgnoreCase))
            {
                newName = newName.Remove(0, 2);
            }
            // else if (String.Equals(Path.GetDirectoryName(path), Path.GetDirectoryName(newName), StringComparison.OrdinalIgnoreCase))
            // {
            //     newName = Path.GetFileName(newName);
            // }

            //Check to see if the target specified is just filename. We dont allow rename to move the file to a different directory.
            //If a path is specified for the newName then we flag that as an error.
            // if (String.Compare(Path.GetFileName(newName), newName, StringComparison.OrdinalIgnoreCase) != 0)
            // {
            //     throw TraceSource.NewArgumentException("newName", ArchiveProviderStrings.RenameError);
            // }

            // Check to see if the target specified exists. 
            if (ItemExists(newName))
            {
                throw TraceSource.NewArgumentException("newName", ArchiveProviderStrings.RenameError);
            }
            
            try
            {           
                // Manually move this item since you cant have more than one stream open at a time.
                ArchiveItemInfo file = new ArchiveItemInfo(ArchiveDriveInfo, path);
                ArchiveItemInfo result;

                // Confirm the rename with the user

                string action = ArchiveProviderStrings.RenameItemActionFile;

                string resource = String.Format(ArchiveProviderStrings.RenameItemResourceFileTemplate, file.FullName, newName);


                if (ShouldProcess(resource, action))
                {
                    // Now move the file
                    // Validate Current PWD is not the Provider
                    //if ((!Path.IsPathFullyQualified(newName)) && (!SessionState.Path.CurrentLocation.Path.StartsWith(ArchiveDriveInfo.Name + ":")) )
                    //{
                    //    newName = Path.Join(SessionState.Path.CurrentLocation.Path, newName);
                    //}

                    file.MoveTo(newName);

                    result = file;
                    WriteItemObject(result, result.FullName, false);
                }
            }
            catch (ArgumentException argException)
            {
                WriteError(new ErrorRecord(argException, "RenameItemArgumentError", ErrorCategory.InvalidArgument, path));
            }
            catch (IOException ioException)
            {
                //IOException contains specific message about the error occured and so no need for errordetails.
                WriteError(new ErrorRecord(ioException, "RenameItemIOError", ErrorCategory.WriteError, path));
            }
            catch (UnauthorizedAccessException accessException)
            {
                WriteError(new ErrorRecord(accessException, "RenameItemUnauthorizedAccessError", ErrorCategory.PermissionDenied, path));
            }
        }
        #endregion RenameItem
        #region NewItem
        /// <summary>
        /// Creates a file or directory with the given path.
        /// </summary>
        /// <param name="path">
        /// The path of the file or directory to create.
        /// </param>
        ///<param name="type">
        /// Specify "file" to create a file.
        /// Specify "directory" or "container" to create a directory.
        /// </param>
        /// <param name="value">
        /// If <paramref name="type" /> is "file" then this parameter becomes the content
        /// of the file to be created.
        /// </param>
        /// <returns>
        /// Nothing.  The new DirectoryInfo or FileInfo object is
        /// written to the context's pipeline.
        /// </returns>
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        ///     type is null or empty.
        /// </exception>
        protected override void NewItem(
            string path,
            string type,
            object value)
        {
            ItemType itemType = ItemType.Unknown;
            bool CreateIntermediateDirectories = false;

            // Verify parameters
            if (string.IsNullOrEmpty(path))
            {
                throw TraceSource.NewArgumentException("path");
            }

            if (String.IsNullOrEmpty(type))
            {
                type = "file";
            }

            itemType = GetItemType(type);

            // Determine item Type
            if (itemType == ItemType.Unknown)
            {
                if (PathUtils.EndsInDirectorySeparator(path))
                {
                    itemType = ItemType.Directory;
                }
                else
                {
                    itemType = ItemType.File;
                }
            }

            path = NormalizePath(path);

            try {

                if (Force)
                {
                    ArchiveItemInfo NewFile = new ArchiveItemInfo(ArchiveDriveInfo, path, true);
                    ArchiveDriveInfo.buildFolderPaths();
                }

                // Validate Parent Directory does not exist
                if (!IsItemContainer(Path.GetDirectoryName(path)) && !Force)
                {
                    WriteError(new ErrorRecord(
                        new IOException("Parent directory does not exist"),
                        "NewItemIOError",
                        ErrorCategory.WriteError,
                        path
                    ));
                    return;
                }
                
                if (IsItemContainer(path) && itemType == ItemType.File)
                {
                    throw new UnauthorizedAccessException("No Access");
                }

                if (ItemExists(path) && !Force)
                {
                    throw new Exception("File Exists");
                }

                if (itemType == ItemType.Directory)
                {
                    string action = ArchiveProviderStrings.NewItemActionDirectory;

                    string resource = String.Format(ArchiveProviderStrings.NewItemActionTemplate, path);

                    if (!ShouldProcess(resource, action))
                    {
                        return;
                    }

                    if (!PathUtils.EndsInDirectorySeparator(path))
                    {
                        path += Path.AltDirectorySeparatorChar;
                    }

                    ArchiveItemInfo newItem = new ArchiveItemInfo(ArchiveDriveInfo, path, true);

                }
                else if (itemType == ItemType.File)
                {
                    string action = ArchiveProviderStrings.NewItemActionFile;

                    string resource = String.Format(ArchiveProviderStrings.NewItemActionTemplate, path);

                    if (!ShouldProcess(resource, action))
                    {
                        return;
                    }

                    ArchiveItemInfo newItem = new ArchiveItemInfo(ArchiveDriveInfo, path, true);
                    newItem = new ArchiveItemInfo(ArchiveDriveInfo, path, true);
                    if (value != null)
                    {
                        using (StreamWriter writer = newItem.AppendText())
                        {
                            writer.Write(value.ToString());
                            writer.Flush();
                            writer.Dispose();
                        }
                    }
                }
            }
            catch(Exception exception) {
                //rollback the directory creation if it was created.
                // if (!pathExists)
                // {
                //     pathDirInfo.Delete();
                // }

                if ((exception is FileNotFoundException) ||
                        (exception is DirectoryNotFoundException) ||
                        (exception is UnauthorizedAccessException) ||
                        (exception is System.Security.SecurityException) ||
                        (exception is ArgumentException) ||
                        (exception is PathTooLongException) ||
                        (exception is NotSupportedException) ||
                        (exception is ArgumentNullException) ||
                        (exception is IOException))
                {
                    WriteError(new ErrorRecord(exception, "NewItemCreateIOError", ErrorCategory.WriteError, path));
                }
                else
                    throw;
            }
        }
        // Todo: private static bool WinCreateSymbolicLink(string path, string strTargetPath, bool isDirectory)
        // Todo: private static bool WinCreateHardLink(string path, string strTargetPath)
        // Todo: private static bool WinCreateJunction(string path, string strTargetPath)
        // Todo: private static bool CheckItemExists(string strTargetPath, out bool isDirectory)
        private enum ItemType
        {
            Unknown,
            File,
            Directory
        };

        private static ItemType GetItemType(string input)
        {
            ItemType itemType = ItemType.Unknown;

            WildcardPattern typeEvaluator =
                WildcardPattern.Get(input + "*",
                                     WildcardOptions.IgnoreCase |
                                     WildcardOptions.Compiled);

            if (typeEvaluator.IsMatch("directory") ||
                typeEvaluator.IsMatch("container"))
            {
                itemType = ItemType.Directory;
            }
            else if (typeEvaluator.IsMatch("file"))
            {
                itemType = ItemType.File;
            }

            return itemType;
        }

        // Todo: private void CreateDirectory(string path, bool streamOutput)
        // Todo: private bool CreateIntermediateDirectories(string path)
        #endregion NewItem
        #region RemoveItem
        /// <summary>
        /// Removes the specified file or directory.
        /// </summary>
        /// <param name="path">
        /// The full path to the file or directory to be removed.
        /// </param>
        /// <param name="recurse">
        /// Specifies if the operation should also remove child items.
        /// </param>
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        /// </exception>
        protected override void RemoveItem(string path, bool recurse)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw TraceSource.NewArgumentException("path");
            }

            try {
                path = NormalizePath(path);

                if (!ItemExists(path))
                {
                    WriteError(
                        new ErrorRecord( 
                            new IOException(String.Format(ArchiveProviderStrings.ItemDoesNotExist, path)),
                            "ItemDoesNotExist",
                            ErrorCategory.ObjectNotFound,
                            path
                        )

                    );
                    return;
                }

                bool isItemContainer = IsItemContainer(path) && IsItemContainerContainsItems(path);

                if (!recurse && isItemContainer)
                {
                    throw new Exception("Folder contains subitems");
                }

                IEnumerable<ArchiveItemInfo> archiveItems;
                if (isItemContainer)
                {
                    // Recursivly remove items

                    archiveItems = ArchiveDriveInfo.GetItem(path+"*");
                }
                else {
                    archiveItems = ArchiveDriveInfo.GetItem(path, true, true);
                }

                // Item ToArray skips a file open bug. 
                foreach(ArchiveItemInfo archiveItem in archiveItems.ToArray())
                {
                    string action = $"Do you want to remove current file?";
                    if (ShouldProcess(archiveItem.FullName, action))
                    {
                        archiveItem.Delete();
                    } // ShouldProcess
                }

            }
            catch(Exception exception) {
                if ((exception is FileNotFoundException) ||
                        (exception is DirectoryNotFoundException) ||
                        (exception is UnauthorizedAccessException) ||
                        (exception is System.Security.SecurityException) ||
                        (exception is ArgumentException) ||
                        (exception is PathTooLongException) ||
                        (exception is NotSupportedException) ||
                        (exception is ArgumentNullException) ||
                        (exception is IOException))
                {
                    WriteError(new ErrorRecord(exception, "NewItemCreateIOError", ErrorCategory.WriteError, path));
                }
                else
                    Console.WriteLine("An Error was thrown");
                    throw;
            }

		}
        // Todo: protected override object RemoveItemDynamicParameters(string path, bool recurse)
        // Todo: private void RemoveDirectoryInfoItem(DirectoryInfo directory, bool recurse, bool force, bool rootOfRemoval)
        // Todo: private void RemoveFileInfoItem(FileInfo file, bool force)
        // Todo: private void RemoveFileSystemItem(FileSystemInfo fileSystemInfo, bool force)
        #endregion RemoveItem
        #region ItemExists
        /// <summary>
        /// Determines if a file or directory exists at the specified path.
        /// </summary>
        ///
        /// <param name="path">
        /// The path of the item to check.
        /// </param>
        ///
        /// <returns>
        /// True if a file or directory exists at the specified path, false otherwise.
        /// </returns>
        ///
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        /// </exception>
        ///

        protected override bool ItemExists(string path)
        {
            ErrorRecord error = null;

            bool result = ItemExists(path, out error);
            if (error != null)
            {
                WriteError(error);
            }

            return result;
        }

        /// <summary>
        /// Implementation of ItemExists for the provider. This implementation
        /// allows the caller to decide if it wants to WriteError or not based
        /// on the returned ErrorRecord
        /// </summary>
        ///
        /// <param name="path">
        /// The path of the object to check
        /// </param>
        ///
        /// <param name="error">
        /// An error record is returned in this parameter if there was an error.
        /// </param>
        ///
        /// <returns>
        /// True if an object exists at the specified path, false otherwise.
        /// </returns>
        ///
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        /// </exception>
        ///

        private bool ItemExists(string path, out ErrorRecord error)
        {
            error = null;

            if (String.IsNullOrEmpty(path))
            {
                throw TraceSource.NewArgumentException("path");
            }

            bool result = false;

            path = NormalizePath(path);
            
            if (String.IsNullOrEmpty(path))
            {
                return true;
            }
            try
            {
                bool notUsed;
                // Exception accessException;

                // First see if the file exists
                try {
                    if (ArchiveDriveInfo.ItemExists(path))
                    {
                        result = true;
                    }
                }
                catch (IOException ioException)
                {
                    // File Archive Open and ArchiveItem Open throws the same errors, need to validate
                    // ArchiveItem existance.
                    if (ioException.Message != String.Format(ArchiveProviderStrings.ItemNotFound, path))
                    {
                        throw ioException;
                    }

                }
                catch (PSArgumentException psArgumentException)
                {

                }
                
                FileSystemItemProviderDynamicParameters itemExistsDynamicParameters =
                    DynamicParameters as FileSystemItemProviderDynamicParameters;

                // If the items see if we need to check the age of the file...
                if (result && itemExistsDynamicParameters != null)
                {
                    // DateTime lastWriteTime = File.GetLastWriteTime(path);

                    // if (itemExistsDynamicParameters.OlderThan.HasValue)
                    // {
                    //     result = lastWriteTime < itemExistsDynamicParameters.OlderThan.Value;
                    // }
                    // if (itemExistsDynamicParameters.NewerThan.HasValue)
                    // {
                    //     result = lastWriteTime > itemExistsDynamicParameters.NewerThan.Value;
                    // }
                }
            }
            catch (System.Security.SecurityException security)
            {
                error = new ErrorRecord(security, "ItemExistsSecurityError", ErrorCategory.PermissionDenied, path);
            }
            catch (ArgumentException argument)
            {
                error = new ErrorRecord(argument, "ItemExistsArgumentError", ErrorCategory.InvalidArgument, path);
            }
            catch (UnauthorizedAccessException unauthorized)
            {
                error = new ErrorRecord(unauthorized, "ItemExistsUnauthorizedAccessError", ErrorCategory.PermissionDenied, path);
            }
            catch (PathTooLongException pathTooLong)
            {
                error = new ErrorRecord(pathTooLong, "ItemExistsPathTooLongError", ErrorCategory.InvalidArgument, path);
            }
            catch (NotSupportedException notSupported)
            {
                error = new ErrorRecord(notSupported, "ItemExistsNotSupportedError", ErrorCategory.InvalidOperation, path);
            }

            return result;
        }
        // Todo: protected override object ItemExistsDynamicParameters(string path)
        #endregion ItemExists
        #region HasChildItems

        /// <summary>
        /// Determines if the given path is a directory, and has children.
        /// </summary>
        /// <param name="path">
        /// The full path to the directory.
        /// </param>
        /// <returns>
        /// True if the path refers to a directory that contains other
        /// directories or files.  False otherwise.
        /// </returns>
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        /// </exception>
        protected override bool HasChildItems(string path)
        {
            bool result = false;
            
            // verify parameters
            if (string.IsNullOrEmpty(path))
            {
                throw TraceSource.NewArgumentException("path");
            }

            path = NormalizePath(path);

            return IsItemContainer(path) && IsItemContainerContainsItems(path);
        }

        // Todo: private static bool DirectoryInfoHasChildItems(DirectoryInfo directory)
        #endregion HasChildItems
        #region CopyItem
        /// <summary>
        /// Copies an item at the specified path to the given destination.
        /// </summary>
        ///
        /// <param name="path">
        /// The path of the item to copy.
        /// </param>
        ///
        /// <param name="destinationPath">
        /// The path of the destination.
        /// </param>
        ///
        /// <param name="recurse">
        /// Specifies if the operation should also copy child items.
        /// </param>
        ///
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        ///     destination path is null or empty.
        /// </exception>
        ///
        /// <returns>
        /// Nothing.  Copied items are written to the context's pipeline.
        /// </returns>
        protected override void CopyItem(
            string path,
            string destinationPath,
            bool recurse)
        {
            if (String.IsNullOrEmpty(path))
            {
                throw TraceSource.NewArgumentException("path");
            }

            if (String.IsNullOrEmpty(destinationPath))
            {
                throw TraceSource.NewArgumentException("destinationPath");
            }

            path = NormalizePath(path);
            destinationPath = NormalizePath(destinationPath);

            // Clean up "newname" to fix some common usability problems:
            // Rename .\foo.txt .\bar.txt
            // Rename c:\temp\foo.txt c:\temp\bar.txt
            if (destinationPath.StartsWith(".\\", StringComparison.OrdinalIgnoreCase) ||
                destinationPath.StartsWith("./", StringComparison.OrdinalIgnoreCase))
            {
                destinationPath = destinationPath.Remove(0, 2);
            }

            bool pathIsDirectory = ArchiveDriveInfo.IsItemContainer(path);
            bool destIsDirectory = false;

            if (PathUtils.EndsInDirectorySeparator(destinationPath))
            {
                destIsDirectory = true;
            }

            // Check if wildcard exists and destination is not a directory.
            // This should throw

            //CopyItemDynamicParameters copyDynamicParameter = DynamicParameters as CopyItemDynamicParameters;

            //if (copyDynamicParameter != null)
            //{
            //    if (copyDynamicParameter.FromSession != null)
            //    {
            //        fromSession = copyDynamicParameter.FromSession;
            //    }
            //    else
            //    {
            //        toSession = copyDynamicParameter.ToSession;
            //    }
            //}

            // Wildcard Items dont exist.
            try 
            {

                IEnumerable<ArchiveItemInfo> files;
                if (pathIsDirectory)
                {
                    files = ArchiveDriveInfo.GetItem(path+"/*", true, true);
                }
                else
                {
                    files = ArchiveDriveInfo.GetItem(path, true, true);
                }

                // Confirm the move with the user
                string action = ArchiveProviderStrings.CopyItemActionFile;
                foreach (ArchiveItemInfo file in files)
                {
                    string driveName = (file.Drive.Name + Path.VolumeSeparatorChar + Path.DirectorySeparatorChar);

                    string resource = String.Format(ArchiveProviderStrings.CopyItemResourceFileTemplate, file.FullName, destinationPath);
                    if (ShouldProcess(resource, action))
                    {
                        // If pathIsDirectory
                        string destPath = destinationPath;

                        if (pathIsDirectory)
                        {
                            string relPath = Path.GetRelativePath($"{driveName}{path}",  file.FullName);
                            destPath = Path.Join(destinationPath, relPath);
                        }
                        else if (destIsDirectory) {
                            destPath = Path.Join(destinationPath, file.Name);
                        }

                        file.CopyTo(destPath);
                    }

                }
            }
            catch(Exception e) {
                throw e;
            }
        }
        // Todo: private void CopyItemFromRemoteSession(string path, string destinationPath, bool recurse, bool force, PSSession fromSession)
        // Todo: private void CopyItemLocalOrToSession(string path, string destinationPath, bool recurse, bool Force, System.Management.Automation.PowerShell ps)
        // Todo: private void CopyDirectoryInfoItem(
        // Todo: private void CopyFileInfoItem(FileInfo file, string destinationPath, bool force, System.Management.Automation.PowerShell ps)
        // Todo: private void CopyDirectoryFromRemoteSession(
        // Todo: private ArrayList GetRemoteSourceAlternateStreams(System.Management.Automation.PowerShell ps, string path)
        // Todo: private void InitializeFunctionPSCopyFileFromRemoteSession(System.Management.Automation.PowerShell ps)
        // Todo: private void RemoveFunctionsPSCopyFileFromRemoteSession(System.Management.Automation.PowerShell ps)
        // Todo: private bool ValidRemoteSessionForScripting(Runspace runspace)
        // Todo: private Hashtable GetRemoteFileMetadata(string filePath, System.Management.Automation.PowerShell ps)
        // Todo: private void SetFileMetadata(string sourceFileFullName, FileInfo destinationFile, System.Management.Automation.PowerShell ps)
        // Todo: private void CopyFileFromRemoteSession(
        // Todo: private bool PerformCopyFileFromRemoteSession(string sourceFileFullName, FileInfo destinationFile, string destinationPath, bool force, System.Management.Automation.PowerShell ps,
        // Todo: private void InitializeFunctionsPSCopyFileToRemoteSession(System.Management.Automation.PowerShell ps)
        // Todo: private void RemoveFunctionPSCopyFileToRemoteSession(System.Management.Automation.PowerShell ps)
        // Todo: private bool RemoteTargetSupportsAlternateStreams(System.Management.Automation.PowerShell ps, string path)
        // Todo: private string MakeRemotePath(System.Management.Automation.PowerShell ps, string remotePath, string filename)
        // Todo: private bool RemoteDirectoryExist(System.Management.Automation.PowerShell ps, string path)
        // Todo: private bool CopyFileStreamToRemoteSession(FileInfo file, string destinationPath, System.Management.Automation.PowerShell ps, bool isAlternateStream, string streamName)
        // Todo: private Hashtable GetFileMetadata(FileInfo file)
        // Todo: private void SetRemoteFileMetadata(FileInfo file, string remoteFilePath, System.Management.Automation.PowerShell ps)
        // Todo: private bool PerformCopyFileToRemoteSession(FileInfo file, string destinationPath, System.Management.Automation.PowerShell ps)
        // Todo: private bool RemoteDestinationPathIsFile(string destination, System.Management.Automation.PowerShell ps)
        // Todo: private string CreateDirectoryOnRemoteSession(string destination, bool force, System.Management.Automation.PowerShell ps)
        // Todo: private bool PathIsReservedDeviceName(string destinationPath, string errorId)
        #endregion CopyItem
        #endregion ContainerCmdletProvider members
        #region NavigationCmdletProvider members
        // Todo: protected override string GetParentPath(string path, string root)

        // Note: we don't use IO.Path.IsPathRooted as this deals with "invalid" i.e. unnormalized paths
        private static bool IsAbsolutePath(string path)
        {
            Console.WriteLine($"IsAbsolutePath: {path}");
            return false;
        }

        internal static bool PathIsUnc(string path)
        {
#if UNIX
            return false;
#else
            Uri uri;
            return !string.IsNullOrEmpty(path) && Uri.TryCreate(path, UriKind.Absolute, out uri) && uri.IsUnc;
#endif
        }

        // Todo: private static bool IsUNCPath(string path)

        // Todo: private static bool IsUNCRoot(string path)
        // Todo: private static bool IsPathRoot(string path)
        // Todo: protected override string NormalizeRelativePath(
        // Todo: private string NormalizeRelativePathHelper(string path, string basePath)
        // Todo: private string RemoveRelativeTokens(string path)
        // Todo: private string GetCommonBase(string path1, string path2)
        // Todo: private Stack<string> TokenizePathToStack(string path, string basePath)
        // Todo: private Stack<string> NormalizeThePath(string basepath, Stack<string> tokenizedPathStack)
        // Todo: private string CreateNormalizedRelativePathFromStack(Stack<string> normalizedPathStack)
        // Todo: protected override string GetChildName(string path)
        // Todo: private static string EnsureDriveIsRooted(string path)

        protected bool IsItemContainerContainsItems(string path)
        {
            bool result = false;

            if (!PathUtils.EndsInDirectorySeparator(path))
            {
                path += Path.DirectorySeparatorChar;
            }
            path += "*";
            
            ArchiveItemInfo[] items = ArchiveDriveInfo.GetItem(path).ToArray();

            if (items.Length > 0)
            {
                result = true;
            }

            return result;
        }

		protected override bool IsItemContainer(string path)
		{
            path = NormalizePath(path);
            
            if ( String.IsNullOrEmpty(path) )
            {
                return true;
            }
            else if ( path == "\\" || path == "/")
            {
                return true;
            }

            return ArchiveDriveInfo.IsItemContainer(path);
		}

        #region MoveItem
        // Todo: protected override void MoveItem(
        // Todo: private void MoveFileInfoItem(
        // Todo: private void MoveDirectoryInfoItem(
        // Todo: private void CopyAndDelete(DirectoryInfo directory, string destination, bool force)
        // Todo: private bool IsSameVolume(string source, string destination)
        #endregion MoveItem
        #endregion NavigationCmdletProvider members
        #region IPropertyCmdletProvider
        // Todo: public void GetProperty(string path, Collection<string> providerSpecificPickList)
        // Todo: public object GetPropertyDynamicParameters(
        // Todo: public void SetProperty(string path, PSObject propertyToSet)
        // Todo: public object SetPropertyDynamicParameters(
        // Todo: public void ClearProperty(
        // Todo: public object ClearPropertyDynamicParameters(
        #endregion IPropertyCmdletProvider
        #region IContentCmdletProvider

        /// <summary>
        /// Creates an instance of the FileSystemContentStream class, opens
        /// the specified file for reading, and returns the IContentReader interface
        /// to it.
        /// </summary>
        /// <param name="path">
        /// The path of the file to be opened for reading.
        /// </param>
        /// <returns>
        /// An IContentReader for the specified file.
        /// </returns>
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        /// </exception>
        public IContentReader GetContentReader(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw TraceSource.NewArgumentException("path");
            }

            path = NormalizePath(path);

            if (IsItemContainer(path))
            {
                throw new Exception("You cannot read the contents of a folder");
            }

            // Defaults for the file read operation
            string delimiter = "\n";

            Encoding encoding = Encoding.Default;
            // Encoding encoding = new Encoding.Default();

            bool streamTypeSpecified = false;
            bool usingByteEncoding = false;
            bool delimiterSpecified = false;
            bool isRawStream = false;

            // Get the dynamic parameters.
            // They override the defaults specified above.
            if (DynamicParameters != null)
            {
                StreamContentReaderDynamicParameters dynParams = DynamicParameters as StreamContentReaderDynamicParameters;
                if (dynParams != null)
                {
                    // -raw is not allowed when -first,-last or -wait is specified
                    // this call will validate that and throws.
                    ValidateParameters(dynParams.Raw);

                    isRawStream = dynParams.Raw;

                    // Get the delimiter
                    delimiterSpecified = dynParams.DelimiterSpecified;
                    if (delimiterSpecified)
                        delimiter = dynParams.Delimiter;

                    // Get the stream type
                    usingByteEncoding = dynParams.AsByteStream;
                    streamTypeSpecified = dynParams.WasStreamTypeSpecified;

                    if (usingByteEncoding && streamTypeSpecified)
                    {
                        WriteWarning(ArchiveProviderStrings.EncodingNotUsed);
                    }

                    if (streamTypeSpecified)
                    {
                        encoding = dynParams.Encoding;
                    }

                }
            }
            StreamContentReaderWriter stream = null;

            ArchiveItemInfo archiveFile = new ArchiveItemInfo(ArchiveDriveInfo, path);

            try
            {
                // Users can't both read as bytes, and specify a delimiter
                if (delimiterSpecified)
                {
                    if (usingByteEncoding)
                    {
                        Exception e =
                            new ArgumentException(ArchiveProviderStrings.DelimiterError, "delimiter");
                        WriteError(new ErrorRecord(
                            e,
                            "GetContentReaderArgumentError",
                            ErrorCategory.InvalidArgument,
                            path));
                    }
                    else
                    {
                        stream = new ArchiveContentStream(archiveFile, FileMode.Append, delimiter, encoding, usingByteEncoding, this, isRawStream);
                    }
                }
                else
                {
                    stream = new ArchiveContentStream(archiveFile, FileMode.Append, encoding, usingByteEncoding, this, isRawStream);
                }
            }
            catch (PathTooLongException pathTooLong)
            {
                WriteError(new ErrorRecord(pathTooLong, "GetContentReaderPathTooLongError", ErrorCategory.InvalidArgument, path));
            }
            catch (FileNotFoundException fileNotFound)
            {
                WriteError(new ErrorRecord(fileNotFound, "GetContentReaderFileNotFoundError", ErrorCategory.ObjectNotFound, path));
            }
            catch (DirectoryNotFoundException directoryNotFound)
            {
                WriteError(new ErrorRecord(directoryNotFound, "GetContentReaderDirectoryNotFoundError", ErrorCategory.ObjectNotFound, path));
            }
            catch (ArgumentException argException)
            {
                WriteError(new ErrorRecord(argException, "GetContentReaderArgumentError", ErrorCategory.InvalidArgument, path));
            }
            catch (IOException ioException)
            {
                // IOException contains specific message about the error occured and so no need for errordetails.
                WriteError(new ErrorRecord(ioException, "GetContentReaderIOError", ErrorCategory.ReadError, path));
            }
            catch (System.Security.SecurityException securityException)
            {
                WriteError(new ErrorRecord(securityException, "GetContentReaderSecurityError", ErrorCategory.PermissionDenied, path));
            }
            catch (UnauthorizedAccessException unauthorizedAccess)
            {
                WriteError(new ErrorRecord(unauthorizedAccess, "GetContentReaderUnauthorizedAccessError", ErrorCategory.PermissionDenied, path));
            }
            catch (Exception e)
            {
                WriteError(
                    new ErrorRecord(e, "Unhandled Error", ErrorCategory.InvalidArgument , path)
                );
            }

            if (stream == null)
            {
                throw new Exception("Invalid stream");
            }

            return stream;
        }

        public object GetContentReaderDynamicParameters(string path)
		{
            return new StreamContentReaderDynamicParameters();
		}

        /// <summary>
        /// Creates an instance of the FileSystemContentStream class, opens
        /// the specified file for writing, and returns the IContentReader interface
        /// to it.
        /// </summary>
        /// <param name="path">
        /// The path of the file to be opened for writing.
        /// </param>
        /// <returns>
        /// An IContentWriter for the specified file.
        /// </returns>
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        /// </exception>
        public IContentWriter GetContentWriter(string path)
        {

            if (string.IsNullOrEmpty(path))
            {
                throw TraceSource.NewArgumentException("path");
            }

            path = NormalizePath(path);

            // If this is true, then the content will be read as bytes
            bool usingByteEncoding = false;
            bool streamTypeSpecified = false;
            
            //Encoding encoding = ClrFacade.GetDefaultEncoding();
            Encoding encoding = Encoding.Default;

            FileMode filemode = FileMode.OpenOrCreate;
            bool suppressNewline = false;

            // Get the dynamic parameters
            if (DynamicParameters != null)
            {

                // [BUG] Regardless of override DynamicParameters is of type FileSystemContentWriterDynamicParameters
                StreamContentWriterDynamicParameters dynParams = DynamicParameters as StreamContentWriterDynamicParameters;

                //FileSystemContentWriterDynamicParameters dynParams = DynamicParameters as FileSystemContentWriterDynamicParameters;
                //ArchiveContentWriterDynamicParameters dynParams = DynamicParameters as ArchiveContentWriterDynamicParameters;

                if (dynParams != null)
                {
                    usingByteEncoding = dynParams.AsByteStream;
                    streamTypeSpecified = dynParams.WasStreamTypeSpecified;

                    if (usingByteEncoding && streamTypeSpecified)
                    {
                        WriteWarning(ArchiveProviderStrings.EncodingNotUsed);
                    }

                    if (streamTypeSpecified)
                    {
                        encoding = dynParams.Encoding;
                    }

                    suppressNewline = dynParams.NoNewline.IsPresent;
                }
            }

            StreamContentReaderWriter stream = null;

            // Validate Parent Directory does not exist
            if (!IsItemContainer(Path.GetDirectoryName(path)))
            {
                throw new Exception("Parent directory does not exist");
            }
            if (IsItemContainer(path))
            {
                throw new Exception("You cannot write to a folder");
            }

            ArchiveItemInfo archiveFile = new ArchiveItemInfo(ArchiveDriveInfo, path, true);

            try
            {
                stream = new ArchiveContentStream(archiveFile, FileMode.Append, encoding, usingByteEncoding, this, false, suppressNewline);
            }
            catch (PathTooLongException pathTooLong)
            {
                WriteError(new ErrorRecord(pathTooLong, "GetContentWriterPathTooLongError", ErrorCategory.InvalidArgument, path));
            }
            catch (FileNotFoundException fileNotFound)
            {
                WriteError(new ErrorRecord(fileNotFound, "GetContentWriterFileNotFoundError", ErrorCategory.ObjectNotFound, path));
            }
            catch (DirectoryNotFoundException directoryNotFound)
            {
                WriteError(new ErrorRecord(directoryNotFound, "GetContentWriterDirectoryNotFoundError", ErrorCategory.ObjectNotFound, path));
            }
            catch (ArgumentException argException)
            {
                WriteError(new ErrorRecord(argException, "GetContentWriterArgumentError", ErrorCategory.InvalidArgument, path));
            }
            catch (IOException ioException)
            {
                // IOException contains specific message about the error occured and so no need for errordetails.
                WriteError(new ErrorRecord(ioException, "GetContentWriterIOError", ErrorCategory.WriteError, path));
            }
            catch (System.Security.SecurityException securityException)
            {
                WriteError(new ErrorRecord(securityException, "GetContentWriterSecurityError", ErrorCategory.PermissionDenied, path));
            }
            catch (UnauthorizedAccessException unauthorizedAccess)
            {
                WriteError(new ErrorRecord(unauthorizedAccess, "GetContentWriterUnauthorizedAccessError", ErrorCategory.PermissionDenied, path));
            }

            return stream;
        }

        public object GetContentWriterDynamicParameters(string path)
		{
			return new StreamContentWriterDynamicParameters();
		}

        /// <summary>
        /// Clears the content of the specified file.
        /// </summary>
        ///
        /// <param name="path">
        /// The path to the file of which to clear the contents.
        /// </param>
        ///
        /// <exception cref="System.ArgumentException">
        ///     path is null or empty.
        /// </exception>
		public void ClearContent(string path)
		{

            if (String.IsNullOrEmpty(path))
            {
                throw TraceSource.NewArgumentException("path");
            }

            path = NormalizePath(path);

            try
            {
                bool clearStream = false;
                string streamName = null;
                FileSystemClearContentDynamicParameters dynamicParameters = null;
                FileSystemContentWriterDynamicParameters writerDynamicParameters = null;

                // We get called during:
                //     - Clear-Content
                //     - Set-Content, in the phase that clears the path first.
                if (DynamicParameters != null)
                {
                    dynamicParameters = DynamicParameters as FileSystemClearContentDynamicParameters;
                    writerDynamicParameters = DynamicParameters as FileSystemContentWriterDynamicParameters;
                }

                string action = ArchiveProviderStrings.ClearContentActionFile;
                string resource = String.Format(ArchiveProviderStrings.ClearContentesourceTemplate, path);

                if (!ShouldProcess(resource, action))
                    return;

                // Validate Parent Directory does not exist
                if (!IsItemContainer(Path.GetDirectoryName(path)))
                {
                    throw new Exception("Parent directory does not exist");
                }

                path = NormalizePath(path);

                ArchiveItemInfo archiveFile = new ArchiveItemInfo(ArchiveDriveInfo, path, Force.ToBool());
                archiveFile.ClearContent();

                // For filesystem once content is cleared
                WriteItemObject("", path, false);
            }
            catch (ArgumentException argException)
            {
                WriteError(new ErrorRecord(argException, "ClearContentArgumentError", ErrorCategory.InvalidArgument, path));
            }
            catch (FileNotFoundException fileNotFoundException)
            {
                WriteError(new ErrorRecord(fileNotFoundException, "PathNotFound", ErrorCategory.ObjectNotFound, path));
            }
            catch (IOException ioException)
            {
                //IOException contains specific message about the error occured and so no need for errordetails.
                WriteError(new ErrorRecord(ioException, "ClearContentIOError", ErrorCategory.WriteError, path));
            }
		}
        
        public object ClearContentDynamicParameters(string path)
		{
            return new StreamContentClearContentDynamicParameters();
		}
        #endregion IContentCmdletProvider

        /// <summary>
        /// -raw is not allowed when -first,-last or -wait is specified
        /// this call will validate that and throws.
        /// </summary>
        private void ValidateParameters(bool isRawSpecified)
        {
            if (isRawSpecified)
            {
                if (this.Context_MyInvocation.BoundParameters.ContainsKey("TotalCount"))
                {
                    string message = String.Format(ArchiveProviderStrings.NoFirstLastWaitForRaw, "Raw", "TotalCount");
                    throw new PSInvalidOperationException(message);
                }
            

                if (this.Context_MyInvocation.BoundParameters.ContainsKey("Tail"))
                {
                    string message = String.Format(ArchiveProviderStrings.NoFirstLastWaitForRaw, "Raw", "Tail");
                    throw new PSInvalidOperationException(message);
                }

                if (this.Context_MyInvocation.BoundParameters.ContainsKey("Delimiter"))
                {
                    string message = String.Format(ArchiveProviderStrings.NoFirstLastWaitForRaw, "Raw", "Delimiter");
                    throw new PSInvalidOperationException(message);
                }
            }
        }

        // Todo: private static class NativeMethods
        // Todo: private struct NetResource
        // Todo:     public int Scope;
        // Todo:     public int Type;
        // Todo:     public int DisplayType;
        // Todo:     public int Usage;
        // Todo:     public string LocalName;
        // Todo:     public string RemoteName;
        // Todo:     public string Comment;
        // Todo:     public string Provider;
        #region InodeTracker
        // Todo: private class InodeTracker
            private HashSet<(UInt64, UInt64)> _visitations;
        #endregion
        // Todo: public static Hashtable Invoke(System.Management.Automation.PowerShell ps, FileSystemProvider fileSystemContext, CmdletProviderContext cmdletContext)
        // Todo: public static Hashtable Invoke(System.Management.Automation.PowerShell ps, FileSystemProvider fileSystemContext, CmdletProviderContext cmdletContext, bool shouldHaveOutput)
    #endregion
    #region Dynamic Parameters
        // Todo: internal sealed class CopyItemDynamicParameters
        // Todo: internal sealed class GetChildDynamicParameters
        // Todo: public class FileSystemContentDynamicParametersBase
        // Todo: public class FileSystemClearContentDynamicParameters        
        // Todo: public class FileSystemContentWriterDynamicParameters
        // Todo: public class FileSystemContentReaderDynamicParameters
        // Todo: public class FileSystemItemProviderDynamicParameters
        // Todo: public class FileSystemProviderGetItemDynamicParameters
        // Todo: public class FileSystemProviderRemoveItemDynamicParameters
    #endregion
    #region Symbolic Link
        // Todo: public static class InternalSymbolicLinkLinkCodeMethods
        // Todo:     private static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode,
        // Todo:     private static extern IntPtr FindFirstFileName(
        // Todo:     private static extern bool FindNextFileName(
        // Todo:     private static extern bool FindClose(IntPtr hFindFile);
        // Todo:     private static extern bool GetFileInformationByHandle(
        // Todo:     public static IEnumerable<string> GetTarget(PSObject instance)
        // Todo:     public static string GetLinkType(PSObject instance)
        // Todo:     private static List<string> InternalGetTarget(string filePath)
        // Todo:     private static string InternalGetLinkType(FileSystemInfo fileInfo)
        // Todo:     private static string WinInternalGetLinkType(string filePath)
        // Todo:     private static bool WinIsSameFileSystemItem(string pathOne, string pathTwo)
        // Todo:     private static bool WinGetInodeData(string path, out System.ValueTuple<UInt64, UInt64> inodeData)
        // Todo:     private static string InternalGetTarget(SafeFileHandle handle)
        // Todo:     private static string WinInternalGetTarget(SafeFileHandle handle)
        // Todo:     private static bool WinCreateJunction(string path, string target)
        // Todo:     private static SafeFileHandle OpenReparsePoint(string reparsePoint, FileDesiredAccess accessMode)
        // Todo:     private static SafeFileHandle WinOpenReparsePoint(string reparsePoint, FileDesiredAccess accessMode)
    #endregion
    #region AlternateDataStreamUtilities
        // Todo: public class AlternateStreamData
        // Todo: public static class AlternateDataStreamUtilities
    #endregion
    #region CopyFileFromRemoteUtils
    //     private const string functionToken = "function ";
    //     private const string nameToken = "Name";
    //     private const string definitionToken = "Definition";
        #region PSCopyToSessionHelper
    //     private static string s_driveMaxSizeErrorFormatString = ArchiveProviderStrings.DriveMaxSizeError;
    //     private static string s_PSCopyToSessionHelperDefinition = String.Format(PSCopyToSessionHelperDefinitionFormat, @"[ValidateNotNullOrEmpty()]", s_driveMaxSizeErrorFormatString);
    //     private static string s_PSCopyToSessionHelperDefinitionRestricted = String.Format(PSCopyToSessionHelperDefinitionFormat, @"[ValidateUserDrive()]", s_driveMaxSizeErrorFormatString);
    //     private const string PSCopyToSessionHelperDefinitionFormat = @"
    //     private static string s_PSCopyToSessionHelper = functionToken + PSCopyToSessionHelperName + @"
    //     private static Hashtable s_PSCopyToSessionHelperFunction = new Hashtable() {
        #endregion
        #region PSCopyFromSessionHelper
    //     private static string s_PSCopyFromSessionHelperDefinition = String.Format(PSCopyFromSessionHelperDefinitionFormat, @"[ValidateNotNullOrEmpty()]");
    //     private static string s_PSCopyFromSessionHelperDefinitionRestricted = String.Format(PSCopyFromSessionHelperDefinitionFormat, @"[ValidateUserDrive()]");
    //     private const string PSCopyFromSessionHelperDefinitionFormat = @"
    //     private static Hashtable s_PSCopyFromSessionHelperFunction = new Hashtable() {
        #endregion
        #region PSCopyRemoteUtils
    //     private static string s_PSCopyRemoteUtilsDefinitionRestricted = String.Format(PSCopyRemoteUtilsDefinitionFormat, @"[ValidateUserDrive()]", PSValidatePathFunction);
    //     private const string PSCopyRemoteUtilsDefinitionFormat = @"
    //     private const string PSValidatePathFunction = functionToken + "PSValidatePath" + @"
        #endregion
    #endregion
    }
    //#endregion ArchiveProvider
}
