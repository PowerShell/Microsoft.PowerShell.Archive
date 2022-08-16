// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Microsoft.PowerShell.Archive
{
    internal class ZipArchive : IArchive
    {
        private bool _disposedValue;

        private readonly ArchiveMode _mode;

        private readonly string _archivePath;

        private readonly FileStream _archiveStream;

        private readonly System.IO.Compression.ZipArchive _zipArchive;

        private readonly CompressionLevel _compressionLevel;

        private const char ZipArchiveDirectoryPathTerminator = '/';

        private int _entryIndex;

        ArchiveMode IArchive.Mode => _mode;

        string IArchive.Path => _archivePath;

        internal int NumberOfEntries => _zipArchive.Entries.Count;

        public ZipArchive(string archivePath, ArchiveMode mode, FileStream archiveStream, CompressionLevel compressionLevel)
        {
            _disposedValue = false;
            _mode = mode;
            _archivePath = archivePath;
            _archiveStream = archiveStream;
            _zipArchive = new System.IO.Compression.ZipArchive(stream: archiveStream, mode: ConvertToZipArchiveMode(_mode), leaveOpen: true);
            _compressionLevel = compressionLevel;
            _entryIndex = -1;
        }

        // If a file is added to the archive when it already contains a folder with the same name,
        // it is up to the extraction software to deal with it (this is how it's done in other archive software).
        // The .NET API differentiates a file and folder based on the last character being '/'. In other words, if the last character in a path is '/', it is treated as a folder.
        // Otherwise, the .NET API treats the path as a file.
        void IArchive.AddFileSystemEntry(ArchiveAddition addition)
        {
            if (_mode == ArchiveMode.Extract) throw new InvalidOperationException("Cannot add a filesystem entry to an archive in read mode");

            var entryName = addition.EntryName.Replace(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

            // If the archive has an entry with the same name as addition.EntryName, then get it, so it can be replaced if necessary
            System.IO.Compression.ZipArchiveEntry? entryInArchive = null;
            if (_mode != ArchiveMode.Create)
            {
                // TODO: Add exception handling for _zipArchive.GetEntry
                entryInArchive = _zipArchive.GetEntry(entryName);
            }

            // If the addition is a folder, only create the entry in the archive -- nothing else is needed
            if (addition.FileSystemInfo.Attributes.HasFlag(System.IO.FileAttributes.Directory))
            {
                // If the archive does not have an entry with the same name, then add an entry for the directory
                if (entryInArchive == null)
                {
                    // Ensure addition.entryName has '/' at the end
                    if (!entryName.EndsWith(ZipArchiveDirectoryPathTerminator))
                    {
                        entryName += ZipArchiveDirectoryPathTerminator;
                    }

                    entryInArchive = _zipArchive.CreateEntry(entryName);

                    // Set the last write time
                    if (entryInArchive != null)
                    {
                        var lastWriteTime = addition.FileSystemInfo.LastWriteTime;
                        if (lastWriteTime.Year < 1980 || lastWriteTime.Year > 2107)
                        {
                            lastWriteTime = new DateTime(1980, 1, 1, 0, 0, 0);
                        }
                        entryInArchive.LastWriteTime = lastWriteTime;
                    }
                }
            }
            else
            {
                // If the archive already has an entry with the same name as addition.EntryName, delete it
                if (entryInArchive != null)
                {
                    entryInArchive.Delete();
                }

                // TODO: Add exception handling
                _zipArchive.CreateEntryFromFile(sourceFileName: addition.FileSystemInfo.FullName, entryName: entryName, compressionLevel: _compressionLevel);
            }
        }

        IEntry? IArchive.GetNextEntry()
        {
            if (_entryIndex < 0)
            {
                _entryIndex = 0;
            }

            // If there are no entries, return null
            if (_zipArchive.Entries.Count == 0 || _entryIndex >= _zipArchive.Entries.Count)
            {
                return null;
            }

            // Create an ZipArchive.ZipArchiveEntry object
            var nextEntry = _zipArchive.Entries[_entryIndex];
            _entryIndex++;

            return new ZipArchiveEntry(nextEntry);
        }

        private static System.IO.Compression.ZipArchiveMode ConvertToZipArchiveMode(ArchiveMode archiveMode)
        {
            switch (archiveMode)
            {
                case ArchiveMode.Create: return System.IO.Compression.ZipArchiveMode.Create;
                case ArchiveMode.Update: return System.IO.Compression.ZipArchiveMode.Update;
                case ArchiveMode.Extract: return System.IO.Compression.ZipArchiveMode.Read;
                default: return System.IO.Compression.ZipArchiveMode.Update;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _zipArchive.Dispose();
                    _archiveStream.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        internal class ZipArchiveEntry : IEntry
        {
            // Underlying object is System.IO.Compression.ZipArchiveEntry

            private System.IO.Compression.ZipArchiveEntry _entry;

            string IEntry.Name => _entry.FullName;

            bool IEntry.IsDirectory => _entry.FullName.EndsWith(System.IO.Path.AltDirectorySeparatorChar);

            void IEntry.ExpandTo(string destinationPath)
            {
                // If the parent directory does not exist, create it
                string? parentDirectory = Path.GetDirectoryName(destinationPath);
                if (parentDirectory is not null && !Directory.Exists(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }

                // .NET APIs differentiate a file and directory by a terminating `/`
                // If the entry name ends with `/`, it is a directory
                if (_entry.FullName.EndsWith(System.IO.Path.AltDirectorySeparatorChar))
                {
                    System.IO.Directory.CreateDirectory(destinationPath);
                    var lastWriteTime = _entry.LastWriteTime;
                    System.IO.Directory.SetLastWriteTime(destinationPath, lastWriteTime.DateTime);
                } else
                {
                    _entry.ExtractToFile(destinationPath);
                }
            }

            internal ZipArchiveEntry(System.IO.Compression.ZipArchiveEntry entry)
            {
                _entry = entry;
            }
        }
    }
}
