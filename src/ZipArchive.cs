using System;
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

        ArchiveMode IArchive.Mode => _mode;

        string IArchive.ArchivePath => _archivePath;

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
        // it is up to the extraction software to deal with it (this is how it's done in other archive software)
        void IArchive.AddFilesytemEntry(ArchiveEntry entry)
        {
            if (_mode == ArchiveMode.Read) throw new InvalidOperationException("Cannot add a filesystem entry to an archive in read mode");

            var entryName = entry.Name.Replace(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

            // TODO: Add exception handling for _zipArchive.GetEntry
            var entryInArchive = (_mode == ArchiveMode.Create) ? null : _zipArchive.GetEntry(entryName);
            if (entryName.EndsWith(System.IO.Path.AltDirectorySeparatorChar))
            {
                //Create an entry only
                // TODO: Add exception handling for CreateEntry
                if (entryInArchive == null) _zipArchive.CreateEntry(entryName);
            }
            else
            {
                if (entryInArchive != null)
                {
                    entryInArchive.Delete();
                }
                // TODO: Add exception handling
                _zipArchive.CreateEntryFromFile(sourceFileName: entry.FullPath, entryName: entryName, compressionLevel: _compressionLevel);
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
                case ArchiveMode.Read: return System.IO.Compression.ZipArchiveMode.Read;
                default: return System.IO.Compression.ZipArchiveMode.Update;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _zipArchive.Dispose();
                    _archiveStream.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ZipArchive()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
