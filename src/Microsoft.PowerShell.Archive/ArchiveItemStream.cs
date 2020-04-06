using System;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.IO.Compression;

namespace Microsoft.PowerShell.Archive
{
    #region ArchiveItemStream
    public class ArchiveItemStream : System.IO.Stream
    {

        private ArchiveItemInfo _itemInfo;
        public System.IO.Stream _stream;

        public bool _isClosed;
        public override long Length {
            get
            {
                return _stream.Length;
            }
        }
        public override long Position {
            get
            {
                return _stream.Position;
            }
            set
            {
                _stream.Position = value;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return _stream.CanSeek;
            }
        }
        public override bool CanRead {
            get
            {
                return _stream.CanRead;
            }
        }

        public override bool CanWrite {
            get
            {
                return _stream.CanWrite;
            }
        }

        public override void Flush()
        {
            _stream.Flush();
        }
        public override void SetLength(long value)
        {
            _stream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _stream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public ArchiveItemStream(ArchiveItemInfo entry)
        {
            _itemInfo = entry;

            ZipArchive archive = _itemInfo.Drive.LockArchive(_itemInfo.ArchiveEntry.FullName);
            
            _stream = archive.GetEntry(_itemInfo.ArchiveEntry.FullName).Open();
            // Sets position to 0 so it can be fresh
            _stream.Position = 0;
        }
        public override void Close()
        {
            if (!_isClosed)
            {
                _stream.Flush();
                _stream.Dispose();

                _itemInfo.Drive.UnlockArchive(_itemInfo.ArchiveEntry.FullName);

                _isClosed = true;
                base.Close();

                GC.Collect();
            }

        }

        public void Dispose()
        {
            base.Dispose();
        }

        ~ArchiveItemStream()
        {
            Dispose();
        }
    }
    #endregion ArchiveItemStream
}