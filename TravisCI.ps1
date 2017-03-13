$testResultsFile = ".\ArchiveTestResults.xml"
Import-Module "C:\projects\Archive-Module\Microsoft.PowerShell.Archive" -Force
$testResults = Invoke-Pester -Script "C:\projects\Archive-Module\Tests" -OutputFormat NUnitXml -OutputFile $testResultsFile -PassThru
if ($testResults.FailedCount -gt 0) {
    throw "$($testResults.FailedCount) tests failed."
}