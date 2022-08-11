<############################################################################################
 # File: Pester.Commands.Cmdlets.ArchiveTests.ps1
 # Commands.Cmdlets.ArchiveTests suite contains Tests that are
 # used for validating Microsoft.PowerShell.Archive module.
 ############################################################################################>
$script:TestSourceRoot = $PSScriptRoot
$DS = [System.IO.Path]::DirectorySeparatorChar
if ($IsWindows -eq $null) {
    $IsWindows = $PSVersionTable.PSEdition -eq "Desktop"
}
Describe "Test suite for Microsoft.PowerShell.Archive module" -Tags "BVT" {

    BeforeAll {
        $originalProgressPref = $ProgressPreference
        $ProgressPreference = "SilentlyContinue"
        $originalPSModulePath = $env:PSModulePath
        # make sure we use the one in this repo
        $env:PSModulePath = "$($script:TestSourceRoot)\..;$($env:PSModulePath)"

        New-Item $TestDrive$($DS)SourceDir -Type Directory | Out-Null
        New-Item $TestDrive$($DS)SourceDir$($DS)ChildDir-1 -Type Directory | Out-Null
        New-Item $TestDrive$($DS)SourceDir$($DS)ChildDir-2 -Type Directory | Out-Null
        New-Item $TestDrive$($DS)SourceDir$($DS)ChildEmptyDir -Type Directory | Out-Null

        $content = "Some Data"
        $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)Sample-1.txt
        $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)Sample-2.txt
        $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-3.txt
        $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-4.txt
        $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)ChildDir-2$($DS)Sample-5.txt
        $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)ChildDir-2$($DS)Sample-6.txt

        "Some Text" > $TestDrive$($DS)Sample.unzip
        "Some Text" > $TestDrive$($DS)Sample.cab

        $preCreatedArchivePath = Join-Path $script:TestSourceRoot "SamplePreCreatedArchive.archive"
        Copy-Item $preCreatedArchivePath $TestDrive$($DS)SamplePreCreatedArchive.zip -Force

        $preCreatedArchivePath = Join-Path $script:TestSourceRoot "TrailingSpacer.archive"
        Copy-Item $preCreatedArchivePath $TestDrive$($DS)TrailingSpacer.zip -Force
    }

    AfterAll {
        $global:ProgressPreference = $originalProgressPref
        $env:PSModulePath = $originalPSModulePath
    }

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
            $destFile = "$TestDrive$($DS)ExpandedFile"+([System.Guid]::NewGuid().ToString())+".txt"

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
                Expand-Archive -LiteralPath $literalPath -DestinationPath $destinationPath
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

    Context "Compress-Archive - Parameter validation test cases" {

        It "Validate errors from Compress-Archive with NULL & EMPTY values for Path, LiteralPath, DestinationPath, CompressionLevel parameters" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)SampleSingleFile.zip"

            CompressArchivePathParameterSetValidator $null $destinationPath
            CompressArchivePathParameterSetValidator $sourcePath $null
            CompressArchivePathParameterSetValidator $null $null

            CompressArchivePathParameterSetValidator "" $destinationPath
            CompressArchivePathParameterSetValidator $sourcePath ""
            CompressArchivePathParameterSetValidator "" ""

            CompressArchivePathParameterSetValidator $null $null "NoCompression"

            CompressArchiveLiteralPathParameterSetValidator $null $destinationPath
            CompressArchiveLiteralPathParameterSetValidator $sourcePath $null
            CompressArchiveLiteralPathParameterSetValidator $null $null

            CompressArchiveLiteralPathParameterSetValidator "" $destinationPath
            CompressArchiveLiteralPathParameterSetValidator $sourcePath ""
            CompressArchiveLiteralPathParameterSetValidator "" ""

            CompressArchiveLiteralPathParameterSetValidator $null $null "NoCompression"

            CompressArchiveLiteralPathParameterSetValidator $sourcePath $destinationPath $null
            CompressArchiveLiteralPathParameterSetValidator $sourcePath $destinationPath ""
        }

        It "Validate errors from Compress-Archive when invalid path (non-existing path / non-filesystem path) is supplied for Path or LiteralPath parameters" {
            CompressArchiveInValidPathValidator "$TestDrive$($DS)InvalidPath" $TestDrive "$TestDrive$($DS)InvalidPath" "ArchiveCmdletPathNotFound,Compress-Archive"
            CompressArchiveInValidPathValidator "$TestDrive" "$TestDrive$($DS)NonExistingDirectory$($DS)sample.zip" "$TestDrive$($DS)NonExistingDirectory$($DS)sample.zip" "ArchiveCmdletPathNotFound,Compress-Archive"

            $path = @("$TestDrive", "$TestDrive$($DS)InvalidPath")
            CompressArchiveInValidPathValidator $path $TestDrive "$TestDrive$($DS)InvalidPath" "ArchiveCmdletPathNotFound,Compress-Archive"

            # The tests below are no longer valid. You can have zip files with non-zip extensions. Different archive
            # formats should be added in a separate pull request, with a parameter to identify the archive format, and
            # default formats associated with specific extensions. Until then, as long as these cmdlets only support
            # Zip files, any file extension is supported.

            #$invalidUnZipFileFormat = "$TestDrive$($DS)Sample.unzip"
            #CompressArchiveInValidArchiveFileExtensionValidator $TestDrive "$invalidUnZipFileFormat" ".unzip"

            #$invalidcabZipFileFormat = "$TestDrive$($DS)Sample.cab"
            #CompressArchiveInValidArchiveFileExtensionValidator $TestDrive "$invalidcabZipFileFormat" ".cab"
        }

        It "Validate error from Compress-Archive when archive file already exists and -Update parameter is not specified" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)ValidateErrorWhenUpdateNotSpecified.zip"

            try
            {
                "Some Data" > $destinationPath
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                throw "Failed to validate that an archive file format $destinationPath already exists and -Update switch parameter is not specified while running Compress-Archive command."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should Be "ArchiveFileExists,Compress-Archive"
            }
        }

        It "Validate error from Compress-Archive when duplicate paths are supplied as input to Path parameter" {
            $sourcePath = @(
                "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt",
                "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt")
            $destinationPath = "$TestDrive$($DS)DuplicatePaths.zip"

            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect that duplicate Path $sourcePath is supplied as input to Path parameter."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should Be "DuplicatePathFound,Compress-Archive"
            }
        }

        It "Validate error from Compress-Archive when duplicate paths are supplied as input to LiteralPath parameter" {
            $sourcePath = @(
                "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt",
                "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt")
            $destinationPath = "$TestDrive$($DS)DuplicatePaths.zip"

            try
            {
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect that duplicate Path $sourcePath is supplied as input to LiteralPath parameter."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should Be "DuplicatePathFound,Compress-Archive"
            }
        }
    }

    Context "Compress-Archive - functional test cases" {
        It "Validate that a single file can be compressed using Compress-Archive cmdlet" {
            $sourcePath = "$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-3.txt"
            $destinationPath = "$TestDrive$($DS)SampleSingleFile.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should Exist
        }
        # This test requires a fix in PS5 to support reading paths with square bracket
        It "Validate that Compress-Archive cmdlet can accept LiteralPath parameter with Special Characters" -skip:(($PSVersionTable.psversion.Major -lt 5) -and ($PSVersionTable.psversion.Minor -lt 0)) {
            $sourcePath = "$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample[]File.txt"
            "Some Random Content" | Out-File -LiteralPath $sourcePath
            $destinationPath = "$TestDrive$($DS)SampleSingleFileWithSpecialCharacters.zip"
            try
            {
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath
                $destinationPath | Should Exist
            }
            finally
            {
                Remove-Item -LiteralPath $sourcePath -Force
            }
        }
        It "Validate that Compress-Archive cmdlet errors out when DestinationPath resolves to multiple locations" {

            New-Item $TestDrive$($DS)SampleDir$($DS)Child-1 -Type Directory -Force | Out-Null
            New-Item $TestDrive$($DS)SampleDir$($DS)Child-2 -Type Directory -Force | Out-Null
            New-Item $TestDrive$($DS)SampleDir$($DS)Test.txt -Type File -Force | Out-Null

            $destinationPath = "$TestDrive$($DS)SampleDir$($DS)Child-*$($DS)SampleChidArchive.zip"
            $sourcePath = "$TestDrive$($DS)SampleDir$($DS)Test.txt"
            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect that destination $destinationPath can resolve to multiple paths"
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should Be "InvalidArchiveFilePath,Compress-Archive"
            }
            finally
            {
                Remove-Item -LiteralPath $TestDrive$($DS)SampleDir -Force -Recurse
            }
        }
        It "Validate that Compress-Archive cmdlet works when DestinationPath has wild card pattern and resolves to a single valid path" {

            New-Item $TestDrive$($DS)SampleDir$($DS)Child-1 -Type Directory -Force | Out-Null
            New-Item $TestDrive$($DS)SampleDir$($DS)Test.txt -Type File -Force | Out-Null

            $destinationPath = "$TestDrive$($DS)SampleDir$($DS)Child-*$($DS)SampleChidArchive.zip"
            $sourcePath = "$TestDrive$($DS)SampleDir$($DS)Test.txt"
            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                $destinationPath | Should Exist
            }
            finally
            {
                Remove-Item -LiteralPath $TestDrive$($DS)SampleDir -Force -Recurse
            }
        }
        It "Validate that Compress-Archive cmdlet works when it ecounters LastWriteTimeValues earlier than 1980" {
            New-Item $TestDrive$($DS)SampleDir$($DS)Child-1 -Type Directory -Force | Out-Null
            $file = New-Item $TestDrive$($DS)SampleDir$($DS)Test.txt -Type File -Force
            $destinationPath = "$TestDrive$($DS)SampleDir$($DS)Child-*$($DS)SampleChidArchive.zip"
            $sourcePath = "$TestDrive$($DS)SampleDir$($DS)Test.txt"

            $file.LastWriteTime = [DateTime]::Parse('1967-03-04T06:00:00')
            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WarningAction SilentlyContinue
                $destinationPath | Should Exist
            }
            finally
            {
                Remove-Item -LiteralPath $TestDrive$($DS)SampleDir -Force -Recurse
            }
        }
        It "Validate that Compress-Archive cmdlet warns when updating the LastWriteTime for files earlier than 1980" {
            New-Item $TestDrive$($DS)SampleDir$($DS)Child-1 -Type Directory -Force | Out-Null
            $file = New-Item $TestDrive$($DS)SampleDir$($DS)Test.txt -Type File -Force
            $destinationPath = "$TestDrive$($DS)SampleDir$($DS)Child-*$($DS)SampleChidArchive.zip"
            $sourcePath = "$TestDrive$($DS)SampleDir$($DS)Test.txt"

            $file.LastWriteTime = [DateTime]::Parse('1967-03-04T06:00:00')
            try
            {
                $ps=[PowerShell]::Create()
                $ps.Streams.Warning.Clear()
                $script = "Import-Module Microsoft.PowerShell.Archive; Compress-Archive -Path $sourcePath -DestinationPath `"$destinationPath`" -CompressionLevel Fastest -Verbose"
                $ps.AddScript($script)
                $ps.Invoke()

                $ps.Streams.Warning.Count -gt 0 | Should Be $True
            }
            finally
            {
                Remove-Item -LiteralPath $TestDrive$($DS)SampleDir -Force -Recurse
            }
        }

        # This test requires a fix in PS5 to support reading paths with square bracket
        It "Validate that Compress-Archive cmdlet can accept LiteralPath parameter for a directory with Special Characters in the directory name"  -skip:(($PSVersionTable.psversion.Major -lt 5) -and ($PSVersionTable.psversion.Minor -lt 0)) {
            $sourcePath = "$TestDrive$($DS)Source[]Dir$($DS)ChildDir[]-1"
            New-Item $sourcePath -Type Directory | Out-Null
            "Some Random Content" | Out-File -LiteralPath "$sourcePath$($DS)Sample[]File.txt"
            $destinationPath = "$TestDrive$($DS)SampleDirWithSpecialCharacters.zip"
            try
            {
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath
                $destinationPath | Should Exist
            }
            finally
            {
                Remove-Item -LiteralPath $sourcePath -Force -Recurse
            }
        }
        It "Validate that Compress-Archive cmdlet can accept DestinationPath parameter with Special Characters" {
            $sourcePath = "$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-3.txt"
            $destinationPath = "$TestDrive$($DS)Sample[]SingleFile.zip"
            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	    Test-Path -LiteralPath $destinationPath | Should Be $true
            }
            finally
            {
                Remove-Item -LiteralPath $destinationPath -Force
            }
        }
        It "Validate that Source Path can be at SystemDrive location" -skip:(!$IsWindows) {
            $sourcePath = "$env:SystemDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)SampleFromSystemDrive.zip"
            New-Item $sourcePath -Type Directory | Out-Null # not enough permissions to write to drive root on Linux
            "Some Data" | Out-File -FilePath $sourcePath$($DS)SampleSourceFileForArchive.txt
            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                Test-Path $destinationPath | Should Be $true
            }
            finally
            {
                del "$sourcePath" -Force -Recurse -ErrorAction SilentlyContinue
            }
        }
        It "Validate that multiple files can be compressed using Compress-Archive cmdlet" {
            $sourcePath = @(
                "$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-3.txt",
                "$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-4.txt",
                "$TestDrive$($DS)SourceDir$($DS)ChildDir-2$($DS)Sample-5.txt",
                "$TestDrive$($DS)SourceDir$($DS)ChildDir-2$($DS)Sample-6.txt")
            $destinationPath = "$TestDrive$($DS)SampleMultipleFiles.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	Test-Path $destinationPath | Should Be $true
        }
        It "Validate that multiple files and directories can be compressed using Compress-Archive cmdlet" {
            $sourcePath = @(
                "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt",
                "$TestDrive$($DS)SourceDir$($DS)Sample-2.txt",
                "$TestDrive$($DS)SourceDir$($DS)ChildDir-1",
                "$TestDrive$($DS)SourceDir$($DS)ChildDir-2")
            $destinationPath = "$TestDrive$($DS)SampleMultipleFilesAndDirs.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	Test-Path $destinationPath | Should Be $true
        }
        It "Validate that a single directory can be compressed using Compress-Archive cmdlet" {
            $sourcePath = @("$TestDrive$($DS)SourceDir$($DS)ChildDir-1")
            $destinationPath = "$TestDrive$($DS)SampleSingleDir.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	Test-Path $destinationPath | Should Be $true
        }
        It "Validate that a single directory with multiple files and subdirectories can be compressed using Compress-Archive cmdlet" {
            $sourcePath = @("$TestDrive$($DS)SourceDir")
            $destinationPath = "$TestDrive$($DS)SampleSubTree.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	Test-Path $destinationPath | Should Be $true
        }
        It "Validate that a single directory & multiple files can be compressed using Compress-Archive cmdlet" {
            $sourcePath = @(
                "$TestDrive$($DS)SourceDir$($DS)ChildDir-1",
                "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt",
                "$TestDrive$($DS)SourceDir$($DS)Sample-2.txt")
            $destinationPath = "$TestDrive$($DS)SampleMultipleFilesAndSingleDir.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	Test-Path $destinationPath | Should Be $true
        }

        It "Validate that if .zip extension is not supplied as input to DestinationPath parameter, then .zip extension is appended" {
            $sourcePath = @("$TestDrive$($DS)SourceDir")
            $destinationPath = "$TestDrive$($DS)SampleNoExtension.zip"
            $destinationWithoutExtensionPath = "$TestDrive$($DS)SampleNoExtension"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationWithoutExtensionPath
        	Test-Path $destinationPath | Should Be $true
        }
        It "Validate that relative path can be specified as Path parameter of Compress-Archive cmdlet" {
            $sourcePath = ".$($DS)SourceDir"
            $destinationPath = "RelativePathForPathParameter.zip"
            try
            {
                Push-Location $TestDrive
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	    Test-Path $destinationPath | Should Be $true
            }
            finally
            {
                Pop-Location
            }
        }
        It "Validate that relative path can be specified as LiteralPath parameter of Compress-Archive cmdlet" {
            $sourcePath = ".$($DS)SourceDir"
            $destinationPath = "RelativePathForLiteralPathParameter.zip"
            try
            {
                Push-Location $TestDrive
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath
        	    Test-Path $destinationPath | Should Be $true
            }
            finally
            {
                Pop-Location
            }
        }
        It "Validate that relative path can be specified as DestinationPath parameter of Compress-Archive cmdlet" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = ".$($DS)RelativePathForDestinationPathParameter.zip"
            try
            {
                Push-Location $TestDrive
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	    Test-Path $destinationPath | Should Be $true
            }
            finally
            {
                Pop-Location
            }
        }
        It "Validate that -Update parameter makes Compress-Archive to not throw an error if archive file already exists" {
            $sourcePath = @("$TestDrive$($DS)SourceDir")
            $destinationPath = "$TestDrive$($DS)SampleUpdateTest.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	Test-Path $destinationPath | Should Be $true
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -Update
        	Test-Path $destinationPath | Should Be $true
        }
        It "Validate -Update parameter by adding a new file to an existing archive file" {
            $sourcePath = @("$TestDrive$($DS)SourceDir$($DS)ChildDir-1")
            $destinationPath = "$TestDrive$($DS)SampleUpdateAdd1File.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	Test-Path $destinationPath | Should Be $true
            New-Item $TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-AddedNewFile.txt -Type File | Out-Null
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -Update
            Test-Path $destinationPath | Should Be $true
            Validate-ArchiveEntryCount -path $destinationPath -expectedEntryCount 3
        }

        It "Validate that all CompressionLevel values can be used with Compress-Archive cmdlet" {
            $sourcePath = "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt"

            $destinationPath = "$TestDrive$($DS)FastestCompressionLevel.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -CompressionLevel Fastest
            Test-Path $destinationPath | Should Be $true

            $destinationPath = "$TestDrive$($DS)OptimalCompressionLevel.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -CompressionLevel Optimal
            Test-Path $destinationPath | Should Be $true

            $destinationPath = "$TestDrive$($DS)NoCompressionCompressionLevel.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -CompressionLevel NoCompression
            Test-Path $destinationPath | Should Be $true
        }

        It "Validate that -Update parameter is modifying a file that already exists in the archive file" {
            $filePath = "$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-3.txt"

            $initialContent = "Initial Content"
            $modifiedContent = "Modified Content"

            $initialContent | Set-Content $filePath

            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)UpdatingModifiedFile.zip"

            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            Test-Path $destinationPath | Should Be $True

            $modifiedContent | Set-Content $filePath

            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -Update
            Test-Path $destinationPath | Should Be $True

            ArchiveFileEntryContentValidator "$destinationPath" "SourceDir$($DS)ChildDir-1$($DS)Sample-3.txt" $modifiedContent
        }

        It "Validate that only / separators are used as archive directory separators" {
            $filePath = "$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-3.txt"

            $initialContent = "Initial Content"
            $modifiedContent = "Modified Content"

            $initialContent | Set-Content $filePath

            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)VerifyingSeparators.zip"

            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            Test-Path $destinationPath | Should Be $True

            ArchiveFileEntrySeparatorValidator "$destinationPath"
        }

        It "Validate Compress-Archive cmdlet in pipleline scenario" {
            $destinationPath = "$TestDrive$($DS)CompressArchiveFromPipeline.zip"

            # Piping a single file path to Compress-Archive
            dir -Path $TestDrive$($DS)SourceDir$($DS)Sample-1.txt | Compress-Archive -DestinationPath $destinationPath
            Test-Path $destinationPath | Should Be $True

            # Piping a string directory path to Compress-Archive
            "$TestDrive$($DS)SourceDir$($DS)ChildDir-2" | Compress-Archive -DestinationPath $destinationPath -Update
            Test-Path $destinationPath | Should Be $True

            # Piping the output of Get-ChildItem to Compress-Archive
            dir "$TestDrive$($DS)SourceDir" -Recurse | Compress-Archive -DestinationPath $destinationPath -Update
            Test-Path $destinationPath | Should Be $True
        }

        It "Validate that Compress-Archive works on ReadOnly files" {
            $sourcePath = "$TestDrive$($DS)ReadOnlyFile.txt"
            $destinationPath = "$TestDrive$($DS)TestForReadOnlyFile.zip"

            "Some Content" | Out-File -FilePath $sourcePath
            $createdItem = Get-Item $sourcePath
            $createdItem.Attributes = 'ReadOnly'

            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	Test-Path $destinationPath | Should Be $true
        }

        It "Validate that Compress-Archive generates Verbose messages" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)Compress-Archive generates VerboseMessages.zip"

            try
            {
                $ps=[PowerShell]::Create()
                $ps.Streams.Error.Clear()
                $ps.Streams.Verbose.Clear()
                $script = "Import-Module Microsoft.PowerShell.Archive; Compress-Archive -Path $sourcePath -DestinationPath `"$destinationPath`" -CompressionLevel Fastest -Verbose"
                $ps.AddScript($script)
                $ps.Invoke()

                $ps.Streams.Verbose.Count -gt 0 | Should Be $True
                $ps.Streams.Error.Count | Should Be 0
            }
            finally
            {
                $ps.Dispose()
            }
        }

        It "Validate that Compress-Archive returns nothing when -PassThru is not used" {
            $sourcePath = @("$TestDrive$($DS)SourceDir")
            $destinationPath = "$TestDrive$($DS)NoPassThruTest.zip"
            $archive = Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $archive | Should Be $null
        }

        It "Validate that Compress-Archive returns nothing when -PassThru is used with a value of $false" {
            $sourcePath = @("$TestDrive$($DS)SourceDir")
            $destinationPath = "$TestDrive$($DS)FalsePassThruTest.zip"
            $archive = Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -PassThru:$false
            $archive | Should Be $null
        }

        It "Validate that Compress-Archive returns the archive when invoked with -PassThru" {
            $sourcePath = @("$TestDrive$($DS)SourceDir")
            $destinationPath = "$TestDrive$($DS)PassThruTest.zip"
            $archive = Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -PassThru
            $archive.FullName | Should Be $destinationPath
        }

        It "Validate that Compress-Archive can create a zip archive that has a different extension" {
            $sourcePath = "$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-3.txt"
            $destinationPath = "$TestDrive$($DS)DifferentZipExtension.dat"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should Exist
        }

        It "Validate that Compress-Archive can create a zip archive when the source is in use" {
            $sourcePath = "$TestDrive$($DS)InUseFile.txt"
            $destinationPath = "$TestDrive$($DS)TestForinUseFile.zip"

            "Some Content" | Out-File -FilePath $sourcePath
            Get-Content $sourcePath
            $TestFile = [System.IO.File]::Open($sourcePath, 'Open', 'Read', 'Read')

            try {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                Test-Path $destinationPath | Should Be $true
            }
            finally {
                $TestFile.Close()
            }
        }
    }

    Context "Expand-Archive - Parameter validation test cases" {
        It "Validate non existing archive -Path trows expected error message" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)ExpandedArchive"
            try
            {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath
        		throw "Expand-Archive succeeded for non existing archive path"
            }
            catch
            {
        		$_.FullyQualifiedErrorId | Should Be "PathNotFound,Expand-Archive"
            }
        }

        It "Validate errors from Expand-Archive with NULL & EMPTY values for Path, LiteralPath, DestinationPath parameters" {
            ExpandArchiveInvalidParameterValidator $false $null "$TestDrive$($DS)SourceDir" "ParameterArgumentValidationError,Expand-Archive"
            ExpandArchiveInvalidParameterValidator $false $null $null "ParameterArgumentValidationError,Expand-Archive"

            ExpandArchiveInvalidParameterValidator $false "$TestDrive$($DS)SourceDir" $null "ParameterArgumentTransformationError,Expand-Archive"
            ExpandArchiveInvalidParameterValidator $false "" "$TestDrive$($DS)SourceDir" "ParameterArgumentTransformationError,Expand-Archive"
            ExpandArchiveInvalidParameterValidator $false "$TestDrive$($DS)SourceDir" "" "ParameterArgumentTransformationError,Expand-Archive"
            ExpandArchiveInvalidParameterValidator $false "" "" "ParameterArgumentTransformationError,Expand-Archive"

            ExpandArchiveInvalidParameterValidator $true $null "$TestDrive$($DS)SourceDir" "ParameterArgumentValidationError,Expand-Archive"
            ExpandArchiveInvalidParameterValidator $true $null $null "ParameterArgumentValidationError,Expand-Archive"

            ExpandArchiveInvalidParameterValidator $true "$TestDrive$($DS)SourceDir" $null "ParameterArgumentValidationError,Expand-Archive"
            ExpandArchiveInvalidParameterValidator $true "" "$TestDrive$($DS)SourceDir" "ParameterArgumentValidationError,Expand-Archive"
            ExpandArchiveInvalidParameterValidator $true "$TestDrive$($DS)SourceDir" "" "ParameterArgumentValidationError,Expand-Archive"
            ExpandArchiveInvalidParameterValidator $true "" "" "ParameterArgumentValidationError,Expand-Archive"

            ExpandArchiveInvalidParameterValidator $true $null "$TestDrive$($DS)SourceDir" "ParameterArgumentValidationError,Expand-Archive"
            ExpandArchiveInvalidParameterValidator $true $null $null "ParameterArgumentValidationError,Expand-Archive"

            ExpandArchiveInvalidParameterValidator $true "$TestDrive$($DS)SourceDir" $null "ParameterArgumentValidationError,Expand-Archive"
            ExpandArchiveInvalidParameterValidator $true "" "$TestDrive$($DS)SourceDir" "ParameterArgumentValidationError,Expand-Archive"
            ExpandArchiveInvalidParameterValidator $true "$TestDrive$($DS)SourceDir" "" "ParameterArgumentValidationError,Expand-Archive"
            ExpandArchiveInvalidParameterValidator $true "" "" "ParameterArgumentValidationError,Expand-Archive"
        }

        It "Validate errors from Expand-Archive when invalid path (non-existing path / non-filesystem path) is supplied for Path or LiteralPath parameters" {
            try { Expand-Archive -Path "$TestDrive$($DS)NonExistingArchive" -DestinationPath "$TestDrive$($DS)SourceDir"; throw "Expand-Archive did NOT throw expected error" }
            catch { $_.FullyQualifiedErrorId | Should Be "ArchiveCmdletPathNotFound,Expand-Archive" }

            try { Expand-Archive -LiteralPath "$TestDrive$($DS)NonExistingArchive" -DestinationPath "$TestDrive$($DS)SourceDir"; throw "Expand-Archive did NOT throw expected error" }
            catch { $_.FullyQualifiedErrorId | Should Be "ArchiveCmdletPathNotFound,Expand-Archive" }
        }

        It "Validate error from Expand-Archive when invalid path (non-existing path / non-filesystem path) is supplied for DestinationPath parameter" {
            $sourcePath = "$TestDrive$($DS)SamplePreCreatedArchive.zip"
            $destinationPath = "HKLM:\SOFTWARE"
            if ($IsWindows) {
                $expectedError = "InvalidDirectoryPath,Expand-Archive"
            }
            else {
                $expectedError = "DriveNotFound,Microsoft.PowerShell.Commands.NewItemCommand"
            }
            try { Expand-Archive -Path $sourcePath -DestinationPath $destinationPath; throw "Expand-Archive did NOT throw expected error" }
            catch { $_.FullyQualifiedErrorId | Should Be $expectedError }
        }

        It "Validate that you can compress an archive to a custom PSDrive using the Compress-Archive cmdlet" {
            $sourcePath = "$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-3.txt"
            $destinationDriveName = 'CompressArchivePesterTest'
            $destinationDrive = New-PSDrive -Name $destinationDriveName -PSProvider FileSystem -Root $TestDrive -Scope Global
            $destinationPath = "${destinationDriveName}:$($DS)CompressToPSDrive.zip"
            try {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                $destinationPath | Should Exist
            } finally {
                Remove-PSDrive -LiteralName $destinationDriveName
            }
        }
    }

    Context "Expand-Archive - functional test cases" {

        It "Validate basic Expand-Archive scenario" {
            $sourcePath = "$TestDrive$($DS)SamplePreCreatedArchive.zip"
            $content = "Some Data"
            $destinationPath = "$TestDrive$($DS)DestDirForBasicExpand"
            $files = @("Sample-1.txt", "Sample-2.txt")

            # The files in "$TestDrive$($DS)SamplePreCreatedArchive.zip" are precreated.
            $fileCreationTimeStamp = Get-Date -Year 2014 -Month 6 -Day 13 -Hour 15 -Minute 50 -Second 20 -Millisecond 0

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath
            foreach($currentFile in $files)
            {
                $expandedFile = Join-Path $destinationPath -ChildPath $currentFile
                Test-Path $expandedFile | Should Be $True

                # We are validating to make sure that time stamps are preserved in the
                # compressed archive are reflected back when the file is expanded.
                (dir $expandedFile).LastWriteTime.CompareTo($fileCreationTimeStamp) | Should Be 0

                Get-Content $expandedFile | Should Be $content
            }
        }
        It "Validate that Expand-Archive cmdlet errors out when DestinationPath resolves to multiple locations" {
            New-Item $TestDrive$($DS)SampleDir$($DS)Child-1 -Type Directory -Force | Out-Null
            New-Item $TestDrive$($DS)SampleDir$($DS)Child-2 -Type Directory -Force | Out-Null

            $destinationPath = "$TestDrive$($DS)SampleDir$($DS)Child-*"
            $sourcePath = "$TestDrive$($DS)SamplePreCreatedArchive.zip"
            try
            {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect that destination $destinationPath can resolve to multiple paths"
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should Be "InvalidDestinationPath,Expand-Archive"
            }
            finally
            {
                Remove-Item -LiteralPath $TestDrive$($DS)SampleDir -Force -Recurse
            }
        }
        It "Validate that Expand-Archive cmdlet works when DestinationPath resolves has wild card pattern and resolves to a single valid path" {
            New-Item $TestDrive$($DS)SampleDir$($DS)Child-1 -Type Directory -Force | Out-Null

            $destinationPath = "$TestDrive$($DS)SampleDir$($DS)Child-*"
            $sourcePath = "$TestDrive$($DS)SamplePreCreatedArchive.zip"
            try
            {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath
                $expandedFiles = Get-ChildItem $destinationPath -Recurse
                $expandedFiles.Length | Should BeGreaterThan 1
            }
            finally
            {
                Remove-Item -LiteralPath $TestDrive$($DS)SampleDir -Force -Recurse
            }
        }
        It "Validate Expand-Archive scenario where DestinationPath has Special Characters" {
            $sourcePath = "$TestDrive$($DS)SamplePreCreatedArchive.zip"
            $content = "Some Data"
            $destinationPath = "$TestDrive$($DS)DestDir[]Expand"
            $files = @("Sample-1.txt", "Sample-2.txt")

            # The files in "$TestDrive$($DS)SamplePreCreatedArchive.zip" are precreated.
            $fileCreationTimeStamp = Get-Date -Year 2014 -Month 6 -Day 13 -Hour 15 -Minute 50 -Second 20 -Millisecond 0

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath
            foreach($currentFile in $files)
            {
                $expandedFile = Join-Path $destinationPath -ChildPath $currentFile
                Test-Path -LiteralPath $expandedFile | Should Be $True

                # We are validating to make sure that time stamps are preserved in the
                # compressed archive are reflected back when the file is expanded.
                (dir -LiteralPath $expandedFile).LastWriteTime.CompareTo($fileCreationTimeStamp) | Should Be 0

                Get-Content -LiteralPath $expandedFile | Should Be $content
            }
        }
        It "Invoke Expand-Archive with relative path in Path parameter and -Force parameter" {
            $sourcePath = ".$($DS)SamplePreCreatedArchive.zip"
            $destinationPath = "$TestDrive$($DS)SomeOtherNonExistingDir$($DS)Path"
            try
            {
                Push-Location $TestDrive

                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -Force
                $expandedFiles = Get-ChildItem $destinationPath -Recurse
                $expandedFiles.Length | Should Be 2
            }
            finally
            {
                Pop-Location
            }
        }

        It "Invoke Expand-Archive with relative path in LiteralPath parameter and -Force parameter" {
            $sourcePath = ".$($DS)SamplePreCreatedArchive.zip"
            $destinationPath = "$TestDrive$($DS)SomeOtherNonExistingDir$($DS)LiteralPath"
            try
            {
                Push-Location $TestDrive

                Expand-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath -Force
                $expandedFiles = Get-ChildItem $destinationPath -Recurse
                $expandedFiles.Length | Should Be 2
            }
            finally
            {
                Pop-Location
            }
        }

        It "Invoke Expand-Archive with non-existing relative directory in DestinationPath parameter and -Force parameter" {
            $sourcePath = "$TestDrive$($DS)SamplePreCreatedArchive.zip"
            $destinationPath = ".$($DS)SomeOtherNonExistingDir$($DS)DestinationPath"
            try
            {
                Push-Location $TestDrive

                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -Force
                $expandedFiles = Get-ChildItem $destinationPath -Recurse
                $expandedFiles.Length | Should Be 2
            }
            finally
            {
                Pop-Location
            }
        }

        # The test below is no longer valid. You can have zip files with non-zip extensions. Different archive
        # formats should be added in a separate pull request, with a parameter to identify the archive format, and
        # default formats associated with specific extensions. Until then, as long as these cmdlets only support
        # Zip files, any file extension is supported.
        #It "Invoke Expand-Archive with unsupported archive format" {
        #    $sourcePath = "$TestDrive$($DS)Sample.cab"
        #    $destinationPath = "$TestDrive$($DS)UnsupportedArchiveFormatDir"
        #    try
        #    {
        #        Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -Force
        #        throw "Failed to detect unsupported archive format at $sourcePath"
        #    }
        #    catch
        #    {
        #        $_.FullyQualifiedErrorId | Should Be "NotSupportedArchiveFileExtension,Expand-Archive"
        #    }
        #}

        It "Invoke Expand-Archive with archive file containing multiple files, directories with subdirectories and empty directories" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $archivePath = "$TestDrive$($DS)FileAndDirTreeForExpand.zip"
            $destinationPath = "$TestDrive$($DS)FileAndDirTree"
            $sourceList = dir $sourcePath -Name

            Add-CompressionAssemblies
            [System.IO.Compression.ZipFile]::CreateFromDirectory($sourcePath, $archivePath)

            Expand-Archive -Path $archivePath -DestinationPath $destinationPath
            $extractedList = dir $destinationPath -Name

            Compare-Object -ReferenceObject $extractedList -DifferenceObject $sourceList -PassThru | Should Be $null
        }

        It "Validate Expand-Archive cmdlet in pipleline scenario" {
            $sourcePath = "$TestDrive$($DS)SamplePreCreated*.zip"
            $destinationPath = "$TestDrive$($DS)PipeToExpandArchive"

            $content = "Some Data"
            $files = @("Sample-1.txt", "Sample-2.txt")

            dir $sourcePath | Expand-Archive -DestinationPath $destinationPath

            foreach($currentFile in $files)
            {
                $expandedFile = Join-Path $destinationPath -ChildPath $currentFile
                Test-Path $expandedFile | Should Be $True
                Get-Content $expandedFile | Should Be $content
            }
        }

        It "Validate that Expand-Archive generates Verbose messages" {
            $sourcePath = "$TestDrive$($DS)SamplePreCreatedArchive.zip"
            $destinationPath = "$TestDrive$($DS)VerboseMessagesInExpandArchive"

            try
            {
                $ps=[PowerShell]::Create()
                $ps.Streams.Error.Clear()
                $ps.Streams.Verbose.Clear()
                $script = "Import-Module Microsoft.PowerShell.Archive; Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -Verbose"
                $ps.AddScript($script)
                $ps.Invoke()

                $ps.Streams.Verbose.Count -gt 0 | Should Be $True
                $ps.Streams.Error.Count | Should Be 0
            }
            finally
            {
                $ps.Dispose()
            }
        }

        It "Validate that without -Force parameter Expand-Archive generates non-terminating errors without overwriting existing files" {
            $sourcePath = "$TestDrive$($DS)SamplePreCreatedArchive.zip"
            $destinationPath = "$TestDrive$($DS)NoForceParameterExpandArchive"

            try
            {
                $ps=[PowerShell]::Create()
                $ps.Streams.Error.Clear()
                $ps.Streams.Verbose.Clear()
                $script = "Import-Module Microsoft.PowerShell.Archive; Expand-Archive -Path $sourcePath -DestinationPath $destinationPath; Expand-Archive -Path $sourcePath -DestinationPath $destinationPath"
                $ps.AddScript($script)
                $ps.Invoke()

                $ps.Streams.Error.Count -gt 0 | Should Be $True
            }
            finally
            {
                $ps.Dispose()
            }
        }

        It "Validate that without DestinationPath parameter Expand-Archive cmdlet succeeds in expanding the archive" {
            $sourcePath = "$TestDrive$($DS)SamplePreCreatedArchive.zip"
            $archivePath = "$TestDrive$($DS)NoDestinationPathParameter.zip"
            $destinationPath = "$TestDrive$($DS)NoDestinationPathParameter"
            copy $sourcePath $archivePath -Force

            try
            {
                Push-Location $TestDrive

                Expand-Archive -Path $archivePath
                (dir $destinationPath).Count | Should Be 2
            }
            finally
            {
                Pop-Location
            }
        }

        It "Validate that without DestinationPath parameter Expand-Archive cmdlet succeeds in expanding the archive when destination directory exists" {
            $sourcePath = "$TestDrive$($DS)SamplePreCreatedArchive.zip"
            $archivePath = "$TestDrive$($DS)NoDestinationPathParameterDirExists.zip"
            $destinationPath = "$TestDrive$($DS)NoDestinationPathParameterDirExists"
            copy $sourcePath $archivePath -Force
            New-Item -Path $destinationPath -ItemType Directory | Out-Null

            try
            {
                Push-Location $TestDrive

                Expand-Archive -Path $archivePath
                (dir $destinationPath).Count | Should Be 2
            }
            finally
            {
                Pop-Location
            }
        }

        It "Validate that Expand-Archive returns nothing when -PassThru is not used" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $archivePath = "$TestDrive$($DS)NoPassThruTestForExpand.zip"
            $destinationPath = "$TestDrive$($DS)NoPassThruTest"

            $sourceList = dir $sourcePath -Name

            Add-CompressionAssemblies
            [System.IO.Compression.ZipFile]::CreateFromDirectory($sourcePath, $archivePath)

            $contents = Expand-Archive -Path $archivePath -DestinationPath $destinationPath

            $contents | Should Be $null
        }

        It "Validate that Expand-Archive returns nothing when -PassThru is used with a value of $false" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $archivePath = "$TestDrive$($DS)FalsePassThruTestForExpand.zip"
            $destinationPath = "$TestDrive$($DS)FalsePassThruTest"
            $sourceList = dir $sourcePath -Name

            Add-CompressionAssemblies
            [System.IO.Compression.ZipFile]::CreateFromDirectory($sourcePath, $archivePath)

            $contents = Expand-Archive -Path $archivePath -DestinationPath $destinationPath -PassThru:$false

            $contents | Should Be $null
        }

        It "Validate that Expand-Archive returns the contents of the archive -PassThru" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $archivePath = "$TestDrive$($DS)PassThruTestForExpand.zip"
            $destinationPath = "$TestDrive$($DS)PassThruTest"
            $sourceList = dir $sourcePath -Name

            Add-CompressionAssemblies
            [System.IO.Compression.ZipFile]::CreateFromDirectory($sourcePath, $archivePath)

            $contents = Expand-Archive -Path $archivePath -DestinationPath $destinationPath -PassThru | Sort-Object -Property PSParentPath,PSIsDirectory,Name
            # We pipe Get-ChildItem to Get-Item here because the ToString results are different between the two, and we
            # need to compare with other Get-Item results
            $extractedList = Get-ChildItem -Recurse -LiteralPath $destinationPath | Get-Item

            Compare-Object -ReferenceObject $extractedList -DifferenceObject $contents -PassThru | Should Be $null
        }

        It "Validate Expand-Archive works with zip files that have non-zip file extensions" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $archivePath = "$TestDrive$($DS)NonZipFileExtension.dat"
            $destinationPath = "$TestDrive$($DS)NonZipFileExtension"
            $sourceList = dir $sourcePath -Name

            Add-CompressionAssemblies
            [System.IO.Compression.ZipFile]::CreateFromDirectory($sourcePath, $archivePath)

            Expand-Archive -Path $archivePath -DestinationPath $destinationPath
            $extractedList = dir $destinationPath -Name

            Compare-Object -ReferenceObject $extractedList -DifferenceObject $sourceList -PassThru | Should Be $null
        }

        # trailing spaces give this error on Linux: Exception calling "[System.IO.Compression.ZipFileExtensions]::ExtractToFile" with "3" argument(s): "Could not find a part of the path '/tmp/02132f1d-5b0c-4a99-b5bf-707cef7681a6/TrailingSpacer/Inner/TrailingSpace/test.txt'."
        It "Validate Expand-Archive works with zip files where the contents contain trailing whitespace" -skip:(!$IsWindows) {
            $archivePath = "$TestDrive$($DS)TrailingSpacer.zip"
            $destinationPath = "$TestDrive$($DS)TrailingSpacer"
            # we can't just compare the output and the results as you only get one DirectoryInfo for directories that only contain directories
            $expectedPaths = "$TestDrive$($DS)TrailingSpacer$($DS)Inner$($DS)TrailingSpace","$TestDrive$($DS)TrailingSpacer$($DS)Inner$($DS)TrailingSpace$($DS)test.txt"

            $contents = Expand-Archive -Path $archivePath -DestinationPath $destinationPath -PassThru

            $contents.Count | Should Be $expectedPaths.Count

            for ($i = 0; $i -lt $expectedPaths.Count; $i++) {
                $contents[$i].FullName | Should Be $expectedPaths[$i]
            }
        }

        It "Validate that Compress-Archive/Expand-Archive work with backslashes and forward slashes in paths" {
            $sourcePath = "$TestDrive\SourceDir/ChildDir-2"
            $archivePath = "$TestDrive\MixedSlashesDir1/MixedSlashesDir2/SampleMixedslashFile.zip"
            $expandPath = "$TestDrive\MixedSlashesExpandDir/DirA\DirB/DirC"

            New-Item -Path (Split-Path $archivePath) -Type Directory | Out-Null
            Compress-Archive -Path $sourcePath -DestinationPath $archivePath
            $archivePath | Should Exist

            $content = "Some Data"
            $files = @("ChildDir-2$($DS)Sample-5.txt", "ChildDir-2$($DS)Sample-6.txt")
            Expand-Archive -Path $archivePath -DestinationPath $expandPath
            foreach($currentFile in $files)
            {
                $expandedFile = Join-Path $expandPath -ChildPath $currentFile
                Test-Path $expandedFile | Should Be $True
                Get-Content $expandedFile | Should Be $content
            }
        }

        It "Validate that Compress-Archive/Expand-Archive work with dates earlier than 1980" {
            $file1 = New-Item $TestDrive$($DS)SourceDir$($DS)EarlierThan1980.txt -Type File -Force   
            $file1.LastWriteTime = [DateTime]::Parse('1974-10-03T04:30:00')
            $file2 = Get-Item "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt"
            $expandPath = "$TestDrive$($DS)EarlyYearDir"
            $expectedFile1 = "$expandPath$($DS)EarlierThan1980.txt"
            $expectedFile2 = "$expandPath$($DS)Sample-1.txt"
            $archivePath = "$TestDrive$($DS)EarlyYear.zip"

            try
            {
                Compress-Archive -Path @($file1, $file2) -DestinationPath $archivePath -WarningAction SilentlyContinue
                $archivePath | Should Exist
                
                Expand-Archive -Path $archivePath -DestinationPath $expandPath

                $expectedFile1 | Should Exist
                (Get-Item $expectedFile1).LastWriteTime | Should Be $([DateTime]::Parse('1980-01-01T00:00'))
                $expectedFile2 | Should Exist
                (Get-Item $expectedFile2).LastWriteTime | Should Not Be $([DateTime]::Parse('1980-01-01T00:00'))
                
            }
            finally
            {
                Remove-Item -LiteralPath $archivePath -Force -Recurse
            }
        }

        # test is currently blocked by https://github.com/dotnet/corefx/issues/24832
        It "Validate module can be imported when current language is not en-US" -Pending {
            $currentCulture = [System.Threading.Thread]::CurrentThread.CurrentUICulture
            try {
                [System.Threading.Thread]::CurrentThread.CurrentCulture = [CultureInfo]::new("he-IL")
                { Import-Module Microsoft.PowerShell.Archive -Force -ErrorAction Stop } | Should Not Throw
            }
            finally {
                [System.Threading.Thread]::CurrentThread.CurrentCulture = $currentCulture
            }
        }
    }
}