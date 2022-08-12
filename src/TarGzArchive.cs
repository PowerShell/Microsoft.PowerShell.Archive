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
    internal class TarGzArchive : IArchive
    {
        private bool _disposedValue;

        private readonly ArchiveMode _mode;

        private readonly string _path;

        private readonly FileStream _fileStream;

        private readonly CompressionLevel _compressionLevel;

        // Use a tar archive because .tar.gz file is a compressed tar file
        private TarArchive _tarArchive;

        ArchiveMode IArchive.Mode => _mode;

        string IArchive.Path => _path;

        public TarGzArchive(string path, ArchiveMode mode, FileStream fileStream, CompressionLevel compressionLevel)
        {
            _mode = mode;
            _path = path;
            _fileStream = fileStream;
            _compressionLevel = compressionLevel;
        }

        void IArchive.AddFileSystemEntry(ArchiveAddition entry)
        {
            if (_mode == ArchiveMode.Create) {
                
            }
        }

        IEntry? IArchive.GetNextEntry()
        {
            // Gzip has no concept of entries
            if (!_didCallGetNextEntry) {
                _didCallGetNextEntry = true;
                return new TarGzArchiveEntry(this);
            }
            return null;
        }

        protected virtual void Dispose(bool disposing)
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

        bool IArchive.HasTopLevelDirectory()
        {
            throw new NotSupportedException();
        }

        internal class TarGzArchiveEntry : IEntry {
            
            private TarGzArchive _gzipArchive;

            // Gzip has no concept of entries, so getting the entry name is not supported
            string IEntry.Name => throw new NotSupportedException();

            // Gzip does not compress directories, so this is always false
            bool IEntry.IsDirectory => false;

            public TarGzArchiveEntry(TarGzArchive gzipArchive)
            {
                _gzipArchive = gzipArchive;
            }

            void IEntry.ExpandTo(string destinationPath)
            {
                using var destinationFileStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var gzipDecompressor = new GZipStream(_gzipArchive._fileStream, CompressionMode.Decompress);
                gzipDecompressor.CopyTo(destinationFileStream);
            }
        }
    }
}
