// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;

namespace Microsoft.PowerShell.Archive
{
    internal class TarGzArchive : IArchive
    {

        // Use a tar archive because .tar.gz file is a compressed tar file
        private TarArchive? _tarArchive;

        private FileStream? _tarFileStream;

        private string? _tarFilePath;

        private string _path;

        private bool _disposedValue;

        private bool _didCallGetNextEntry;

        private readonly ArchiveMode _mode;

        private readonly FileStream _fileStream;

        private readonly CompressionLevel _compressionLevel;

        public string Path => _path;

        ArchiveMode IArchive.Mode => _mode;

        string IArchive.Path => _path;

        public TarGzArchive(string path, ArchiveMode mode, FileStream fileStream, CompressionLevel compressionLevel)
        {
            _path = path;
            _mode = mode;
            _fileStream = fileStream;
            _compressionLevel = compressionLevel;
        }

        public void AddFileSystemEntry(ArchiveAddition entry)
        {
            if (_mode == ArchiveMode.Extract || _mode == ArchiveMode.Update)
            {
                throw new ArgumentException("Adding entries to the archive is not supported in extract or update mode");
            }

            if (_tarArchive is null)
            {
                // This will create a temp file and return the path
                _tarFilePath = System.IO.Path.GetTempFileName();
                // When creating the stream, the file already exists
                _tarFileStream = new FileStream(_tarFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                _tarArchive = new TarArchive(_tarFilePath, ArchiveMode.Create, _tarFileStream);
                
            }
            _tarArchive.AddFileSystemEntry(entry);
        }

        public IEntry? GetNextEntry()
        {
            if (_mode == ArchiveMode.Create)
            {
                throw new ArgumentException("Getting next entry is not supported when the archive is in Create mode");
            }

            if (_tarArchive is null)
            {
                // Create a Gzip archive
                using var gzipArchive = new GzipArchive(_path, _mode, _fileStream, _compressionLevel);
                // Where to put the tar file when expanding the tar.gz archive
                _tarFilePath = System.IO.Path.GetTempFileName();
                // Expand the gzip portion
                var entry = gzipArchive.GetNextEntry();
                Debug.Assert(entry is not null);
                entry.ExpandTo(_tarFilePath);
                // Create a TarArchive pointing to the newly expanded out tar file from the tar.gz file
                FileStream tarFileStream = new FileStream(_tarFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                _tarArchive = new TarArchive(_tarFilePath, ArchiveMode.Extract, tarFileStream);
            }
            return _tarArchive?.GetNextEntry();
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        // Performs gzip compression on _path
        private void CompressArchive() {
            Debug.Assert(_tarFilePath is not null);
            _tarFileStream = new FileStream(_tarFilePath, FileMode.Open, FileAccess.Read);
            using var gzipCompressor = new GZipStream(_fileStream, _compressionLevel, true);
            _tarFileStream.CopyTo(gzipCompressor);
            _tarFileStream.Dispose();
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // Do this before compression because disposing a tar archive will add necessary EOF markers
                    _tarArchive?.Dispose();
                    if (_mode == ArchiveMode.Create) {
                        CompressArchive();
                    }
                    _fileStream.Dispose();
                    if (_tarFilePath is not null) {
                        // Delete the tar file created in the process of created the tar.gz file
                        File.Delete(_tarFilePath);
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }
    }
}
