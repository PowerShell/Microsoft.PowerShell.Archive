function Should-BeArchiveOnlyContaining {
    <#
    .SYNOPSIS
        Checks if a zip archive contains the entries $ExpectedValue
    .EXAMPLE
        "C:\Users\<user>\archive.zip" | Should -BeZipArchiveContaining @("file1.txt")

        Checks if archive.zip only contains file1.txt
    #>

    [CmdletBinding()]
    Param (
        [string] $ActualValue,
        [string[]] $ExpectedValue,
        [switch] $Negate,
        [string] $Because,
        [switch] $LiteralPath,
        $CallerSessionState,
        [string] $Format
    )

    if ($Format -eq "Zip") {
        return Should-BeZipArchiveOnlyContaining -ActualValue $ActualValue -ExpectedValue $ExpectedValue -Negate:$Negate -Because $Because -LiteralPath:$LiteralPath -CallerSessionState $CallerSessionState
    }
    if ($Format -eq "Tar") {
        return Should-BeTarArchiveOnlyContaining -ActualValue $ActualValue -ExpectedValue $ExpectedValue -Negate:$Negate -Because $Because -LiteralPath:$LiteralPath -CallerSessionState $CallerSessionState
    } 
    return return [pscustomobject]@{
        Succeeded      = $false
        FailureMessage = "Format ${Format} is not supported."
    }

}
Add-ShouldOperator -Name BeArchiveOnlyContaining -InternalName 'Should-BeArchiveOnlyContaining' -Test ${function:Should-BeArchiveOnlyContaining}