if ((Test-Path "$PSScriptRoot\out")) {
    Remove-Item -Path $PSScriptRoot\out -Recurse -Force
}

New-Item -ItemType directory -Path $PSScriptRoot\out | Out-Null
New-Item -ItemType directory -Path $PSScriptRoot\out\Microsoft.PowerShell.Archive | Out-Null

$OutPath = Join-Path $PSScriptRoot "out"
$OutModulePath = Join-Path $OutPath "Microsoft.PowerShell.Archive"

Copy-Item -Recurse -Path "$PSScriptRoot\Microsoft.PowerShell.Archive" -Destination $OutPath -Force

"Build module location:   $OutModulePath" | Write-Verbose -Verbose

"Setting VSTS variable 'BuildOutDir' to '$OutModulePath'" | Write-Verbose -Verbose
Write-Host "##vso[task.setvariable variable=BuildOutDir]$OutModulePath"

$psd1ModuleVersion = (Get-Content -Path "$OutModulePath\Microsoft.PowerShell.Archive.psd1" | Select-String 'ModuleVersion="(.*)"').Matches[0].Groups[1].Value
"Setting VSTS variable 'PackageVersion' to '$psd1ModuleVersion'" | Write-Verbose -Verbose
Write-Host "##vso[task.setvariable variable=PackageVersion]$psd1ModuleVersion"
