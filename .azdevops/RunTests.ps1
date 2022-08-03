# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Load the module
$module = Get-Module -Name "Microsoft.PowerShell.Archive"
if ($null -ne $module)
{
    Remove-Module $module
}

# Import the built module
Import-Module "$env:PIPELINE_WORKSPACE/ModuleBuild/Microsoft.PowerShell.Archive.psd1"

$pesterRequiredVersion = "5.3"

# If Pester 5.3.3 is not installed, install it
$shouldInstallPester = $true

if ($pesterModules = Get-Module -Name "Pester" -ListAvailable) {
    foreach ($module in $pesterModules) {
        if ($module.Version.ToString() -eq $pesterRequiredVersion) {
            $shouldInstallPester = $false
            break
        }
    }
}

if ($shouldInstallPester) {
    Install-Module -Name "Pester" -RequiredVersion $pesterRequiredVersion -Force
}

# Load Pester
Import-Module -Name "Pester" -RequiredVersion $pesterRequiredVersion

# Run tests
$OutputFile = "$PWD/build-unit-tests.xml"
$results = $null
$results = Invoke-Pester -Script ./Tests/Compress-Archive.Tests.ps1 -OutputFile $OutputFile -PassThru -OutputFormat NUnitXml -Show Failed, Context, Describe, Fails
Write-Host "##vso[artifact.upload containerfolder=testResults;artifactname=testResults]$OutputFile"
if(!$results -or $results.FailedCount -gt 0 -or !$results.TotalCount)
{
    throw "Build or tests failed.  Passed: $($results.PassedCount) Failed: $($results.FailedCount) Total: $($results.TotalCount)"
}