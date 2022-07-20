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
                ArchiveMode.Extract => new System.IO.FileStream(archivePath, mode: System.IO.FileMode.Open, access: System.IO.FileAccess.Read, share: System.IO.FileShare.Read),
                // TODO: Add message to exception
                _ => throw new NotImplementedException()
            };

            return format switch
            {
                ArchiveFormat.zip => new ZipArchive(archivePath, archiveMode, archiveFileStream, compressionLevel),
                ArchiveFormat.tar => new TarArchive(archivePath, archiveMode, archiveFileStream),
                // TODO: Add archive types here
                // TODO: Add message to exception
                _ => throw new NotImplementedException()
            };
        }

        internal static bool TryGetArchiveFormatForPath(string path, out ArchiveFormat? archiveFormat)
        {
            archiveFormat = null;
            if (path.EndsWith(".zip"))
            {
                archiveFormat = ArchiveFormat.zip;
            }
            if (path.EndsWith(".tar"))
            {
                archiveFormat = ArchiveFormat.tar;
            }
            if (path.EndsWith(".tar.gz") || path.EndsWith(".tgz"))
            {
                archiveFormat = ArchiveFormat.tgz;
            }
            return archiveFormat != null;
        }
    }
}
