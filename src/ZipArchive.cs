﻿using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

namespace Microsoft.PowerShell.Archive
{
    internal class ZipArchive : IArchive
    {
        private bool disposedValue;

        private ArchiveMode _mode;

        private string _archivePath;

        private System.IO.FileStream _archiveStream;

        private System.IO.Compression.ZipArchive _zipArchive;

        private System.IO.Compression.CompressionLevel _compressionLevel;

        private const char ZipArchiveDirectoryPathTerminator = '/';

        ArchiveMode IArchive.Mode => _mode;

        string IArchive.Path => _archivePath;

        public ZipArchive(string archivePath, ArchiveMode mode, System.IO.FileStream archiveStream, CompressionLevel compressionLevel)
        {
            disposedValue = false;
            _mode = mode;
            _archivePath = archivePath;
            _archiveStream = archiveStream;
            _zipArchive = new System.IO.Compression.ZipArchive(stream: archiveStream, mode: ConvertToZipArchiveMode(_mode), leaveOpen: true);
            _compressionLevel = compressionLevel;
        }

        // If a file is added to the archive when it already contains a folder with the same name,
        // it is up to the extraction software to deal with it (this is how it's done in other archive software).
        // The .NET API differentiates a file and folder based on the last character being '/'. In other words, if the last character in a path is '/', it is treated as a folder.
        // Otherwise, the .NET API treats the path as a file.
        void IArchive.AddFilesytemEntry(ArchiveAddition addition)
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
            if (addition.Type == ArchiveAddition.ArchiveAdditionType.Directory)
            {
                // If the archive does not have an entry with the same name, then add an entry for the directory
                if (entryInArchive == null)
                {
                    // Ensure addition.entryName has '/' at the end
                    if (!addition.EntryName.EndsWith(ZipArchiveDirectoryPathTerminator))
                    {
                        addition.EntryName += ZipArchiveDirectoryPathTerminator;
                    }
                    _zipArchive.CreateEntry(entryName);
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
                _zipArchive.CreateEntryFromFile(sourceFileName: addition.FullPath, entryName: entryName, compressionLevel: _compressionLevel);
            }
        }

        string[] IArchive.GetEntries()
        {
            throw new NotImplementedException();
        }

        void IArchive.Expand(string destinationPath)
        {
            throw new NotImplementedException();
        }

        private System.IO.Compression.ZipArchiveMode ConvertToZipArchiveMode(ArchiveMode archiveMode)
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
            if (!disposedValue)
            {
                if (disposing)
                {
                    _zipArchive.Dispose();
                    _archiveStream.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}