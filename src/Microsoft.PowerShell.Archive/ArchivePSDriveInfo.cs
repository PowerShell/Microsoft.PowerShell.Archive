using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.IO;
using System.IO.Compression;

namespace Microsoft.PowerShell.Archive
{

    public static class PathUtils
    {
        public static bool EndsInDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.AltDirectorySeparatorChar))
                return true;
            if (path.EndsWith(Path.DirectorySeparatorChar))
                return true;
            return false;
        }
        public static string TrimEndingDirectorySeparator(string path)
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar).TrimEnd(Path.AltDirectorySeparatorChar);
            return path;
        }

        
    }

	public class ArchivePSDriveInfo : PSDriveInfo
	{
		internal ZipArchive Archive {
			get;
			private set;
		}
		private Dictionary<string, System.IO.Stream> _streamsInUse;

        private FileSystemWatcher _fileWatcher;
        private int _fileWatcherLock = 0;

        private List<ArchiveItemInfo> _entryCache;

		//internal bool IsStreamInUse()
		//internal void OpenStream()
		//internal void CloseStream()
		
		//internal Stream PullStream() // Note this should not be used

		public List<string> _lockedEntries = new List<string>();
		public ZipArchive LockArchive(string entry)
		{
			if (_lockedEntries.Contains(entry))
			{
				throw new Exception("Cannot open file it is already open in another process");
			}
			_lockedEntries.Add(entry);
			
			if (Archive == null)
			{
				Archive = ZipFile.Open(Root, ZipArchiveMode.Update);
			}

			return Archive;
		}

		public void UnlockArchive(string entry)
		{
            UnlockArchive(entry, false);
		}
		public void UnlockArchive(string entry, bool updateCache)
		{
			if (!_lockedEntries.Contains(entry))
			{
				throw new Exception("Cannot unlock stream it doesnt exist");
			}

            if (!updateCache)
            {
                _entryCache = null;
            }

			_lockedEntries.Remove(entry);

			if (_lockedEntries.Count == 0)
			{
				Archive.Dispose();

				Archive = null;
				GC.Collect();
			}
		}

		internal bool IsStreamInUse()
		{
			if (Archive != null)
			{
				return true;
			}
			return false;
		}
		public int ActiveHandles {
			get {
				return _lockedEntries.Count;
			}
		}

	    /// <summary>
	    /// Initializes a new instance of the AccessDBPSDriveInfo class.
	    /// The constructor takes a single argument.
	    /// </summary>
	    /// <param name="driveInfo">Drive defined by this provider</param>
        public ArchivePSDriveInfo(PSDriveInfo driveInfo) : base(driveInfo)
		{
            UpdateCache();
		}
		

        #region ItemCache

        /// <summary>
        /// Updates the cached entries.
        /// </summary>
        protected private void UpdateCache()
        {
            try
            {
                _entryCache = new List<ArchiveItemInfo>();
                ZipArchive zipArchive = LockArchive(ArchiveProviderStrings.GetChildItemsAction);

                foreach (ZipArchiveEntry zipArchiveEntry in zipArchive.Entries)
                {
                    _entryCache.Add( new ArchiveItemInfo(zipArchiveEntry, this) );
                }
            }
            catch(Exception e)
            {
                throw e;
            }
            finally
            {
                UnlockArchive(ArchiveProviderStrings.GetChildItemsAction, true);
            }
        }

        #endregion ItemCache

        #region ItemHandler

		public IEnumerable<ArchiveItemInfo> GetItem()
        {
            if (_entryCache == null)
            {
                UpdateCache();
            }
            //UpdateCache();
            foreach (ArchiveItemInfo item in _entryCache)
            {
                yield return item;
            }
        }

        public IEnumerable<ArchiveItemInfo> GetItem(string path)
        {
            IEnumerable<ArchiveItemInfo> results = GetItem();

			path = PathUtils.TrimEndingDirectorySeparator(path).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            WildcardPattern wildcardPattern = WildcardPattern.Get(path, WildcardOptions.IgnoreCase | WildcardOptions.Compiled);

            foreach (ArchiveItemInfo item in results)
            {
                if (wildcardPattern.IsMatch(PathUtils.TrimEndingDirectorySeparator( item.FullArchiveName )))
                {
                    yield return item;
                }
            }
        }

        public IEnumerable<ArchiveItemInfo> GetItem(string path, bool directory, bool file)
        {
            IEnumerable<ArchiveItemInfo> results = GetItem(path);
            WildcardPattern wildcardPattern = WildcardPattern.Get(path, WildcardOptions.IgnoreCase | WildcardOptions.Compiled);
            path = path.TrimStart(Path.AltDirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);

            foreach (ArchiveItemInfo item in results)
            {
                if ( Path.GetDirectoryName(path) != Path.GetDirectoryName( PathUtils.TrimEndingDirectorySeparator(item.FullArchiveName) ) )
                {
                    continue;
                }

                if ((directory && item.IsContainer) || (file && !item.IsContainer))
                {
                    yield return item;
                }

            }
        }

		public bool ItemExists(string path)
		{
			// Return true if either condition is met.
			return ItemExists(path, false) || ItemExists(path, true);
		}

        public bool ItemExists(string path, bool directory)
        {
            path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
			
            List<ArchiveItemInfo> items = GetItem().ToList();

            foreach (ArchiveItemInfo i in items)
            {
                if (!directory && (path == i.FullArchiveName))
                {
                    return true;
                }

                if (directory && PathUtils.EndsInDirectorySeparator(i.FullArchiveName) && (PathUtils.TrimEndingDirectorySeparator(path) == PathUtils.TrimEndingDirectorySeparator(i.FullArchiveName)))
                {
                    return true;
                }
            }
            return false;
        }

		public bool IsItemContainer(string path)
		{
			return ItemExists(path, true);
		}

        #endregion ItemHandler
		public void buildFolderPaths()
        {

            try {
                ZipArchive zipArchive = LockArchive(ArchiveProviderStrings.GetChildItemsAction);

                // Generate a list of items to create
                List<string> dirList = new List<string>();
                foreach (ZipArchiveEntry entry in zipArchive.Entries)
                {
                    string fullName = entry.FullName;
                    if (PathUtils.EndsInDirectorySeparator(fullName))
                    {
                        continue;
                    }

                    fullName = Path.GetDirectoryName(fullName) + Path.AltDirectorySeparatorChar;
                    fullName = fullName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (String.IsNullOrEmpty(fullName))
                    {
                        continue;
                    }
                    var paths = enumFolderPaths(fullName);

                    foreach (string path in paths)
                    {
                        if (zipArchive.GetEntry(path) == null)
                        {
                            if (!dirList.Contains(path))
                            {
                                dirList.Add(path);
                            }
                        }
                    }
                }
                
                // Generate a list of directories
                foreach (string dir in dirList)
                {
                    zipArchive.CreateEntry(dir);
                }

            }
            catch(Exception e) {
                throw e;
            }
            finally {
                UnlockArchive(ArchiveProviderStrings.GetChildItemsAction);
            }
        }

        private static IEnumerable<string> enumFolderPaths(string path)
        {
            int i = 0;
            while((i = path.IndexOf(Path.AltDirectorySeparatorChar, i+1)) > -1)
            {
                yield return path.Substring(0, i+1);
            }
        }
        
    }

}