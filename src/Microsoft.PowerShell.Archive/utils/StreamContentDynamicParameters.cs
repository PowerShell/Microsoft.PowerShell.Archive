using System;

using System.Collections;
using System.Collections.Generic;

using System.Text;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Internal;
using Microsoft.PowerShell.Commands;

namespace Microsoft.PowerShell.Archive
{

    /// <summary>
    /// Defines the dynamic parameters used by both the content reader and writer.
    /// </summary>
    public class StreamContentDynamicParameterBase : FileSystemContentDynamicParametersBase
    {
        /// <summary>
        /// Gets or sets the encoding method used when
        /// reading data from the file.
        /// </summary>
        [Parameter]
        [ArgumentToEncodingTransformationAttribute()]
        [ArgumentEncodingCompletionsAttribute]
        [ValidateNotNullOrEmpty]
        public Encoding Encoding
        {
            get
            {
                return _encoding;
            }

            set
            {
                _encoding = value;
                // If an encoding was explicitly set, be sure to capture that.
                WasStreamTypeSpecified = true;
            }
        }

        //private Encoding _encoding = ClrFacade.GetDefaultEncoding();
        private Encoding _encoding = Encoding.Default;

        /// <summary>
        /// Return file contents as a byte stream or create file from a series of bytes.
        /// </summary>
        [Parameter]
        public SwitchParameter AsByteStream { get; set; }

#if !UNIX
        /// <summary>
        /// A parameter to return a stream of an item.
        /// </summary>
        [Parameter]
        public string Stream { get; set; }
#endif

        /// <summary>
        /// Gets the status of the StreamType parameter.  Returns true
        /// if the stream was opened with a user-specified encoding, false otherwise.
        /// </summary>
        public bool WasStreamTypeSpecified { get; private set; }

    }
    
    /// <summary>
    /// Defines the dynamic parameters used by the set-content and
    /// add-content cmdlets.
    /// </summary>

    public class StreamContentWriterDynamicParameters : StreamContentDynamicParameterBase
    {
        /// <summary>
        /// False to add a newline to the end of the output string, true if not.
        /// </summary>
        [Parameter]
        public SwitchParameter NoNewline
        {
            get
            {
                return _suppressNewline;
            }

            set
            {
                _suppressNewline = value;
            }
        }

        private bool _suppressNewline = false;
    }

    /// <summary>
    /// Defines the dynamic parameters used by the get-content cmdlet.
    /// </summary>
    public class StreamContentReaderDynamicParameters : StreamContentDynamicParameterBase
    {
        /// <summary>
        /// Gets or sets the delimiter to use when reading the file.  Custom delimiters
        /// may not be used when the file is opened with a "Byte" encoding.
        /// </summary>
        [Parameter]
        public string Delimiter
        {
            get
            {
                return _delimiter;
            }

            set
            {
                DelimiterSpecified = true;
                _delimiter = value;
            }
        }

        private string _delimiter = "\n";
        
        /// <summary>
        /// When the Raw switch is present, we don't do any breaks on newlines,
        /// and only emit one object to the pipeline: all of the content.
        /// </summary>
        [Parameter]
        public SwitchParameter Raw
        {
            get
            {
                return _isRaw;
            }

            set
            {
                _isRaw = value;
            }
        }

        private bool _isRaw;

        /// <summary>
        /// Gets the status of the delimiter parameter.  Returns true
        /// if the delimiter was explicitly specified by the user, false otherwise.
        /// </summary>
        public bool DelimiterSpecified
        {
            get; private set;
            // get
        }

        /// <summary>
        /// The number of content items to retrieve from the back of the file.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        // [Alias("Last")]
        public int Trail
        {
            set
            {
                _backCount = value;
                _tailSpecified = true;
            }

            get { return _backCount; }
        }

        private int _backCount = -1;
        private bool _tailSpecified = false;

    }

        /// <summary>
    /// Defines the dynamic parameters used by the Clear-Content cmdlet.
    /// </summary>
    public class StreamContentClearContentDynamicParameters
    {

    }
}