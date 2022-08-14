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
    internal class TarGzArchive : GzipArchive
    {

        // Use a tar archive because .tar.gz file is a compressed tar file
        private TarArchive? _tarArchive;

        private string? _tarFilePath;

        private FileStream? _tarFileStream;

        public TarGzArchive(string path, ArchiveMode mode, FileStream fileStream, CompressionLevel compressionLevel) : base(path, mode, fileStream, compressionLevel)
        {
        }

        public override void AddFileSystemEntry(ArchiveAddition entry)
        {
            if (_mode == ArchiveMode.Extract || _mode == ArchiveMode.Update) {
                throw new ArgumentException("Adding entries to the archive is not supported in extract or update mode");
            }

            if (_tarArchive is null)
            {
                var outputDirectory = Path.GetDirectoryName(_path);
                var tarFilename = Path.GetRandomFileName();
                _tarFilePath = Path.Combine(outputDirectory, tarFilename);
                _tarFileStream = new FileStream(_tarFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                _tarArchive = new TarArchive(_tarFilePath, ArchiveMode.Create, _tarFileStream);
                
            }
            _tarArchive.AddFileSystemEntry(entry);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _fileStream.Dispose();
                    CompressArchive();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // Performs gzip compression on _path
        private void CompressArchive() {
            Debug.Assert(_tarFileStream is not null);
            _tarFileStream.Position = 0;
            using var gzipCompressor = new GZipStream(_fileStream, _compressionLevel, true);
            _tarFileStream.CopyTo(gzipCompressor);
        }
    }
}
