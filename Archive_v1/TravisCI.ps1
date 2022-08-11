$testResultsFile = "./ArchiveTestResults.xml"
Import-Module "./Microsoft.PowerShell.Archive/Microsoft.PowerShell.Archive.psd1" -Force
$testResults = Invoke-Pester -Script "./Tests" -OutputFormat NUnitXml -OutputFile $testResultsFile -PassThru
if ($testResults.FailedCount -gt 0) {
    throw "$($testResults.FailedCount) tests failed."
}
