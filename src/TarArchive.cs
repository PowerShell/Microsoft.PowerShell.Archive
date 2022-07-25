using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Text;

namespace Microsoft.PowerShell.Archive
{
    internal class TarArchive : IArchive
    {
        private bool disposedValue;

        private ArchiveMode _mode;

        private string _path;

        private TarWriter _tarWriter;

        private FileStream _fileStream;

        ArchiveMode IArchive.Mode => _mode;

        string IArchive.Path => _path;

        public TarArchive(string path, ArchiveMode mode, FileStream fileStream)
        {
            _mode = mode;
            _path = path;
            _tarWriter = new TarWriter(archiveStream: fileStream, format: TarEntryFormat.Pax, leaveOpen: false);
            _fileStream = fileStream;
        }

        void IArchive.AddFilesytemEntry(ArchiveAddition entry)
        {
            _tarWriter.WriteEntry(fileName: entry.FileSystemInfo.FullName, entryName: entry.EntryName);
        }

        string[] IArchive.GetEntries()
        {
            throw new NotImplementedException();
        }

        void IArchive.Expand(string destinationPath)
        {
            throw new NotImplementedException();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _tarWriter.Dispose();
                    _fileStream.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
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
