# Load the module
$module = Get-Module -Name "Microsoft.PowerShell.Archive"
if ($null -ne $module)
{
    Remove-Module $module
}

# Import the built module
Import-Module "$env:PIPELINE_WORKSPACE/ModuleBuild/Microsoft.PowerShell.Archive.psd1"

$module = Get-Module -Name "Microsoft.PowerShell.Archive"
$module.Path | Write-Verbose -Verbose

# Load Pester
Install-Module -Name "Pester" -RequiredVersion "5.3.3" -Force
$module = Get-Module -Name "Pester"
if ($null -ne $module)
{
    Remove-Module "Pester"
} 
Import-Module -Name "Pester" -RequiredVersion "5.3.3"

# Run tests
$OutputFile = "$PWD/build-unit-tests.xml"
$results = $null
$results = Invoke-Pester -Script ./Tests/Compress-Archive.Tests.ps1 -OutputFile $OutputFile -PassThru -OutputFormat NUnitXml -Show Failed, Context, Describe, Fails
Write-Host "##vso[artifact.upload containerfolder=testResults;artifactname=testResults]$OutputFile"
if(!$results -or $results.FailedCount -gt 0 -or !$results.TotalCount)
{
    throw "Build or tests failed.  Passed: $($results.PassedCount) Failed: $($results.FailedCount) Total: $($results.TotalCount)"
}

# Unload module
$module = Get-Module -Name "Microsoft.PowerShell.Archive"
if ($null -ne $module)
{
    Remove-Module $module
}
