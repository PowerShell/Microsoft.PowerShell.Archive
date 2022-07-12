using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerShell.Archive
{
    internal class ArchiveEntry
    {
        public string Name { get; set; }

        public string FullPath { get; set; }

        public ArchiveEntry(string name, string fullPath)
        {
            Name = name;
            FullPath = fullPath;
        }
    }
}
