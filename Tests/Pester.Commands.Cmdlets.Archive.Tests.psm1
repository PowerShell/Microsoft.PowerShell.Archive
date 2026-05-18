# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$DS = [System.IO.Path]::DirectorySeparatorChar
function Add-CompressionAssemblies {
    Add-Type -AssemblyName System.IO.Compression
    if ($psedition -eq "Core")
    {
        Add-Type -AssemblyName System.IO.Compression.ZipFile
    }
    else
    {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
    }
}

function CompressArchivePathParameterSetValidator {
    param
    (
        [string[]] $path,
        [string] $destinationPath,
        [string] $compressionLevel = "Optimal"
    )

    try
    {
        Compress-Archive -Path $path -DestinationPath $destinationPath -CompressionLevel $compressionLevel
        throw "ValidateNotNullOrEmpty attribute is missing on one of parameters belonging to Path parameterset."
    }
    catch
    {
        $_.FullyQualifiedErrorId | Should Be "ParameterArgumentValidationError,Compress-Archive"
    }
}

function CompressArchiveLiteralPathParameterSetValidator {
    param
    (
        [string[]] $literalPath,
        [string] $destinationPath,
        [string] $compressionLevel = "Optimal"
    )

    try
    {
        Compress-Archive -LiteralPath $literalPath -DestinationPath $destinationPath -CompressionLevel $compressionLevel
        throw "ValidateNotNullOrEmpty attribute is missing on one of parameters belonging to LiteralPath parameterset."
    }
    catch
    {
        $_.FullyQualifiedErrorId | Should Be "ParameterArgumentValidationError,Compress-Archive"
    }
}


function CompressArchiveInValidPathValidator {
    param
    (
        [string[]] $path,
        [string] $destinationPath,
        [string] $invalidPath,
        [string] $expectedFullyQualifiedErrorId
    )

    try
    {
        Compress-Archive -Path $path -DestinationPath $destinationPath
        throw "Failed to validate that an invalid Path $invalidPath was supplied as input to Compress-Archive cmdlet."
    }
    catch
    {
        $_.FullyQualifiedErrorId | Should Be $expectedFullyQualifiedErrorId
    }
}

function CompressArchiveInValidArchiveFileExtensionValidator {
    param
    (
        [string[]] $path,
        [string] $destinationPath,
        [string] $invalidArchiveFileExtension
    )

    try
    {
        Compress-Archive -Path $path -DestinationPath $destinationPath
        throw "Failed to validate that an invalid archive file format $invalidArchiveFileExtension was supplied as input to Compress-Archive cmdlet."
    }
    catch
    {
        $_.FullyQualifiedErrorId | Should Be "NotSupportedArchiveFileExtension,Compress-Archive"
    }
}

function Validate-ArchiveEntryCount {
    param
    (
        [string] $path,
        [int] $expectedEntryCount
    )

    Add-CompressionAssemblies
    try
    {
        $archiveFileStreamArgs = @($path, [System.IO.FileMode]::Open)
        $archiveFileStream = New-Object -TypeName System.IO.FileStream -ArgumentList $archiveFileStreamArgs

        $zipArchiveArgs = @($archiveFileStream, [System.IO.Compression.ZipArchiveMode]::Read, $false)
        $zipArchive = New-Object -TypeName System.IO.Compression.ZipArchive -ArgumentList $zipArchiveArgs

        $actualEntryCount = $zipArchive.Entries.Count
        $actualEntryCount | Should Be $expectedEntryCount
    }
    finally
    {
        if ($null -ne $zipArchive) { $zipArchive.Dispose()}
        if ($null -ne $archiveFileStream) { $archiveFileStream.Dispose() }
    }
}

function ArchiveFileEntryContentValidator {
    param
    (
        [string] $path,
        [string] $entryFileName,
        [string] $expectedEntryFileContent
    )

    Add-CompressionAssemblies
    try
    {
        $destFile = "$TestDrive$($DS)ExpandedFile"+([System.Guid]::NewGuid().ToString())+".txt"

        $archiveFileStreamArgs = @($path, [System.IO.FileMode]::Open)
        $archiveFileStream = New-Object -TypeName System.IO.FileStream -ArgumentList $archiveFileStreamArgs

        $zipArchiveArgs = @($archiveFileStream, [System.IO.Compression.ZipArchiveMode]::Read, $false)
        $zipArchive = New-Object -TypeName System.IO.Compression.ZipArchive -ArgumentList $zipArchiveArgs

        $entryToBeUpdated = $zipArchive.Entries | ? {$_.FullName -eq $entryFileName.replace([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)}

        if($entryToBeUpdated -ne $null)
        {
            $srcStream = $entryToBeUpdated.Open()
            $destStream = New-Object "System.IO.FileStream" -ArgumentList( $destFile, [System.IO.FileMode]::Create )
            $srcStream.CopyTo( $destStream )
            $destStream.Dispose()
            $srcStream.Dispose()
            Get-Content $destFile | Should Be $expectedEntryFileContent
        }
        else
        {
            throw "Failed to find the file $entryFileName in the archive file $path"
        }
    }
    finally
    {
        if ($zipArchive)
        {
            $zipArchive.Dispose()
        }
        if ($archiveFileStream)
        {
            $archiveFileStream.Dispose()
        }
    }
}

function ArchiveFileEntrySeparatorValidator {
    param
    (
        [string] $path
    )

    Add-CompressionAssemblies
    try
    {
        $archiveFileStreamArgs = @($path, [System.IO.FileMode]::Open)
        $archiveFileStream = New-Object -TypeName System.IO.FileStream -ArgumentList $archiveFileStreamArgs

        $zipArchiveArgs = @($archiveFileStream, [System.IO.Compression.ZipArchiveMode]::Read, $false)
        $zipArchive = New-Object -TypeName System.IO.Compression.ZipArchive -ArgumentList $zipArchiveArgs

        $badEntries = $zipArchive.Entries | Where-Object {$_.FullName.Contains('\')}

        $badEntries.Count | Should Be 0
    }
    finally
    {
        if ($zipArchive)
        {
            $zipArchive.Dispose()
        }
        if ($archiveFileStream)
        {
            $archiveFileStream.Dispose()
        }
    }
}

function ExpandArchiveInvalidParameterValidator {
    param
    (
        [boolean] $isLiteralPathParameterSet,
        [string[]] $path,
        [string] $destinationPath,
        [string] $expectedFullyQualifiedErrorId
    )

    try
    {
        if($isLiteralPathParameterSet)
        {
            Expand-Archive -LiteralPath $null -DestinationPath $destinationPath
        }
        else
        {
            Expand-Archive -Path $path -DestinationPath $destinationPath
        }

        throw "Expand-Archive did NOT throw expected error"
    }
    catch
    {
        $_.FullyQualifiedErrorId | Should Be $expectedFullyQualifiedErrorId
    }
}
