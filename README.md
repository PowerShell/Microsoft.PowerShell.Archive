# Microsoft.PowerShell.Archive Module

> [!NOTE]
> This repo is not currently actively being developed although we will actively address any security issues

[Microsoft.PowerShell.Archive module](https://technet.microsoft.com/en-us/library/dn818910.aspx) contains cmdlets that let you create and extract ZIP archives.

| Azure CI  |
|:-------------------:|
|[![Build Status](https://dev.azure.com/powershell/Archive/_apis/build/status/PowerShell.Microsoft.PowerShell.Archive?repoName=PowerShell%2FMicrosoft.PowerShell.Archive&branchName=refs%2Fpull%2F131%2Fmerge)](https://dev.azure.com/powershell/Archive/_build/latest?definitionId=130&repoName=PowerShell%2FMicrosoft.PowerShell.Archive&branchName=refs%2Fpull%2F131%2Fmerge)|

## [Compress-Archive](https://technet.microsoft.com/library/dn841358.aspx) examples

1. Create an archive from an entire folder including subdirectories: `Compress-Archive -Path C:\Reference -DestinationPath C:\Archives\Draft.zip`
2. Update an existing archive file: `Compress-Archive -Path C:\Reference\* -DestinationPath C:\Archives\Draft.zip -Update`

## [Expand-Archive](https://technet.microsoft.com/library/dn841359.aspx) examples

1. Extract the contents of an archive in the current folder: `Expand-Archive -Path SampleArchive.zip`
2. Use -Force parameter to overwrite existing files by those in the archive: `Expand-Archive -Path .\SampleArchive.zip -DestinationPath .\ExistingDir -Force`

## Code of Conduct

Please see our [Code of Conduct](.github/CODE_OF_CONDUCT.md) before participating in this project.

## Security Policy

For any security issues, please see our [Security Policy](.github/SECURITY.md).
