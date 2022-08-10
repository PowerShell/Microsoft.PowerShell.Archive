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
    public class ArchiveAddition
    {
        /// <summary>
        /// The name of the file or directory in the archive.
        /// This is a path of the file or directory in the archive (e.g., 'file1.txt` means the file is a top-level file in the archive).
        ///
        /// Does EntryName == FileSystemInfo.Name? This is not always true because EntryName can contain ancestor directories due to path directory structure preservation or due to the user
        /// archiving parent directories.
        /// For example, supoose we have the following directory
        ///     grandparent
        ///     |---parent
        ///         |---file.txt
        /// If we want to add or update grandparent to/in the archive, grandparent would be recursed for its descendents. This means the EntryName of file.txt would become
        /// `grandparent/parent/file.txt` so that when expanding the archive, file.txt is put in the correct location (directly under parent and under grandparent).
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
