using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Internal;

namespace Microsoft.PowerShell.Archive
{
    #region ArchiveItemInfo
    public class ArchiveItemInfo
    {
        //Public Extension info
        
        //public DateTime        CreationTime;                   // {get;set;}
        //public DateTime        CreationTimeUtc;                // {get;set;}
        public ArchivePSDriveInfo Drive {
            get;
            private set;
        }

        public DirectoryInfo Directory;                      // {get;}

        public string DirectoryName
        {
            get {
                if (IsContainer)
                {
                    return Path.GetDirectoryName(PathUtils.TrimEndingDirectorySeparator(FullName));
                }

                return Path.GetDirectoryName(FullName);
            }
        }

        public bool Exists {
            get {
                return true;
            }
        }

        public object Crc32 {
            get {
                return null; //archiveEntry.Crc32;
            }
        }

        public string Extension {
            get {
                return Path.GetExtension(FullName);
            }
        }

        public string BaseName {
            get {
                return Path.GetFileNameWithoutExtension(FullName);
            }
        }

        public string FullName {
            get {
                return String.Format("{0}:\\{1}", Drive.Name, ArchiveEntry.FullName).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
        }

        public string FullArchiveName {
            get {
                return ArchiveEntry.FullName.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
        }

        public bool IsReadOnly
        {
            get {
                return false;
            }
            set {

            }
        }

        //public DateTime        LastAccessTime;                 // {get;set;}
        //public DateTime        LastAccessTimeUtc;              // {get;set;}
        public DateTime LastWriteTime
        {
            get {
                return ArchiveEntry.LastWriteTime.DateTime;
            }
            set {
                // Todo: Fix writetime so it updates the archive as well
                ArchiveEntry.LastWriteTime = new DateTimeOffset(value);
            }
        }

        public DateTime LastWriteTimeUtc
        {
            get {
                return this.LastWriteTime.ToUniversalTime();
            }
            set {
                this.LastWriteTime = value.ToLocalTime();
            }
        }

        public long Length {
            get {
                return ArchiveEntry.Length;
            }
        }

        public long CompressedLength {
            get {
                return ArchiveEntry.CompressedLength;
            }
        }

        public string Name {
            get {
                if (IsContainer)
                {
                    return Path.GetFileName(PathUtils.TrimEndingDirectorySeparator(ArchiveEntry.FullName));
                }
                return ArchiveEntry.Name;
            }
        }

        internal ZipArchive Archive {
            get {
                if (ArchiveEntry.Archive.Entries.Count == 0)
                {
                    return null;
                }
                return ArchiveEntry.Archive;
            }
        }

        internal ZipArchiveEntry ArchiveEntry {
            get;
            private set;

        }

        public FileInfo FileSystemContainer {
            get {
                return new FileInfo(Drive.Root);
            }
        }

        public bool IsContainer {
            get {
                return PathUtils.EndsInDirectorySeparator(ArchiveEntry.FullName);
            }
        }

        public ArchiveItemInfo(ZipArchiveEntry item, ArchivePSDriveInfo drive)
        {
            Drive = drive;
            ArchiveEntry = item;
        }

        public ArchiveItemInfo(ArchivePSDriveInfo drive, string path) : this(drive, path, false)
        {

        }

        public ArchiveItemInfo(ArchivePSDriveInfo drive, string path, bool createEntry)
        {

            if (String.IsNullOrEmpty(path))
            {
                throw PSTraceSource.NewArgumentNullException("path");
            }

            Drive = drive;

            if (path.StartsWith(Drive.Name))
            {
                path = Path.GetRelativePath(Drive.Name + ":\\", path);
            }
            // Path.VolumeSeparatorChar defaults to a / in ubuntu
            if (path.Contains( ":" ))
            {
                throw PSTraceSource.NewArgumentException(path);
            }

            path = path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            try {
                ZipArchive zipArchive = drive.LockArchive(ArchiveProviderStrings.GetChildItemsAction);
                ArchiveEntry = zipArchive.GetEntry(path);

                if (ArchiveEntry == null)
                {
                    if (createEntry == true)
                    {
                        // Create an entry if not exists
                        ArchiveEntry = zipArchive.CreateEntry(path);
                        //ArchiveEntry = zipArchive.GetEntry(path);

                        if (ArchiveEntry == null)
                        {
                            throw new IOException(ArchiveProviderStrings.PermissionError);
                        }
                    }
                    else
                    {
                        string error = String.Format(ArchiveProviderStrings.ItemNotFound, path);
                        throw new FileNotFoundException(error);
                    }

                }

            }
            catch(Exception e) {
                throw e;
            }
            finally {
                drive.UnlockArchive(ArchiveProviderStrings.GetChildItemsAction);
            }

        }

        public StreamWriter AppendText()
        {
            return new StreamWriter( OpenWrite() );
        }

        public void CopyTo(string destFileName)
        {
            CopyTo(destFileName, false, false);
        }
        
        public void CopyTo(string destFileName, bool overwrite)
        {
            CopyTo(destFileName, false,  overwrite);
        }
        //Create                    Method         System.IO.FileStream Create()
        //CreateObjRef              Method         System.Runtime.Remoting.ObjRef CreateObjRef(type requestedType)        

        public StreamWriter CreateText()
        {
            return new StreamWriter( OpenWrite() );
        }
        
        public void Delete()
        {
            try {
                ZipArchive zipArchive = Drive.LockArchive(ArchiveEntry.FullName);
                ZipArchiveEntry zipArchiveEntry = zipArchive.GetEntry(ArchiveEntry.FullName);
                zipArchiveEntry.Delete();
            }
            catch {

            }
            finally {
                Drive.UnlockArchive(ArchiveEntry.FullName);
            }

        }

        public void Decrypt()
        {
            throw new NotImplementedException();
        }
        
        public void Encrypt()
        {
            throw new NotImplementedException();
        }

        
        //GetAccessControl          Method         System.Security.AccessControl.FileSecurity GetAccessControl(), System.Secur...
        //GetHashCode               Method         int GetHashCode()
        //GetLifetimeService        Method         System.Object GetLifetimeService()
        //GetObjectData             Method         void GetObjectData(System.Runtime.Serialization.SerializationInfo info, Sys...
        //GetType                   Method         type GetType()
        //InitializeLifetimeService Method         System.Object InitializeLifetimeService()
        

        public void MoveTo(string destFileName)
        {
            CopyTo(destFileName, true, false);
        }

        internal void CopyTo(string destFileName, bool removeItem, bool overwrite)
        {
            // if (destFileName.Contains(Path.GetInvalidPathChars()) || destFileName.Contains(Path.GetInvalidFileNameChars())
            if (destFileName.IndexOfAny(Path.GetInvalidPathChars()) != -1)
            {
                throw new InvalidDataException("Path contains invalid characters");
            }

            // Convert Path to its proper dest path
            destFileName = destFileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            
            // If the destination file is a folder
            // We should move/copy the item to that folder.
            // Example:
            // Move-Item Provider:\a\b\c\file.txt .\d\e\f
            // Will move the file to Provider:\d\e\f\file.txt
            if (destFileName.EndsWith(Path.AltDirectorySeparatorChar))
            {
                destFileName = $"{destFileName}{ArchiveEntry.Name}";
            }

            // Validate if path is filesystem
            if (Path.IsPathRooted(destFileName) && !destFileName.StartsWith(Drive.Name))
            {
                CopyToFileSystem(destFileName, removeItem, overwrite);
                return;
            }
 
            // Cleanup the filesystem path
            if (destFileName.StartsWith(Drive.Name))
            {
                destFileName = Path.GetRelativePath((Drive.Name + ":\\"), destFileName);
            }
            else if (destFileName.StartsWith(Drive.Root))
            {
                destFileName = Path.GetRelativePath(Drive.Root, destFileName);
            }

            CopyToArchive(destFileName, removeItem, overwrite);
        }
        
        internal void CopyToFileSystem(string destFileName, bool removeItem, bool overwrite)
        {
            if (File.Exists(destFileName) && !overwrite) 
            {
                throw new Exception($"The item exists '{destFileName}'");
            }

            ZipArchive zipArchive = Drive.LockArchive(FullArchiveName);
            ZipArchiveEntry thisEntry = zipArchive.GetEntry(ArchiveEntry.FullName);

            thisEntry.ExtractToFile(destFileName);

            if (removeItem)
            {
                thisEntry.Delete();
            }

            Drive.UnlockArchive(FullArchiveName);
        }

        internal void CopyToArchive(string destFileName, bool removeItem, bool overwrite)
        {
            ZipArchive zipArchive = Drive.LockArchive(FullArchiveName);

            ZipArchiveEntry thisEntry = zipArchive.GetEntry(ArchiveEntry.FullName);
            ZipArchiveEntry newEntry = zipArchive.GetEntry(destFileName);

            // Determine if Overwrite is enabled and item exists.
            if ((overwrite == false) && (newEntry != null))
            {
                throw new Exception($"The item exists '{destFileName}'");
            }

            if (newEntry == null) {
                newEntry = zipArchive.CreateEntry(destFileName);
            }

            using (Stream thisStream = thisEntry.Open())
                using (Stream newStream = newEntry.Open())
                {
                    thisStream.CopyTo(newStream);
                }
            if (removeItem)
            {
                thisEntry.Delete();
            }

            Drive.UnlockArchive(FullArchiveName);

        }

        public ArchiveItemStream Open()
        {
            return new ArchiveItemStream(this);
        }

        public ArchiveItemStream Open(FileMode mode)
        {
            return new ArchiveItemStream(this);
        }

        public ArchiveItemStream Open(FileMode mode, FileAccess access)
        {
            throw new NotImplementedException();
        }

        public ArchiveItemStream Open(FileMode mode, FileAccess access, FileShare share)
        {
            throw new NotImplementedException();
        }

        public ArchiveItemStream OpenRead()
        {
            return Open();
        }

        public StreamReader OpenText()
        {
            return new StreamReader(Open());
        }

        public ArchiveItemStream OpenWrite()
        {
            return Open();
        }

        //Refresh                   Method         void Refresh()
        //Replace                   Method         System.IO.FileInfo Replace(string destinationFileName, string destinationBa...
        //SetAccessControl          Method         void SetAccessControl(System.Security.AccessControl.FileSecurity fileSecurity)

        public string ReadToEnd()
        {
            string result;
            using (ArchiveItemStream stream = Open(FileMode.Append))
            using (StreamReader streamReader = new StreamReader(stream))
            {
                result = streamReader.ReadToEnd();
            }
            return result;
        }

        internal void ClearContent()
        {
            ArchiveItemStream fileStream = Open(FileMode.Append);
            fileStream.Seek(0, SeekOrigin.Begin);
            fileStream.SetLength(0);
            fileStream.Flush();
            fileStream.Close();
            fileStream.Dispose();
        }

    }
    #endregion ArchiveItemInfo
}