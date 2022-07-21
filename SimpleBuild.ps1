$buildOutputDirectory = "$PSScriptRoot\src\build\net7.0"

if ((Test-Path $buildOutputDirectory)) {
    Remove-Item -Path $buildOutputDirectory -Recurse -Force
}

# Perform dotnet build
dotnet build .\src\Microsoft.PowerShell.Archive.csproj -c release

"Build module location:   $buildOutputDirectory" | Write-Verbose -Verbose

"Setting VSTS variable 'BuildOutDir' to '$buildOutputDirectory'" | Write-Verbose -Verbose
Write-Host "##vso[task.setvariable variable=BuildOutDir]$buildOutputDirectory"

$psd1ModuleVersion = (Get-Content -Path "$buildOutputDirectory\Microsoft.PowerShell.Archive.psd1" | Select-String 'ModuleVersion="(.*)"').Matches[0].Groups[1].Value
"Setting VSTS variable 'PackageVersion' to '$psd1ModuleVersion'" | Write-Verbose -Verbose
Write-Host "##vso[task.setvariable variable=PackageVersion]$psd1ModuleVersion"
