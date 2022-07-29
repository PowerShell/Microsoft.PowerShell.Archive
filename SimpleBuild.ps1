$buildOutputDirectory = "$PSScriptRoot\src\bin\Release"

if ((Test-Path $buildOutputDirectory)) {
    Remove-Item -Path $buildOutputDirectory -Recurse -Force
}

# Perform dotnet build
dotnet build "$PSScriptRoot\src\Microsoft.PowerShell.Archive.csproj" -c Release

"Build module location:   $buildOutputDirectory" | Write-Verbose -Verbose

# Get module version
$ManifestData = Import-PowerShellDataFile -Path "$buildOutputDirectory\Microsoft.PowerShell.Archive.psd1"
$Version = $ManifestData.ModuleVersion

"Setting VSTS variable 'BuildOutDir' to '$buildOutputDirectory'" | Write-Verbose -Verbose
Write-Host "##vso[task.setvariable variable=BuildOutDir]$buildOutputDirectory"

"Setting VSTS variable 'PackageVersion' to '$Version'" | Write-Verbose -Verbose
Write-Host "##vso[task.setvariable variable=PackageVersion]$Version"
