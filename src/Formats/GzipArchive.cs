// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;

namespace Microsoft.PowerShell.Archive
{
    internal class GzipArchive : IArchive
    {
        private bool _disposedValue;

        private readonly ArchiveMode _mode;

        private readonly string _path;

        private readonly FileStream _fileStream;

        private readonly CompressionLevel _compressionLevel;

        private bool _addedFile;

        private bool _didCallGetNextEntry;

        ArchiveMode IArchive.Mode => _mode;

        string IArchive.Path => _path;

        public GzipArchive(string path, ArchiveMode mode, FileStream fileStream, CompressionLevel compressionLevel)
        {
            _mode = mode;
            _path = path;
            _fileStream = fileStream;
            _compressionLevel = compressionLevel;
        }

        public void AddFileSystemEntry(ArchiveAddition entry)
        {
            if (_mode == ArchiveMode.Extract)
            {
                throw new ArgumentException("Adding entries to the archive is not supported on Extract mode.");
            }
            if (_mode == ArchiveMode.Update)
            {
                throw new ArgumentException("Updating a Gzip file in not supported.");
            }
            if (_addedFile)
            {
                throw new ArgumentException("Adding a Gzip file in not supported.");
            }
            if (entry.FileSystemInfo.Attributes.HasFlag(FileAttributes.Directory)) {
                throw new ArgumentException("Compressing directories is not supported");
            }
            using var gzipCompressor = new GZipStream(_fileStream, _compressionLevel, leaveOpen: true);
            using var fileToCopy = new FileStream(entry.FileSystemInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            fileToCopy.CopyTo(gzipCompressor);
            _addedFile = true;
        }

        public IEntry? GetNextEntry()
        {
            // Gzip has no concept of entries
            if (!_didCallGetNextEntry) {
                _didCallGetNextEntry = true;
                return new GzipArchiveEntry(this);
            }
            return null;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _fileStream.Dispose();
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

        internal class GzipArchiveEntry : IEntry {
            
            private GzipArchive _gzipArchive;

            // Gzip has no concept of entries, so getting the entry name is not supported
            string IEntry.Name => throw new NotSupportedException();

            // Gzip does not compress directories, so this is always false
            bool IEntry.IsDirectory => false;

            public GzipArchiveEntry(GzipArchive gzipArchive)
            {
                _gzipArchive = gzipArchive;
            }

            void IEntry.ExpandTo(string destinationPath)
            {
                using var destinationFileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var gzipDecompressor = new GZipStream(_gzipArchive._fileStream, CompressionMode.Decompress);
                gzipDecompressor.CopyTo(destinationFileStream);
            }
        }
    }
}
