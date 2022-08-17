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
    if ($Format -eq "Tgz") {
        # Get a temp file
        $gzipFolderPath = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
        New-Item -Path $gzipFolderPath -ItemType Directory
        "7z e $ActualValue -o${gzipFolderPath} -tgzip" | Invoke-Expression
        $tarFilePath = (Get-ChildItem $gzipFolderPath)[0].FullName
        return Should-BeTarArchiveOnlyContaining -ActualValue $tarFilePath -ExpectedValue $ExpectedValue -Negate:$Negate -Because $Because -LiteralPath:$LiteralPath -CallerSessionState $CallerSessionState
    } 
    return [pscustomobject]@{
        Succeeded      = $false
        FailureMessage = "Format ${Format} is not supported."
    }

}
Add-ShouldOperator -Name BeArchiveOnlyContaining -InternalName 'Should-BeArchiveOnlyContaining' -Test ${function:Should-BeArchiveOnlyContaining}