@{
ModuleVersion = '2.0.1'
GUID = '06a335eb-dd10-4d25-b753-4f6a80163516'
Author = 'Microsoft'
CompanyName = 'Microsoft'
Copyright = '(c) Microsoft. All rights reserved.'
Description = 'PowerShell module for creating and expanding archives.'
PowerShellVersion = '7.2.5'
NestedModules = @('Microsoft.PowerShell.Archive.dll')
CmdletsToExport = @('Compress-Archive')
PrivateData = @{
    PSData = @{
        Tags = @('Archive', 'Zip', 'Compress')
        ProjectUri = 'https://github.com/PowerShell/Microsoft.PowerShell.Archive'
        ReleaseNotes = @'
        ## 2.0.1-preview1
        - Rewrote Compress-Archive cmdlet in C#
'@
        Prerelease = 'preview1'
    }
}