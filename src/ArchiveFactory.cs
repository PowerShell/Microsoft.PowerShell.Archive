using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net;
using System.Text;

namespace Microsoft.PowerShell.Archive
{
    internal static class ArchiveFactory
    {
        internal static IArchive GetArchive(ArchiveFormat format, string archivePath, ArchiveMode archiveMode, System.IO.Compression.CompressionLevel compressionLevel)
        {
            System.IO.FileStream archiveFileStream = archiveMode switch
            {
                ArchiveMode.Create => new System.IO.FileStream(archivePath, mode: System.IO.FileMode.CreateNew, access: System.IO.FileAccess.Write, share: System.IO.FileShare.None),
                ArchiveMode.Update => new System.IO.FileStream(archivePath, mode: System.IO.FileMode.Open, access: System.IO.FileAccess.ReadWrite, share: System.IO.FileShare.None),
                ArchiveMode.Read => new System.IO.FileStream(archivePath, mode: System.IO.FileMode.Open, access: System.IO.FileAccess.Read, share: System.IO.FileShare.Read),
                // TODO: Add message to exception
                _ => throw new NotImplementedException()
            };

            return format switch
            {
                ArchiveFormat.zip => new ZipArchive(archivePath, archiveMode, archiveFileStream, compressionLevel),
                // TODO: Add archive types here
                // TODO: Add message to exception
                _ => throw new NotImplementedException()
            };
        }
    }
}
