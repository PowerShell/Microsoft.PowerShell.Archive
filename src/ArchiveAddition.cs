// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerShell.Archive
{
    /// <summary>
    /// ArchiveAddition represents an filesystem entry that we want to add to or update in the archive.
    /// ArchiveAddition DOES NOT represent an entry in the archive -- rather, it represents an entry to be created or updated using the information contained in an instance of this class.
    /// </summary>
    internal class ArchiveAddition
    {
        /// <summary>
        /// The name of the file or directory in the archive.
        /// This is a path of the file or directory in the archive (e.g., 'file1.txt` means the file is a top-level file in the archive).
        /// </summary>
        internal string EntryName { get; set; }

        internal System.IO.FileSystemInfo FileSystemInfo { get; set; }

        internal ArchiveAddition(string entryName, System.IO.FileSystemInfo fileSystemInfo)
        {
            EntryName = entryName;
            FileSystemInfo = fileSystemInfo;
        }
    }
}
