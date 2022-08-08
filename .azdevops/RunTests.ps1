# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Import the built module
Import-Module "$env:PIPELINE_WORKSPACE/ModuleBuild/Microsoft.PowerShell.Archive.psd1"

Get-ChildItem "$env:PIPELINE_WORKSPACE/ModuleBuild" | Write-Verbose -Verbose

$pesterMinVersion = "5.3.0"
$pesterMaxVersion = "5.3.9"

# If Pester 5.3.x is not installed, install it
$pesterModule = Get-InstalledModule -Name "Pester" -MinimumVersion $pesterMinVersion -MaximumVersion $pesterMaxVersion
if ($null -eq $pesterModule) {
    Install-Module -Name "Pester" -MinimumVersion $pesterMinVersion -MaximumVersion $pesterMaxVersion -Force
}

# Load Pester
Import-Module -Name "Pester" -MinimumVersion $pesterMinVersion -MaximumVersion $pesterMaxVersion

# Run tests
$OutputFile = "$PWD/build-unit-tests.xml"
$results = $null
$results = Invoke-Pester -Script ./Tests/Compress-Archive.Tests.ps1 -OutputFile $OutputFile -PassThru -OutputFormat NUnitXml -Show Failed, Context, Describe, Fails
Write-Host "##vso[artifact.upload containerfolder=testResults;artifactname=testResults]$OutputFile"
if(!$results -or $results.FailedCount -gt 0 -or !$results.TotalCount)
{
    throw "Build or tests failed.  Passed: $($results.PassedCount) Failed: $($results.FailedCount) Total: $($results.TotalCount)"
}