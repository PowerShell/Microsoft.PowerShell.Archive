# Microsoft.PowerShell.Archive Module
[Microsoft.PowerShell.Archive module](https://technet.microsoft.com/en-us/library/dn818910.aspx) contains cmdlets that let you create and extract ZIP archives.

|AppVeyor (Windows)   | Travis CI (Linux)  |
|:-------------------:|:------------------:|
|[![Build status](https://ci.appveyor.com/api/projects/status/npvhboe2nbdbtteg/branch/master?svg=true)](https://ci.appveyor.com/project/PowerShell/microsoft-powershell-archive/branch/master)|[![Build Status](https://travis-ci.org/PowerShell/Microsoft.PowerShell.Archive.svg?branch=master)](https://travis-ci.org/PowerShell/Microsoft.PowerShell.Archive)|

## [Compress-Archive](https://technet.microsoft.com/library/dn841358.aspx) examples
1. Create an archive from an entire folder including subdirectories: `Compress-Archive -Path C:\Reference -DestinationPath C:\Archives\Draft.zip`
2. Update an existing archive file: `Compress-Archive -Path C:\Reference\* -DestinationPath C:\Archives\Draft.zip -Update`

## [Expand-Archive](https://technet.microsoft.com/library/dn841359.aspx) examples
1. Extract the contents of an archive in the current folder: `Expand-Archive -Path SampleArchive.zip`
2. Use -Force parameter to overwrite existing files by those in the archive: `Expand-Archive -Path .\SampleArchive.zip -DestinationPath .\ExistingDir -Force`
