// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerShell.Archive
{
    internal interface IArchive: IDisposable
    {
        // Get what mode the archive is in
        internal ArchiveMode Mode { get; }

        // Get the fully qualified path of the archive
        internal string Path { get; }

        // Add a file or folder to the archive. The entry name of the added item in the
        // will be ArchiveEntry.Name.
        // Throws an exception if the archive is in read mode.
        internal void AddFileSystemEntry(ArchiveAddition entry);

        internal IEntry? GetNextEntry();

        // Does the archive have only a top-level directory?
        internal bool HasTopLevelDirectory();
    }
}
