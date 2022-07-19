using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerShell.Archive
{
    internal class TarArchive : IArchive
    {
        private bool disposedValue;

        private ArchiveMode _mode;

        private string _path;

        ArchiveMode IArchive.Mode => throw new NotImplementedException();

        string IArchive.Path => throw new NotImplementedException();

        void IArchive.AddFilesytemEntry(ArchiveAddition entry)
        {
            throw new NotImplementedException();
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
