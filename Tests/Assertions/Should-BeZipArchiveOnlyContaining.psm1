function Should-BeZipArchiveOnlyContaining {
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
        $CallerSessionState
    )

    # ActualValue is supposed to be a path to an archive
    # It could be a path to a custom PSDrive, so it needes to be converted
    if ($LiteralPath) {
        $ActualValue = Convert-Path -LiteralPath $ActualValue
    } else {
        $ActualValue = Convert-Path -Path $ActualValue
    }


    # Ensure ActualValue is a valid path
    if ($LiteralPath) {
        $testPathResult = Test-Path -LiteralPath $ActualValue
    } else {
        $testPathResult = Test-Path -Path $ActualValue
    }

    # Don't continue processing if ActualValue is not an actual path
    # Determine if the assertion succeeded or failed and then return    
    if (-not $testPathResult) {
        $succeeded = $Negate
        if (-not $succeeded) {
            $failureMessage = "The path ${ActualValue} does not exist"
        }
        return [pscustomobject]@{
            Succeeded      = $succeeded
            FailureMessage = $failureMessage
        }
    }

    # Get 7-zip to list the contents of the archive
    $output = 7z.exe l $ActualValue -ba

    # Check if the output is null
    if ($null -eq $output) {
        if ($null -eq $ExpectedValue -or $ExpectedValue.Length -eq 0) {
            $succeeded = -not $Negate
        } else {
            $succeeded = $Negate
        }

        if (-not $succeeded) {
            $failureMessage = "Archive {0} contains nothing, but it was expected to contain something"
        }

        return [pscustomobject]@{
            Succeeded      = $succeeded
            FailureMessage = $failureMessage
        }
    }

    # Filter the output line by line
    $lines = $output -split [System.Environment]::NewLine

    # Stores the entry names
    $entryNames = @()

    # Go through each line and split it by whitespace
    foreach ($line in $lines) {
        $lineComponents = $line -split " +"
        
        # Example of some lines:
        #2022-08-05 15:54:04 D....            0            0  SourceDir
        #2022-08-05 15:54:04 .....           11           11  SourceDir/Sample-1.txt
        
        # First component is date
        # 2nd component is time
        # 3rd componnent is attributes
        # 4th component is size
        # 5th component is compressed size
        # 6th component is entry name

        $entryName = $lineComponents[$lineComponents.Length - 1]

        # Since 7zip does not show trailing forwardslash for directories, we need to check the attributes to see if it starts with 'D'
        # If so, it means the entry is a directory and we should append a forwardslash to the entry name

        if ($lineComponents[2].StartsWith('D')) {
            $entryName += '/'
        }

        # Replace backslashes to forwardslashes
        $dirSeperatorChar = [System.IO.Path]::DirectorySeparatorChar
        $entryName = $entryName.Replace($dirSeperatorChar, "/")

        $entryNames += $entryName
    }

    $itemsNotInArchive = @()

    # Go through each item in ExpectedValue and ensure it is in entryNames
    foreach ($expectedItem in $ExpectedValue) {
        if ($entryNames -notcontains $expectedItem) {
            $itemsNotInArchive += $expectedItem
        }
    }

    if ($itemsNotInArchive.Length -gt 0 -and -not $Negate) {
        # Create a comma-seperated string from $itemsNotInEnryName
        $commaSeperatedItemsNotInArchive = $itemsNotInArchive -join ","
        $failureMessage = "'$ActualValue' does not contain $commaSeperatedItemsNotInArchive $(if($Because) { "because $Because"})."
        $succeeded = $false
    }

    # Ensure the length of $entryNames is equal to that of $ExpectedValue
    if ($null -eq $succeeded -and $entryNames.Length -ne $ExpectedValue.Length -and -not $Negate) {
        $failureMessage = "${ActualValue} does not contain the same number of items as ${ExpectedValue -join ""} (expected ${ExpectedValue.Length} entries but found ${entryNames.Length}) $(if($Because) { "because $Because"})."
        $succeeded = $false
    }

    if ($null -eq $succeeded) {
        $succeeded = -not $Negate
        if (-not $succeeded) {
            $failureMessage = "Expected ${ActualValue} to not contain the entries ${ExpectedValue -join ""} only $(if($Because) { "because $Because"})."
        }
    }

    $ObjProperties = @{
        Succeeded      = $succeeded
        FailureMessage = $failureMessage
    }
    return New-Object PSObject -Property $ObjProperties
}

Add-ShouldOperator -Name BeZipArchiveOnlyContaining -InternalName 'Should-BeZipArchiveOnlyContaining' -Test ${function:Should-BeZipArchiveOnlyContaining}