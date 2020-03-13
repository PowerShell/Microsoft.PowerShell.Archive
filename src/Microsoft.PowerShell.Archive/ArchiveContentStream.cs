
using Microsoft.PowerShell.Commands;
using System;
using System.IO;
using System.IO.Compression;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Text;

namespace Microsoft.PowerShell.Archive
{
    #region StreamContent

    #region ArchiveContentStream
    public class ArchiveContentStream : StreamContentReaderWriter
    {

        private ArchiveItemInfo _archiveFileInfo;
        private ArchiveItemStream _archiveFileStream;

        private ArchiveItemStream stream;
        private CmdletProvider _provider;


        public ArchiveContentStream(ArchiveItemInfo archiveFileInfo, FileMode mode, Encoding encoding, bool usingByteEncoding, CmdletProvider provider, bool isRawStream)
        : base( archiveFileInfo.Open(mode), encoding, usingByteEncoding, provider, isRawStream)
        {
            _provider = provider;
        }

        public ArchiveContentStream(ArchiveItemInfo archiveFileInfo, FileMode mode, Encoding encoding, bool usingByteEncoding, CmdletProvider provider, bool isRawStream, bool suppressNewline)
        : base(archiveFileInfo.Open(mode), encoding, usingByteEncoding, provider, isRawStream, suppressNewline)
        {
            _provider = provider;
        }

        public ArchiveContentStream(ArchiveItemInfo archiveFileInfo, FileMode mode, string delimiter, Encoding encoding, bool usingByteEncoding, CmdletProvider provider, bool isRawStream)
        : base(archiveFileInfo.Open(mode), delimiter, encoding, provider, isRawStream)
        {
            _provider = provider;
        }


        ~ArchiveContentStream()
        {

        }

    }
    #endregion ArchiveContentStream

    #endregion StreamContent

}