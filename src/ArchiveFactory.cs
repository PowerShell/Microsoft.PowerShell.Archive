// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

namespace Microsoft.PowerShell.Archive
{
    internal static class ArchiveFactory
    {
        internal static IArchive GetArchive(ArchiveFormat format, string archivePath, ArchiveMode archiveMode, System.IO.Compression.CompressionLevel compressionLevel)
        {
            System.IO.FileStream archiveFileStream = archiveMode switch
            {
                ArchiveMode.Create => new FileStream(archivePath, mode: System.IO.FileMode.CreateNew, access: System.IO.FileAccess.Write, share: System.IO.FileShare.None),
                ArchiveMode.Update => new FileStream(archivePath, mode: System.IO.FileMode.Open, access: System.IO.FileAccess.ReadWrite, share: System.IO.FileShare.None),
                ArchiveMode.Extract => new FileStream(archivePath, mode: System.IO.FileMode.Open, access: System.IO.FileAccess.Read, share: System.IO.FileShare.Read),
                _ => throw new ArgumentOutOfRangeException(nameof(archiveMode))
            };

            return format switch
            {
                ArchiveFormat.Zip => new ZipArchive(archivePath, archiveMode, archiveFileStream, compressionLevel),
                //ArchiveFormat.tar => new TarArchive(archivePath, archiveMode, archiveFileStream),
                // TODO: Add Tar.gz here
                _ => throw new ArgumentOutOfRangeException(nameof(archiveMode))
            };
        }

        internal static bool TryGetArchiveFormatFromExtension(string path, out ArchiveFormat? archiveFormat)
        {
            archiveFormat = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".zip" => ArchiveFormat.Zip,
                /* Disable support for tar and tar.gz for preview1 release 
                ".gz" => path.EndsWith(".tar.gz) ? ArchiveFormat.Tgz : null,
                 */
                _ => null
            };
            return archiveFormat is not null;
        }
    }
}
