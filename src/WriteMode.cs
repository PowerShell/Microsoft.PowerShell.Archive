// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerShell.Archive
{
    public enum WriteMode
    {
        Create,
        Update,
        Overwrite
    }

    public enum ExpandArchiveWriteMode
    {
        Expand,
        Overwrite
    }
}
