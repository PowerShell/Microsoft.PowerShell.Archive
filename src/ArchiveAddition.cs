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
        public string EntryName { get; set; }

        /// <summary>
        /// The fully qualified path of the file or directory to add to or update in the archive.
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// The type of filesystem entry to add.
        /// </summary>
        public ArchiveAdditionType Type { get; set; }

        public ArchiveAddition(string entryName, string fullPath, ArchiveAdditionType type)
        {
            EntryName = entryName;
            FullPath = fullPath;
            Type = type;
        }

        /// <summary>
        /// This enum tracks types of filesystem entries
        /// </summary>
        internal enum ArchiveAdditionType
        {
            File,
            Directory,
        }
    }
}
