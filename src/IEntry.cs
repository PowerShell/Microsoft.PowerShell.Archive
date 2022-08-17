using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.Archive
{
    public interface IEntry
    {
        public string Name { get; }

        public bool IsDirectory { get; }

        public void ExpandTo(string destinationPath);
    }
}
