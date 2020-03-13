
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Management.Automation.Provider;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Dbg = System.Management.Automation;

namespace Microsoft.PowerShell.Archive
{
    /// <summary>
    /// The content stream base class for the Stream provider. It Implements both
    /// the IContentReader and IContentWriter interfaces.
    /// </summary>
    /// <remarks>
    /// Note, this class does no specific error handling. All errors are allowed to
    /// propagate to the caller so that they can be written to the error pipeline
    /// if necessary.
    /// </remarks>
    public class StreamContentReaderWriter : IContentReader, IContentWriter
    {
        private Encoding _encoding;
        private CmdletProvider _provider;
        private Stream _stream;
        private StreamReader _reader;
        private StreamWriter _writer;
        private bool _usingByteEncoding;
        private const char DefaultDelimiter = '\n';
        private string _delimiter = $"{DefaultDelimiter}";
        private int[] _offsetDictionary;
        private bool _usingDelimiter;
        private StringBuilder _currentLineContent;
        private bool _isRawStream;
        private long _fileOffset;

        // The reader to read stream content backward
        private StreamContentBackReader _backReader;

        private bool _alreadyDetectEncoding = false;

        // False to add a newline to the end of the output string, true if not.
        private bool _suppressNewline = false;

        /// <summary>
        /// Constructor for the content stream.
        /// </summary>
        public StreamContentReaderWriter(System.IO.Stream stream, Encoding encoding, bool usingByteEncoding, CmdletProvider provider, bool isRawStream)
        {
            _encoding = encoding;
            _usingByteEncoding = usingByteEncoding;
            _provider = provider;
            _isRawStream = isRawStream;

            CreateStreams(stream, encoding);
        }

        /// <summary>
        /// Constructor for the content stream.
        /// </summary>
        public StreamContentReaderWriter(System.IO.Stream stream, Encoding encoding, bool usingByteEncoding, CmdletProvider provider, bool isRawStream, bool suppressNewline)
            : this(stream, encoding, usingByteEncoding, provider, isRawStream)
        {

            _suppressNewline = suppressNewline;
        }

        /// <summary>
        /// Constructor for the content stream.
        /// </summary>
        /// <param name="stream">
        /// The name of the Alternate Data Stream to get the content from. If null or empty, returns
        /// the file's primary content.
        /// </param>
        /// <param name="delimiter">
        /// The delimiter to use when reading strings. Each time read is called, all contents up to an including
        /// the delimiter is read.
        /// </param>
        /// <param name="encoding">
        /// The encoding of the file to be read or written.
        /// </param>
        /// <param name="provider">
        /// The CmdletProvider invoking this stream
        /// </param>
        /// <param name="isRawStream">
        /// Indicates raw stream.
        /// </param>

        
        public StreamContentReaderWriter(
            System.IO.Stream stream,
            string delimiter,
            Encoding encoding,
            CmdletProvider provider,
            bool isRawStream)
            : this(stream, encoding, false, provider, isRawStream)
        {
            // If the delimiter is default ('\n') we'll use ReadLine() method.
            // Otherwise allocate temporary structures for ReadDelimited() method.
            if (!(delimiter.Length == 1 && delimiter[0] == DefaultDelimiter))
            {
                _delimiter = delimiter;
                _usingDelimiter = true;

                // We expect that we are parsing files where line lengths can be relatively long.
                const int DefaultLineLength = 256;
                _currentLineContent = new StringBuilder(DefaultLineLength);

                // For Boyer-Moore string search algorithm.
                // Populate the offset lookups.
                // These will tell us the maximum number of characters
                // we can read to generate another possible match (safe shift).
                // If we read more characters than this, we risk consuming
                // more of the stream than we need.
                //
                // Because an unicode character size is 2 byte we would to have use
                // very large array with 65535 size to keep this safe offsets.
                // One solution is to pack unicode character to byte.
                // The workaround is to use low byte from unicode character.
                // This allow us to use small array with size 256.
                // This workaround is the fastest and provides excellent results
                // in regular search scenarios when the file contains
                // mostly characters from the same alphabet.
                _offsetDictionary = new int[256];

                // If next char from file is not in search pattern safe shift is the search pattern length.
                for (var n = 0; n < _offsetDictionary.Length; n++)
                {
                    _offsetDictionary[n] = _delimiter.Length;
                }

                // If next char from file is in search pattern we should calculate a safe shift.
                char currentChar;
                byte lowByte;
                for (var i = 0; i < _delimiter.Length; i++)
                {
                    currentChar = _delimiter[i];
                    lowByte = Unsafe.As<char, byte>(ref currentChar);
                    _offsetDictionary[lowByte] = _delimiter.Length - i - 1;
                }
            }
        }

        /// <summary>
        /// Reads the specified number of characters or a lines from the Stream.
        /// </summary>
        /// <param name="readCount">
        /// If less than 1, then the entire Stream is read at once. If 1 or greater, then
        /// readCount is used to determine how many items (ie: lines, bytes, delimited tokens)
        /// to read per call.
        /// </param>
        /// <returns>
        /// An array of strings representing the character(s) or line(s) read from
        /// the Stream.
        /// </returns>
        public IList Read(long readCount)
        {
            //s_tracer.WriteLine("blocks requested = {0}", readCount);

            ArrayList blocks = new ArrayList();
            bool readToEnd = (readCount <= 0);
            bool waitChanges = false;

            if (_alreadyDetectEncoding && _reader.BaseStream.Position == 0)
            {
                Encoding curEncoding = _reader.CurrentEncoding;
                // Close the stream, and reopen the stream to make the BOM correctly processed.
                // The reader has already detected encoding, so if we don't reopen the stream, the BOM (if there is any)
                // will be treated as a regular character.
                // _stream.Dispose();
                CreateStreams(_stream, curEncoding);
                _alreadyDetectEncoding = false;
            }

            try
            {
                for (long currentBlock = 0; (currentBlock < readCount) || (readToEnd); ++currentBlock)
                {

                    if (_usingByteEncoding)
                    {
                        if (!ReadByteEncoded(waitChanges, blocks, false))
                            break;
                    }
                    else
                    {
                        if (_usingDelimiter || _isRawStream)
                        {
                            if (!ReadDelimited(waitChanges, blocks, false, _delimiter))
                                break;
                        }
                        else
                        {
                            if (!ReadByLine(waitChanges, blocks, false))
                                break;
                        }
                    }
                }

                //s_tracer.WriteLine("blocks read = {0}", blocks.Count);
            }
            catch (Exception e)
            {
                if ((e is IOException) ||
                    (e is ArgumentException) ||
                    (e is System.Security.SecurityException) ||
                    (e is UnauthorizedAccessException) ||
                    (e is ArgumentNullException))
                {
                    // Exception contains specific message about the error occured and so no need for errordetails.
                    _provider.WriteError(new ErrorRecord(e, "GetContentReaderIOError", ErrorCategory.ReadError, "System.IO.Stream"));
                    return null;
                }
                else
                    throw;
            }

            return blocks.ToArray();
        }

        /// <summary>
        /// Move the pointer of the stream to the position where there are 'backCount' number
        /// of items (depends on what we are using: delimiter? line? byts?) to the end of the stream.
        /// </summary>
        /// <param name="backCount"></param>
        internal void SeekItemsBackward(int backCount)
        {
            if (backCount < 0)
            {
                // The caller needs to guarantee that 'backCount' is greater or equals to 0
                throw TraceSource.NewArgumentException("backCount");
            }

            //s_tracer.WriteLine("blocks seek backwards = {0}", backCount);

            ArrayList blocks = new ArrayList();
            if (_reader != null)
            {
                // Make the reader automatically detect the encoding
                Seek(0, SeekOrigin.Begin);
                _reader.Peek();
                _alreadyDetectEncoding = true;
            }

            Seek(0, SeekOrigin.End);

            if (backCount == 0)
            {
                // If backCount is 0, we should move the position to the end of the stream.
                // Maybe the "waitForChanges" is true in this case, which means that we are waiting for new inputs.
                return;
            }

            StringBuilder builder = new StringBuilder();
            foreach (char character in _delimiter)
            {
                builder.Insert(0, character);
            }

            string actualDelimiter = builder.ToString();
            long currentBlock = 0;
            string lastDelimiterMatch = null;

            try
            {
                if (_isRawStream)
                {
                    // We always read to the end for the raw data.
                    // If it's indicated as RawStream, we move the pointer to the
                    // beginning of the stream
                    Seek(0, SeekOrigin.Begin);
                    return;
                }

                for (; currentBlock < backCount; ++currentBlock)
                {
                    if (_usingByteEncoding)
                    {
                        if (!ReadByteEncoded(false, blocks, true))
                            break;
                    }
                    else
                    {
                        if (_usingDelimiter)
                        {
                            if (!ReadDelimited(false, blocks, true, actualDelimiter))
                                break;
                            // If the delimiter is at the end of the stream, we need to read one more
                            // to get to the right position. For example:
                            //      ua123ua456ua -- -Tail 1
                            // If we read backward only once, we get 'ua', and cannot get to the right position
                            // So we read one more time, get 'ua456ua', and then we can get the right position
                            lastDelimiterMatch = (string)blocks[0];
                            if (currentBlock == 0 && lastDelimiterMatch.Equals(actualDelimiter, StringComparison.Ordinal))
                                backCount++;
                        }
                        else
                        {
                            if (!ReadByLine(false, blocks, true))
                                break;
                        }
                    }

                    blocks.Clear();
                }

                // If usingByteEncoding is true, we don't create the reader and _backReader
                if (!_usingByteEncoding)
                {
                    long curStreamPosition = _backReader.GetCurrentPosition();
                    if (_usingDelimiter)
                    {
                        if (currentBlock == backCount)
                        {
                            Diagnostics.Assert(lastDelimiterMatch != null, "lastDelimiterMatch should not be null when currentBlock == backCount");
                            if (lastDelimiterMatch.EndsWith(actualDelimiter, StringComparison.Ordinal))
                            {
                                curStreamPosition += _backReader.GetByteCount(_delimiter);
                            }
                        }
                    }

                    Seek(curStreamPosition, SeekOrigin.Begin);
                }

                //s_tracer.WriteLine("blocks seek position = {0}", _stream.Position);
            }
            catch (Exception e)
            {
                if ((e is IOException) ||
                    (e is ArgumentException) ||
                    (e is System.Security.SecurityException) ||
                    (e is UnauthorizedAccessException) ||
                    (e is ArgumentNullException))
                {
                    // Exception contains specific message about the error occured and so no need for errordetails.
                    _provider.WriteError(new ErrorRecord(e, "GetContentReaderIOError", ErrorCategory.ReadError, "System.IO.Stream"));
                }
                else
                    throw;
            }
        }
        private bool ReadByLine(bool waitChanges, ArrayList blocks, bool readBackward)
        {
            // Reading lines as strings
            string line = readBackward ? _backReader.ReadLine() : _reader.ReadLine();

            if (line != null)
                blocks.Add(line);

            int peekResult = readBackward ? _backReader.Peek() : _reader.Peek();
            if (peekResult == -1)
                return false;
            else
                return true;
        }

        private bool ReadDelimited(bool waitChanges, ArrayList blocks, bool readBackward, string actualDelimiter)
        {
            if (_isRawStream)
            {
                // when -Raw is used we want to anyway read the whole thing
                // so avoiding the while loop by reading the entire content.
                string contentRead = _reader.ReadToEnd();

                if (contentRead.Length > 0)
                {
                    blocks.Add(contentRead);
                }

                // We already read whole stream so return EOF.
                return false;
            }


            // Since the delimiter is a string, we're essentially
            // dealing with a "find the substring" algorithm, but with
            // the additional restriction that we cannot read past the
            // end of the delimiter. If we read past the end of the delimiter,
            // then we'll eat up bytes that we need from the stream.
            // The solution is a modified Boyer-Moore string search algorithm.
            // This version retains the sub-linear search performance (via the
            // lookup tables).
            int numRead = 0;
            int currentOffset = actualDelimiter.Length;
            Span<char> readBuffer = stackalloc char[currentOffset];
            bool delimiterNotFound = true;
            _currentLineContent.Clear();

            do
            {
                // Read in the required batch of characters
                numRead = readBackward
                                ? _backReader.Read(readBuffer.Slice(0, currentOffset))
                                : _reader.Read(readBuffer.Slice(0, currentOffset));

                if (numRead > 0)
                {
                                    
                    _currentLineContent.Append(readBuffer.Slice(0, numRead));

                    // Look up the final character in our offset table.
                    // If the character doesn't exist in the lookup table, then it's not in
                    // our search key.  That means the match must happen strictly /after/ the
                    // current position.  Because of that, we can feel confident reading in the
                    // number of characters in the search key, without the risk of reading too many.
                    var currentChar = _currentLineContent[_currentLineContent.Length - 1];
                    currentOffset = _offsetDictionary[currentChar];
                    //currentOffset = _offsetDictionary[Unsafe.As<char, byte>(ref currentChar)];

                    // We want to keep reading if delimiter not found and we haven't hit the end of stream
                    delimiterNotFound = true;

                    // If the final letters matched, then we will get an offset of "0".
                    // In that case, we'll either have a match (and break from the while loop,)
                    // or we need to move the scan forward one position.
                    if (currentOffset == 0)
                    {
                        currentOffset = 1;

                        if (actualDelimiter.Length <= _currentLineContent.Length)
                        {
                            delimiterNotFound = false;
                            int i = 0;
                            int j = _currentLineContent.Length - actualDelimiter.Length;
                            for (; i < actualDelimiter.Length; i++, j++)
                            {
                                if (actualDelimiter[i] != _currentLineContent[j])
                                {
                                    delimiterNotFound = true;
                                    break;
                                }
                            }
                        }
                    }
                }

            } while (delimiterNotFound && (numRead != 0));

            // We've reached the end of stream or end of line.
            if (_currentLineContent.Length > 0)
            {
                // Add the block read to the ouptut array list, trimming a trailing delimiter, if present.
                // Note: If -Tail was specified, we get here in the course of 2 distinct passes:
                //  - Once while reading backward simply to determine the appropriate *start position* for later forward reading, ignoring the content of the blocks read (in reverse).
                //  - Then again during forward reading, for regular output processing; it is only then that trimming the delimiter is necessary.
                //    (Trimming it during backward reading would not only be unnecessary, but could interfere with determining the correct start position.)
                blocks.Add(
                    !readBackward && !delimiterNotFound
                        ? _currentLineContent.ToString(0, _currentLineContent.Length - actualDelimiter.Length)
                        : _currentLineContent.ToString()
                );
            }

            int peekResult = readBackward ? _backReader.Peek() : _reader.Peek();
            if (peekResult != -1)
                return true;
            else
            {
                if (readBackward && _currentLineContent.Length > 0)
                {
                    return true;
                }

                return false;
            }
        }
        private bool ReadByteEncoded(bool waitChanges, ArrayList blocks, bool readBack)
        {
            if (_isRawStream)
            {
                // if RawSteam, read all bytes and return. When RawStream is used, we dont
                // support -first, -last
                byte[] bytes = new byte[_stream.Length];
                int numBytesToRead = (int)_stream.Length;
                int numBytesRead = 0;
                while (numBytesToRead > 0)
                {
                    // Read may return anything from 0 to numBytesToRead.
                    int n = _stream.Read(bytes, numBytesRead, numBytesToRead);

                    // Break when the end of the stream is reached.
                    if (n == 0)
                        break;

                    numBytesRead += n;
                    numBytesToRead -= n;
                }

                if (numBytesRead == 0)
                {
                    return false;
                }
                else
                {
                    blocks.Add(bytes);
                    return true;
                }
            }

            if (readBack)
            {
                if (_stream.Position == 0)
                {
                    return false;
                }

                _stream.Position--;
                blocks.Add((byte)_stream.ReadByte());
                _stream.Position--;
                return true;
            }

            // Reading bytes not strings
            int byteRead = _stream.ReadByte();

            // Add the byte we read to the list of blocks
            if (byteRead != -1)
            {
                blocks.Add((byte)byteRead);
                return true;
            }
            else
                return false;
        }
        private void CreateStreams(Stream stream, Encoding encoding)
        {
            _stream = stream;


            if (!_usingByteEncoding)
            {
                // Open the reader stream
                _reader = new StreamReader(_stream, encoding);
                _backReader = new StreamContentBackReader(_stream, encoding);

                // Open the writer stream
                if (_reader != null)
                {
                    _reader.Peek();
                    encoding = _reader.CurrentEncoding;
                }

                _writer = new StreamWriter(_stream, encoding);
            }
        }

        /// <summary>
        /// Moves the current stream position.
        /// </summary>
        /// <param name="offset">
        /// The offset from the origin to move the position to.
        /// </param>
        /// <param name="origin">
        /// The origin from which the offset is calculated.
        /// </param>
        public void Seek(long offset, SeekOrigin origin)
        {
            if (_writer != null) { _writer.Flush(); }

            _stream.Seek(offset, origin);

            if (_writer != null) { _writer.Flush(); }

            if (_reader != null) { _reader.DiscardBufferedData(); }

            if (_backReader != null) { _backReader.DiscardBufferedData(); }
        }

        public virtual void FinalizeStream()
        {

        }

        /// <summary>
        /// Closes the stream.
        /// </summary>
        public void Close()
        {
            bool streamClosed = false;

            if (_writer != null)
            {
                try
                {
                    _writer.Flush();
                    _writer.Dispose();
                }
                finally
                {
                    streamClosed = true;
                }
            }

            if (_reader != null)
            {
                _reader.Dispose();
                streamClosed = true;
            }

            if (_backReader != null)
            {
                _backReader.Dispose();
                streamClosed = true;
            }
            
            if (!streamClosed)
            {
                _stream.Flush();
                _stream.Dispose();
            }
        }

        /// <summary>
        /// Writes the specified object to the stream.
        /// </summary>
        /// <param name="content">
        /// The objects to write to the stream
        /// </param>
        /// <returns>
        /// The objects written to the stream.
        /// </returns>
        public IList Write(IList content)
        {

            foreach (object line in content)
            {
                object[] contentArray = line as object[];
                if (contentArray != null)
                {
                    foreach (object obj in contentArray)
                    {
                        WriteObject(obj);
                    }
                }
                else
                {
                    WriteObject(line);
                }
            }

            return content;
        }

        private void WriteObject(object content)
        {
            if (content == null)
            {
                return;
            }

            if (_usingByteEncoding)
            {

                try
                {
                    byte byteToWrite = (byte)content;
                    _stream.WriteByte(byteToWrite);
                }
                catch (InvalidCastException)
                {
                    throw TraceSource.NewArgumentException("content", Exceptions.ByteEncodingError);
                }
            }
            else
            {
                if (_suppressNewline)
                {
                    _writer.Write(content.ToString());
                }
                else
                {
                    _writer.WriteLine(content.ToString());
                }
            }
        }

        /// <summary>
        /// Closes the stream.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (_stream != null)
                    _stream.Dispose();
                if (_reader != null)
                    _reader.Dispose();
                if (_backReader != null)
                    _backReader.Dispose();
                if (_writer != null)
                    _writer.Dispose();
            }
        }
    }

    internal sealed class StreamContentBackReader : StreamReader
    {
        internal StreamContentBackReader(Stream stream, Encoding encoding)
            : base(stream, encoding)
        {
            _stream = stream;
            if (_stream.Length > 0)
            {
                long curPosition = _stream.Position;
                _stream.Seek(0, SeekOrigin.Begin);
                base.Peek();
                _stream.Position = curPosition;
                _currentEncoding = base.CurrentEncoding;
                _currentPosition = _stream.Position;
                
                // Get the oem encoding and system current ANSI code page
                _oemEncoding = EncodingConversion.Convert(null, EncodingConversion.OEM);
                _defaultAnsiEncoding = EncodingConversion.Convert(null, EncodingConversion.Default);
            }
        }

        private readonly Stream _stream;
        private readonly Encoding _currentEncoding;
        private readonly Encoding _oemEncoding;
        private readonly Encoding _defaultAnsiEncoding;

        private const int BuffSize = 4096;
        private readonly byte[] _byteBuff = new byte[BuffSize];
        private readonly char[] _charBuff = new char[BuffSize];
        private int _byteCount = 0;
        private int _charCount = 0;
        private long _currentPosition = 0;
        private bool? _singleByteCharSet = null;

        private const byte BothTopBitsSet = 0xC0;
        private const byte TopBitUnset = 0x80;

        /// <summary>
        /// If the given encoding is OEM or Default, check to see if the code page
        /// is SBCS(single byte character set).
        /// </summary>
        /// <returns></returns>
        private bool IsSingleByteCharacterSet()
        {
            if (_singleByteCharSet != null)
                return (bool)_singleByteCharSet;

            // Porting note: only UTF-8 is supported on Linux, which is not an SBCS
            if ((_currentEncoding.Equals(_oemEncoding) ||
                 _currentEncoding.Equals(_defaultAnsiEncoding))
                && Platform.IsWindows)
            {
                NativeMethods.CPINFO cpInfo;
                if (NativeMethods.GetCPInfo((uint)_currentEncoding.CodePage, out cpInfo) &&
                    cpInfo.MaxCharSize == 1)
                {
                    _singleByteCharSet = true;
                    return true;
                }
            }

            _singleByteCharSet = false;
            return false;
        }

        /// <summary>
        /// We don't support this method because it is not used by the ReadBackward method in StreamContentReaderWriter.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="index"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int ReadBlock(char[] buffer, int index, int count)
        {
            // This method is not supposed to be used
            throw TraceSource.NewNotSupportedException();
        }

        /// <summary>
        /// We don't support this method because it is not used by the ReadBackward method in StreamContentReaderWriter.
        /// </summary>
        /// <returns></returns>
        public override string ReadToEnd()
        {
            // This method is not supposed to be used
            throw TraceSource.NewNotSupportedException();
        }

        /// <summary>
        /// Reset the internal character buffer. Use it only when the position of the internal buffer and
        /// the base stream do not match. These positions can become mismatch when the user read the data
        /// into the buffer and then seek a new position in the underlying stream.
        /// </summary>
        internal new void DiscardBufferedData()
        {
            base.DiscardBufferedData();
            _currentPosition = _stream.Position;
            _charCount = 0;
            _byteCount = 0;
        }

        /// <summary>
        /// Return the current actual stream position.
        /// </summary>
        /// <returns></returns>
        internal long GetCurrentPosition()
        {
            if (_charCount == 0)
                return _currentPosition;

            // _charCount > 0
            int byteCount = _currentEncoding.GetByteCount(_charBuff, 0, _charCount);
            return (_currentPosition + byteCount);
        }

        /// <summary>
        /// Get the number of bytes the delimiter will
        /// be encoded to.
        /// </summary>
        /// <param name="delimiter"></param>
        /// <returns></returns>
        internal int GetByteCount(string delimiter)
        {
            char[] chars = delimiter.ToCharArray();
            return _currentEncoding.GetByteCount(chars, 0, chars.Length);
        }

        /// <summary>
        /// Peek the next character.
        /// </summary>
        /// <returns>Return -1 if we reach the head of the stream.</returns>
        public override int Peek()
        {
            if (_charCount == 0)
            {
                if (RefillCharBuffer() == -1)
                {
                    return -1;
                }
            }

            // Return the next available character, but DONT consume it (don't advance the _charCount)
            return (int)_charBuff[_charCount - 1];
        }

        /// <summary>
        /// Read the next character.
        /// </summary>
        /// <returns>Return -1 if we reach the head of the stream.</returns>
        public override int Read()
        {
            if (_charCount == 0)
            {
                if (RefillCharBuffer() == -1)
                {
                    return -1;
                }
            }

            _charCount--;
            return _charBuff[_charCount];
        }

        /// <summary>
        /// Read a specific maximum of characters from the current stream into a buffer.
        /// </summary>
        /// <param name="buffer">Output buffer.</param>
        /// <param name="index">Start position to write with.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <returns>Return the number of characters read, or -1 if we reach the head of the stream.</returns>
        /// <returns>Return the number of characters read, or -1 if we reach the head of the stream.</returns>
        public override int Read(char[] buffer, int index, int count)
        {
            return ReadSpan(new Span<char>(buffer, index, count));
        }

        /// <summary>
        /// Read characters from the current stream into a Span buffer.
        /// </summary>
        /// <param name="buffer">Output buffer.</param>
        /// <returns>Return the number of characters read, or -1 if we reach the head of the stream.</returns>
        public override int Read(Span<char> buffer)
        {
            return ReadSpan(buffer);
        }

        private int ReadSpan(Span<char> buffer)
        {
            // deal with the argument validation
            int charRead = 0;
            int index = 0;
            int count = buffer.Length;

            do
            {
                if (_charCount == 0)
                {
                    if (RefillCharBuffer() == -1)
                    {
                        return charRead;
                    }
                }

                int toRead = _charCount > count ? count : _charCount;

                for (; toRead > 0; toRead--, count--, charRead++)
                {
                    buffer[index++] = _charBuff[--_charCount];
                }
            }
            while (count > 0);

            return charRead;
        }

        /// <summary>
        /// Read a line from the current stream.
        /// </summary>
        /// <returns>Return null if we reach the head of the stream.</returns>
        public override string ReadLine()
        {
            if (_charCount == 0 && RefillCharBuffer() == -1)
            {
                return null;
            }

            int charsToRemove = 0;
            StringBuilder line = new StringBuilder();

            if (_charBuff[_charCount - 1] == '\r' ||
                _charBuff[_charCount - 1] == '\n')
            {
                charsToRemove++;
                line.Insert(0, _charBuff[--_charCount]);

                if (_charBuff[_charCount] == '\n')
                {
                    if (_charCount == 0 && RefillCharBuffer() == -1)
                    {
                        return string.Empty;
                    }

                    if (_charCount > 0 && _charBuff[_charCount - 1] == '\r')
                    {
                        charsToRemove++;
                        line.Insert(0, _charBuff[--_charCount]);
                    }
                }
            }

            do
            {
                while (_charCount > 0)
                {
                    if (_charBuff[_charCount - 1] == '\r' ||
                        _charBuff[_charCount - 1] == '\n')
                    {
                        line.Remove(line.Length - charsToRemove, charsToRemove);
                        return line.ToString();
                    }
                    else
                    {
                        line.Insert(0, _charBuff[--_charCount]);
                    }
                }

                if (RefillCharBuffer() == -1)
                {
                    line.Remove(line.Length - charsToRemove, charsToRemove);
                    return line.ToString();
                }
            } while (true);
        }

        /// <summary>
        /// Refill the internal character buffer.
        /// </summary>
        /// <returns></returns>
        private int RefillCharBuffer()
        {
            if ((RefillByteBuff()) == -1)
            {
                return -1;
            }

            _charCount = _currentEncoding.GetChars(_byteBuff, 0, _byteCount, _charBuff, 0);
            return _charCount;
        }

        /// <summary>
        /// Refill the internal byte buffer.
        /// </summary>
        /// <returns></returns>
        private int RefillByteBuff()
        {
            long lengthLeft = _stream.Position;

            if (lengthLeft == 0)
            {
                return -1;
            }

            int toRead = lengthLeft > BuffSize ? BuffSize : (int)lengthLeft;
            _stream.Seek(-toRead, SeekOrigin.Current);

            if (_currentEncoding.Equals(Encoding.UTF8))
            {
                // It's UTF-8, we need to detect the starting byte of a character
                do
                {
                    _currentPosition = _stream.Position;
                    byte curByte = (byte)_stream.ReadByte();
                    if ((curByte & BothTopBitsSet) == BothTopBitsSet ||
                        (curByte & TopBitUnset) == 0x00)
                    {
                        _byteBuff[0] = curByte;
                        _byteCount = 1;
                        break;
                    }
                } while (lengthLeft > _stream.Position);

                if (lengthLeft == _stream.Position)
                {
                    // Cannot find a starting byte. The stream is NOT UTF-8 format. Read 'toRead' number of bytes
                    _stream.Seek(-toRead, SeekOrigin.Current);
                    _byteCount = 0;
                }

                _byteCount += _stream.Read(_byteBuff, _byteCount, (int)(lengthLeft - _stream.Position));
                _stream.Position = _currentPosition;
            }
            else if (_currentEncoding.Equals(Encoding.Unicode) ||
                _currentEncoding.Equals(Encoding.BigEndianUnicode) ||
                _currentEncoding.Equals(Encoding.UTF32) ||
                _currentEncoding.Equals(Encoding.ASCII) ||
                IsSingleByteCharacterSet())
            {
                // Unicode -- two bytes per character
                // BigEndianUnicode -- two types per character
                // UTF-32 -- four bytes per character
                // ASCII -- one byte per character
                // The BufferSize will be a multiple of 4, so we can just read toRead number of bytes
                // if the current stream is encoded by any of these formatting

                // If IsSingleByteCharacterSet() returns true, we are sure that the given encoding is OEM
                // or Default, and it is SBCS(single byte character set) code page -- one byte per character
                _currentPosition = _stream.Position;
                _byteCount = _stream.Read(_byteBuff, 0, toRead);
                _stream.Position = _currentPosition;
            }
            else
            {
                // OEM and ANSI code pages include multibyte CJK code pages. If the current code page
                // is MBCS(multibyte character set), we cannot detect a starting byte.
                // UTF-7 has some characters encoded into UTF-16 and then in Modified Base64,
                // the start of these characters is indicated by a '+' sign, and the end is
                // indicated by a character that is not in Modified Base64 set.
                // For these encodings, we cannot detect a starting byte with confidence when
                // reading bytes backward. Throw out exception in these cases.
                string errMsg = String.Format(
                    Exceptions.ReadBackward_Encoding_NotSupport,
                    _currentEncoding.EncodingName);
                throw new BackReaderEncodingNotSupportedException(errMsg, _currentEncoding.EncodingName);
            }

            return _byteCount;
        }
        private static class NativeMethods
        {
            // Default values
            private const int MAX_DEFAULTCHAR = 2;
            private const int MAX_LEADBYTES = 12;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct CPINFO
            {
                [MarshalAs(UnmanagedType.U4)]
                internal int MaxCharSize;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DEFAULTCHAR)]
                public byte[] DefaultChar;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_LEADBYTES)]
                public byte[] LeadBytes;
            };

            /// <summary>
            /// Get information on a named code page.
            /// </summary>
            /// <param name="codePage"></param>
            /// <param name="lpCpInfo"></param>
            /// <returns></returns>
            [DllImport(PinvokeDllNames.GetCPInfoDllName, CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetCPInfo(uint codePage, out CPINFO lpCpInfo);
        }

        /// <summary>
        /// The exception that indicates the encoding is not supported when reading backward.
        /// </summary>
        internal sealed class BackReaderEncodingNotSupportedException : NotSupportedException
        {
            internal BackReaderEncodingNotSupportedException(string message, string encodingName)
                : base(message)
            {
                EncodingName = encodingName;
            }

            internal BackReaderEncodingNotSupportedException(string encodingName)
            {
                EncodingName = encodingName;
            }

            /// <summary>
            /// Get the encoding name.
            /// </summary>
            internal string EncodingName { get; }
        }
    }

}

