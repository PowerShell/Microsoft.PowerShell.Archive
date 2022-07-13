using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerShell.Archive
{
    internal class ZipArchive : IArchive
    {
        private bool disposedValue;

        private ArchiveMode _mode;

        private string _archivePath;

        private System.IO.Compression.ZipArchive _zipArchive;

        ArchiveMode Mode => _mode;

        string ArchivePath => _archivePath;

        public ZipArchive(string archivePath, ArchiveMode mode, System.IO.FileStream _archiveStream)
        {
            disposedValue = false;
            _mode = mode;
            _archivePath = archivePath;
            _zipArchive = new System.IO.Compression.ZipArchive(_archiveStream, )
        }

        void AddFilesytemEntry(ArchiveEntry entry)
        {
            throw new NotImplementedException();
        }

        string[] GetEntries()
        {
            throw new NotImplementedException();
        }

        void Expand(string destinationPath)
        {
            throw new NotImplementedException();
        }

        private System.IO.Compression.ZipArchiveMode GetZipArchiveMode(ArchiveMode archiveMode)
        {

        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
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
