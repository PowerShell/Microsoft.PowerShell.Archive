function Should-BeZipArchiveWithUnixPermissions {
    <#
    .SYNOPSIS
        Checks if a zip archive contains entries with the expected Unix permissions
    .EXAMPLE
        "C:\Users\<user>\archive.zip" | Should -BeZipArchiveWithUnixPermissions "d---------" "-rw-------"

        Checks if archive.zip only contains file1.txt
    #>

    [CmdletBinding()]
    Param (
        [string] $ActualValue,
        [string] $TempDirectory,
        [string] $ExpectedDirectoryPermissions,
        [string] $ExpectedFilePermissions,
        [switch] $Negate,
        [string] $Because,
        [switch] $LiteralPath,
        $CallerSessionState
    )

    # We need to ensure that ls won't run Get-ChildItem instead
    $previousAlias = Get-Alias ls -ErrorAction SilentlyContinue
    if ($previousAlias -ne $null) {
        Remove-Alias ls
    }

    try {
        # ActualValue is supposed to be a path to an archive
        # It could be a path to a custom PSDrive, so it needes to be converted
        if ($LiteralPath) {
            $ActualValue = Convert-Path -LiteralPath $ActualValue
        }
        else {
            $ActualValue = Convert-Path -Path $ActualValue
        }


        # Ensure ActualValue is a valid path
        if ($LiteralPath) {
            $testPathResult = Test-Path -LiteralPath $ActualValue
        }
        else {
            $testPathResult = Test-Path -Path $ActualValue
        }

        # Don't continue processing if ActualValue is not an actual path
        if (-not $testPathResult) {
            return [pscustomobject]@{
                Succeeded      = $false
                FailureMessage = $failureMessage
            }
        }

        $unzipPath = "$TempDirectory/unzipped"

        unzip $ActualValue -d $unzipPath

        # Get ls to list the unzipped contents of the archive with permissions
        chmod 775 $unzipPath
        $output = ls -Rl $unzipPath

        # Check if the output is null
        if ($null -eq $output) {
            return [pscustomobject]@{
                Succeeded      = $false
                FailureMessage = "Archive {0} contains nothing, but it was expected to contain something"
            }
        }

        # Filter the output line by line
        $lines = $output -split [System.Environment]::NewLine

        # Go through each line and split it by whitespace
        foreach ($line in $lines) {
            
            #Skip non-file/directory lines from recursive output
            #eg. directory path and total blocks count
            #./src/obj/Release/ref:
            #total 12
            if (-not $line.StartsWith("-") -and -not $line.StartsWith("d")) {
                continue;
            }
            Write-Host $line
            $lineComponents = $line -split " +"

            # Example of some lines:
            #-rw-r--r-- 1 owner group 26112 Mar 22 00:36 Microsoft.PowerShell.Archive.dll
            #drwxr-xr-x 2 owner group  4096 Mar 22 00:19 ref
        
            # First component contains attributes
            # 2nd component is link count
            # 3rd componnent is owner
            # 4th component is group
            # 5th component is file size
            # 6th component is last modified month
            # 7th component is last modified day
            # 8th component is last modified time
            # 9th component is file name

            $permissionString = $lineComponents[0];


            if ($permissionString[0] -eq 'd') {
                if ($permissionString -ne $expectedDirectoryPermissions) {
                    return [pscustomobject]@{
                        Succeeded      = $false
                        FailureMessage = "Expected directory permissions '$expectedDirectoryPermissions' but got '$permissionString'"
                    }
                }
            }
            else {
                if ($permissionString -ne $expectedFilePermissions) {
                    return [pscustomobject]@{
                        Succeeded      = $false
                        FailureMessage = "Expected directory permissions '$expectedFilePermissions' but got '$permissionString'"
                    }
                }
            }
        }


        $ObjProperties = @{
            Succeeded = $true
        }
        return New-Object PSObject -Property $ObjProperties
    }
    finally {
        if($previousAlias -ne $null) {
            Set-Alias ls $previousAlias.Definition
        }
    }
}

Add-ShouldOperator -Name BeZipArchiveWithUnixPermissions -InternalName 'Should-BeZipArchiveWithUnixPermissions' -Test ${function:Should-BeZipArchiveWithUnixPermissions}