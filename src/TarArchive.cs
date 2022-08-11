// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Diagnostics;

namespace Microsoft.PowerShell.Archive
{
    internal class TarArchive : IArchive
    {
        private bool _disposedValue;

        private readonly ArchiveMode _mode;

        private readonly string _path;

        private TarWriter? _tarWriter;

        private TarReader? _tarReader;

        private readonly FileStream _fileStream;

        private FileStream? _copyStream;

        private string? _copyPath;

        ArchiveMode IArchive.Mode => _mode;

        string IArchive.Path => _path;

        public TarArchive(string path, ArchiveMode mode, FileStream fileStream)
        {
            _mode = mode;
            _path = path;
            _fileStream = fileStream;
        }

        void IArchive.AddFileSystemEntry(ArchiveAddition entry)
        {
            if (_mode == ArchiveMode.Extract)
            {
                throw new ArgumentException("Adding entries to the archive is not supported on Extract mode.");
            }
            
            // If the archive is in Update mode, we want to update the archive by copying it to a new archive
            // and then adding the entries to that archive
            if (_mode == ArchiveMode.Update)
            {
                if (_copyStream is null)
                {
                    CreateCopyStream();
                }       
            }
            else if (_tarWriter is null)
            {
                _tarWriter = new TarWriter(_fileStream, TarEntryFormat.Pax, true);
            }

            // Replace '\' with '/'
            var entryName = entry.EntryName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            Debug.Assert(_tarWriter is not null);
            _tarWriter.WriteEntry(fileName: entry.FileSystemInfo.FullName, entryName: entryName); 
        }

        IEntry? IArchive.GetNextEntry()
        {
            // If _tarReader is null, create it
            if (_tarReader is null)
            {
                _fileStream.Position = 0;
                _tarReader = new TarReader(archiveStream: _fileStream, leaveOpen: true);
            }
            var entry = _tarReader.GetNextEntry();
            if (entry is null)
            {
                return null;
            }
            // Create and return a TarArchiveEntry, which is a wrapper around entry
            return new TarArchiveEntry(entry);
        }

        private void CreateCopyStream() {
            // Determine an appropritae and random filenname
            string copyName = Path.GetRandomFileName();

            // Directory of the copy will be the same as the directory of the archive
            string? directory = Path.GetDirectoryName(_path);
            Debug.Assert(directory is not null);

            _copyPath = Path.Combine(directory, copyName);
            _copyStream = new FileStream(_copyPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

            // Create a tar reader that will read the contents of the archive
            _tarReader = new TarReader(_fileStream, leaveOpen: false);

            // Create a tar writer that will write the contents of the archive to the copy
            _tarWriter = new TarWriter(_copyStream, TarEntryFormat.Pax, true);

            var entry = _tarReader.GetNextEntry();
            while (entry is not null)
            {
                _tarWriter.WriteEntry(entry);
                entry = _tarReader.GetNextEntry();
            }
        }

        private void ReplaceArchiveWithCopy() {
            Debug.Assert(_copyPath is not null);
            // Delete the archive
            File.Delete(_path);
            // Move copy to archive path
            File.Move(_copyPath, _path);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _tarWriter?.Dispose();
                    _copyStream?.Dispose();
                    _tarReader?.Dispose();
                    _fileStream.Dispose();

                    if (_mode == ArchiveMode.Update)
                    {
                        ReplaceArchiveWithCopy();
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        bool IArchive.HasTopLevelDirectory()
        {
            // Go through each entry and see if it is a top-level entry
            _tarReader = new TarReader(_fileStream, leaveOpen: true);

            int topLevelDirectoriesCount = 0;
            var entry = _tarReader.GetNextEntry();
            while (entry is not null) {
                
                if (entry.EntryType == TarEntryType.Directory)
                {
                    topLevelDirectoriesCount++;
                    if (topLevelDirectoriesCount > 1)
                    {
                        break;
                    }
                } else
                {
                    _tarReader.Dispose();
                    _tarReader = null;
                    return false;
                }
                entry = _tarReader.GetNextEntry();
            }

            _tarReader.Dispose();
            _tarReader = null;
            return topLevelDirectoriesCount == 1;
        }

        internal class TarArchiveEntry : IEntry {
            
            // Underlying object is System.Formats.Tar.TarEntry
            private TarEntry _entry;

            private IEntry _objectAsIEntry;

            string IEntry.Name => _entry.Name;

            bool IEntry.IsDirectory => _entry.EntryType == TarEntryType.Directory;

            public TarArchiveEntry(TarEntry entry)
            {
                _entry = entry;
                _objectAsIEntry = this;
            }

            void IEntry.ExpandTo(string destinationPath)
            {
                // If the parent directory does not exist, create it
                string? parentDirectory = Path.GetDirectoryName(destinationPath);
                if (parentDirectory is not null && !Directory.Exists(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }

                if (_objectAsIEntry.IsDirectory)
                {
                    System.IO.Directory.CreateDirectory(destinationPath);
                    var lastWriteTime = _entry.ModificationTime;
                    System.IO.Directory.SetLastWriteTime(destinationPath, lastWriteTime.DateTime);
                } else
                {
                    _entry.ExtractToFile(destinationPath, overwrite: false);
                }
            }

            private void SetFileAttributes(string destinationPath) {
                
            }
        }
    }
}
