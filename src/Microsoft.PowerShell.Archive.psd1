@{
ModuleVersion = '2.0.1'
GUID = '06a335eb-dd10-4d25-b753-4f6a80163516'
Author = 'Microsoft'
CompanyName = 'Microsoft'
Copyright = '(c) Microsoft. All rights reserved.'
Description = 'PowerShell module for creating and expanding archives.'
PowerShellVersion = '7.2.5'
NestedModules = @('Microsoft.PowerShell.Archive.dll')
CmdletsToExport = @('Compress-Archive', 'Expand-Archive')
PrivateData = @{
    PSData = @{
        Tags = @('Archive', 'Zip', 'Compress')
        ProjectUri = 'https://github.com/PowerShell/Microsoft.PowerShell.Archive'
        LicenseUri = 'https://go.microsoft.com/fwlink/?linkid=2203619'
        ReleaseNotes = @'
        ## 2.0.1-preview2
        - Rewrite `Expand-Archive` cmdlet in C#
        - Added `-Format` parameter to `Expand-Archive`
        - Added `-WriteMode` parameter to `Expand-Archive`
        - Added support for zip64
        - Fixed a bug where the entry names of files in a directory would not be correct when compressing an archive

        ## 2.0.1-preview1
        - Rewrite `Compress-Archive` cmdlet in C#
        - Added `-Format` parameter to `Compress-Archive`
        - Added `-WriteMode` parameter to `Compress-Archive`
        - Added support for relative path structure preservating when paths relative to the working directory are specified to `-Path` or `-LiteralPath` in `Compress-Archive`
        - Added support for zip64
        - Fixed a bug where empty directories would not be compressed
        - Fixed a bug where an abrupt stop when compressing empty directories would not delete the newly created archive
'@
        Prerelease = 'preview1'
    }
}