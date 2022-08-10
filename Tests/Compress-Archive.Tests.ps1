# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

BeforeDiscovery {
      # Loads and registers custom assertion. Ignores usage of unapproved verb with -DisableNameChecking
      Import-Module "$PSScriptRoot/Assertions/Should-BeZipArchiveOnlyContaining.psm1" -DisableNameChecking
}

 Describe("Microsoft.PowerShell.Archive tests") {
    BeforeAll {

        $originalProgressPref = $ProgressPreference
        $ProgressPreference = "SilentlyContinue"
        $originalPSModulePath = $env:PSModulePath
    }
    
    AfterAll {
        $global:ProgressPreference = $originalProgressPref
        $env:PSModulePath = $originalPSModulePath
    }

    Context "Parameter set validation tests" {
        BeforeAll {
            # Set up files for tests
            New-Item TestDrive:/SourceDir -Type Directory
            "Some Data" | Out-File -FilePath TestDrive:/SourceDir/Sample-1.txt
            New-Item TestDrive:/EmptyDirectory -Type Directory | Out-Null
        }


        It "Validate errors from Compress-Archive with null and empty values for Path, LiteralPath, and DestinationPath parameters" -ForEach @(
            @{ Path = $null; DestinationPath = "TestDrive:/archive1.zip" }
            @{ Path = "TestDrive:/SourceDir"; DestinationPath = $null }
            @{ Path = $null; DestinationPath = $null }
            @{ Path = ""; DestinationPath = "TestDrive:/archive1.zip" }
            @{ Path = "TestDrive:/SourceDir"; DestinationPath = "" }
            @{ Path = ""; DestinationPath = "" }
        ) {
            try
            {
                Compress-Archive -Path $Path -DestinationPath $DestinationPath
                throw "ValidateNotNullOrEmpty attribute is missing on one of parameters belonging to LiteralPath parameterset."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "ParameterArgumentValidationError,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }

            try
            {
                Compress-Archive -LiteralPath $Path -DestinationPath $DestinationPath
                throw "ValidateNotNullOrEmpty attribute is missing on one of parameters belonging to LiteralPath parameterset."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "ParameterArgumentValidationError,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Validate errors from Compress-Archive when invalid path is supplied for Path or LiteralPath parameters" -ForEach @(
            @{ Path = "Variable:/PWD" }
            @{ Path = @("TestDrive:/", "Variable:/PWD") }
        ) {
            $DestinationPath = "TestDrive:/archive2.zip"

            Compress-Archive -Path $Path -DestinationPath $DestinationPath -ErrorAction SilentlyContinue -ErrorVariable error
            $error.Count | Should -Be 1
            $error[0].FullyQualifiedErrorId | Should -Be "InvalidPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            Remove-Item -Path $DestinationPath

            Compress-Archive -LiteralPath $Path -DestinationPath $DestinationPath -ErrorAction SilentlyContinue -ErrorVariable error
            $error.Count | Should -Be 1
            $error[0].FullyQualifiedErrorId | Should -Be "InvalidPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            Remove-Item -Path $DestinationPath
        }

        It "Throws terminating error when non-existing path is supplied for Path or LiteralPath parameters" -ForEach @(
            @{ Path = "TestDrive:/DoesNotExist" }
            @{ Path = @("TestDrive:/", "TestDrive:/DoesNotExist") }
        ) -Tag this2 {
            $DestinationPath = "TestDrive:/archive3.zip"

            try
            {
                Compress-Archive -Path $Path -DestinationPath $DestinationPath
                throw "Failed to validate that an invalid Path was supplied as input to Compress-Archive cmdlet."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "PathNotFound,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }

            try
            {
                Compress-Archive -LiteralPath $Path -DestinationPath $DestinationPath
                throw "Failed to validate that an invalid LiteralPath was supplied as input to Compress-Archive cmdlet."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "PathNotFound,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Validate error from Compress-Archive when duplicate paths are supplied as input to Path parameter" {
            $sourcePath = @(
                "TestDrive:/SourceDir/Sample-1.txt",
                "TestDrive:/SourceDir/Sample-1.txt")
            $destinationPath = "TestDrive:/DuplicatePaths.zip"

            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect that duplicate Path $sourcePath is supplied as input to Path parameter."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "DuplicatePaths,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Validate error from Compress-Archive when duplicate paths are supplied as input to LiteralPath parameter" {
            $sourcePath = @(
                "TestDrive:/SourceDir/Sample-1.txt",
                "TestDrive:/SourceDir/Sample-1.txt")
            $destinationPath = "TestDrive:/DuplicatePaths.zip"

            try
            {
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect that duplicate Path $sourcePath is supplied as input to LiteralPath parameter."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "DuplicatePaths,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        ## From 504
        It "Validate that Source Path can be at SystemDrive location" -Skip {
            $sourcePath = "$env:SystemDrive/SourceDir"
            $destinationPath = "TestDrive:/SampleFromSystemDrive.zip"
            New-Item $sourcePath -Type Directory | Out-Null # not enough permissions to write to drive root on Linux
            "Some Data" | Out-File -FilePath $sourcePath/SampleSourceFileForArchive.txt
            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                Test-Path $destinationPath | Should -Be $true
            }
            finally
            {
                Remove-Item "$sourcePath" -Force -Recurse -ErrorAction SilentlyContinue
            }
        }

        # This cannot happen in -WriteMode Create because another error will be throw before
        It "Throws an error when Path and DestinationPath are the same" -Skip {
            $sourcePath = "TestDrive:/SourceDir/Sample-1.txt"
            $destinationPath = $sourcePath

            try {
                # Note the cmdlet performs validation on $destinationPath
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect an error when Path and DestinationPath are the same"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SamePathAndDestinationPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws an error when Path and DestinationPath are the same and -Update is specified" {
            $sourcePath = "TestDrive:/SourceDir/Sample-1.txt"
            $destinationPath = $sourcePath

            try {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Update
                throw "Failed to detect an error when Path and DestinationPath are the same and -Update is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SamePathAndDestinationPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws an error when Path and DestinationPath are the same and -Overwrite is specified" {
            $sourcePath = "TestDrive:/EmptyDirectory"
            $destinationPath = $sourcePath

            try {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite
                throw "Failed to detect an error when Path and DestinationPath are the same and -Overwrite is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SamePathAndDestinationPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws an error when LiteralPath and DestinationPath are the same" -Skip {
            $sourcePath = "TestDrive:/SourceDir/Sample-1.txt"
            $destinationPath = $sourcePath

            try {
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect an error when LiteralPath and DestinationPath are the same"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SameLiteralPathAndDestinationPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws an error when LiteralPath and DestinationPath are the same and -Update is specified" {
            $sourcePath = "TestDrive:/SourceDir/Sample-1.txt"
            $destinationPath = $sourcePath

            try {
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath -WriteMode Update
                throw "Failed to detect an error when LiteralPath and DestinationPath are the same and -Update is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SameLiteralPathAndDestinationPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws an error when LiteralPath and DestinationPath are the same and -Overwrite is specified" {
            $sourcePath = "TestDrive:/EmptyDirectory"
            $destinationPath = $sourcePath

            try {
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite
                throw "Failed to detect an error when LiteralPath and DestinationPath are the same and -Overwrite is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SameLiteralPathAndDestinationPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws an error when an invalid path is supplied to DestinationPath" {
            $sourcePath = "TestDrive:/SourceDir"
            $destinationPath = "Variable:/PWD"
            
            try {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect an error when an invalid path is supplied to DestinationPath"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "InvalidPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }
    }

    Context "WriteMode tests" {
        BeforeAll {
            New-Item TestDrive:/SourceDir -Type Directory | Out-Null
    
            $content = "Some Data"
            $content | Out-File -FilePath TestDrive:/SourceDir/Sample-1.txt
        }

        It "Throws a terminating error when an incorrect value is supplied to -WriteMode" {
            $sourcePath = "TestDrive:/SourceDir"
            $destinationPath = "TestDrive:/archive1.zip"

            try {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode mode
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "CannotConvertArgumentNoMessage,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "-WriteMode Create works" -Tag td1 {
            $sourcePath = "TestDrive:/SourceDir"
            $destinationPath = "TestDrive:/archive1.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -Verbose
            if ($IsWindows) {
                $t = Convert-Path $destinationPath
                7z l "${t}" | Write-Verbose -Verbose
            }
            $destinationPath | Should -BeZipArchiveOnlyContaining @('SourceDir/', 'SourceDir/Sample-1.txt')

            
        }
    }

    Context "Basic functional tests" {
        BeforeAll {
            New-Item TestDrive:/SourceDir -Type Directory | Out-Null
            New-Item TestDrive:/SourceDir/ChildDir-1 -Type Directory | Out-Null
            New-Item TestDrive:/SourceDir/ChildDir-2 -Type Directory | Out-Null
            New-Item TestDrive:/SourceDir/ChildEmptyDir -Type Directory | Out-Null

            # create an empty directory
            New-Item TestDrive:/EmptyDir -Type Directory | Out-Null
    
            $content = "Some Data"
            $content | Out-File -FilePath TestDrive:/SourceDir/Sample-1.txt
            $content | Out-File -FilePath TestDrive:/SourceDir/ChildDir-1/Sample-2.txt
            $content | Out-File -FilePath TestDrive:/SourceDir/ChildDir-2/Sample-3.txt

            "Hello, World!" | Out-File -FilePath $TestDrive$($DS)HelloWorld.txt

            # Create a zero-byte file
            New-Item $TestDrive$($DS)EmptyFile -Type File | Out-Null

            # Create a file whose last write time is before 1980
            $content | Out-File -FilePath TestDrive:/OldFile.txt
            Set-ItemProperty -Path TestDrive:/OldFile.txt -Name LastWriteTime -Value '1974-01-16 14:44'

            # Create a directory whose last write time is before 1980
            New-Item -Path "TestDrive:/olddirectory" -ItemType Directory
            Set-ItemProperty -Path "TestDrive:/olddirectory" -Name "LastWriteTime" -Value '1974-01-16 14:44'
        }

        It "Compresses a single file" {
            $sourcePath = "$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-2.txt"
            $destinationPath = "$TestDrive$($DS)archive1.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -BeZipArchiveOnlyContaining @('Sample-2.txt')
        }

        It "Compresses a non-empty directory" {
            $sourcePath =  "$TestDrive$($DS)SourceDir$($DS)ChildDir-1"
            $destinationPath = "$TestDrive$($DS)archive2.zip"
            
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -Exist
            Test-ZipArchive $destinationPath @('ChildDir-1/', 'ChildDir-1/Sample-2.txt')
        }

        It "Compresses an empty directory" {
            $sourcePath = "$TestDrive$($DS)EmptyDir"
            $destinationPath = "$TestDrive$($DS)archive3.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -BeZipArchiveOnlyContaining @('EmptyDir/')
        }

        It "Compresses multiple files" {
            $sourcePath = @("$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-2.txt", "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt")
            $destinationPath = "$TestDrive$($DS)archive4.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -Exist
            Test-ZipArchive $destinationPath @('Sample-1.txt', 'Sample-2.txt')
        }

        It "Compresses multiple files and a single empty directory" {
            $sourcePath = @("$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-2.txt", "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt", 
            "$TestDrive$($DS)SourceDir$($DS)ChildEmptyDir")
            
            $destinationPath = "$TestDrive$($DS)archive5.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -Exist
            Test-ZipArchive $destinationPath @('Sample-1.txt', 'Sample-2.txt', 'ChildEmptyDir/')
        }

        It "Compresses multiple files and a single non-empty directory" {
            $sourcePath = @("$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-2.txt", "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt", 
            "$TestDrive$($DS)SourceDir$($DS)ChildDir-2")
            
            $destinationPath = "$TestDrive$($DS)archive6.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -Exist
            Test-ZipArchive $destinationPath @('Sample-1.txt', 'Sample-2.txt', 'ChildDir-2/', 'ChildDir-2/Sample-3.txt')
        }

        It "Compresses multiple files and non-empty directories" {
            $sourcePath = @("$TestDrive$($DS)HelloWorld.txt", "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt", 
            "$TestDrive$($DS)SourceDir$($DS)ChildDir-1", "$TestDrive$($DS)SourceDir$($DS)ChildDir-2")
            
            $destinationPath = "$TestDrive$($DS)archive7.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -Exist
            Test-ZipArchive $destinationPath @('Sample-1.txt', 'HelloWorld.txt', 'ChildDir-1/', 'ChildDir-2/', 
            'ChildDir-1/Sample-2.txt', 'ChildDir-2/Sample-3.txt')
        }

        It "Compresses multiple files, non-empty directories, and an empty directory" {
            $sourcePath = @("$TestDrive$($DS)HelloWorld.txt", "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt", 
            "$TestDrive$($DS)SourceDir$($DS)ChildDir-1", "$TestDrive$($DS)SourceDir$($DS)ChildDir-2", "$TestDrive$($DS)SourceDir$($DS)ChildEmptyDir")
            
            $destinationPath = "$TestDrive$($DS)archive8.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -Exist
            Test-ZipArchive $destinationPath @('Sample-1.txt', 'HelloWorld.txt', 'ChildDir-1/', 'ChildDir-2/', 
            'ChildDir-1/Sample-2.txt', 'ChildDir-2/Sample-3.txt', "ChildEmptyDir/")
        }

        It "Compresses a directory containing files, non-empty directories, and an empty directory can be compressed" -Tag td4 {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)archive9.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -BeZipArchiveOnlyContaining @('SourceDir/', 'SourceDir/ChildDir-1/', 'SourceDir/ChildDir-2/', 'SourceDir/ChildEmptyDir/', 'SourceDir/Sample-1.txt', 'SourceDir/ChildDir-1/Sample-2.txt', 'SourceDir/ChildDir-2/Sample-3.txt')
        }

        It "Compresses a zero-byte file" {
            $sourcePath = "$TestDrive$($DS)EmptyFile"
            $destinationPath = "$TestDrive$($DS)archive10.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -Exist
            $contents = @('EmptyFile')
            Test-ZipArchive $destinationPath $contents
        }

        It "Compresses a file whose last write time is before 1980" {
            $sourcePath = "$TestDrive$($DS)OldFile.txt"
            $destinationPath = "$TestDrive$($DS)archive11.zip"

            # Assert the last write time of the file is before 1980
            $dateProperty = Get-ItemPropertyValue -Path $sourcePath -Name "LastWriteTime"
            $dateProperty.Year | Should -BeLessThan 1980

            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -Exist
            Test-ZipArchive $destinationPath @('OldFile.txt')

            # Get the archive
            $fileMode = [System.IO.FileMode]::Open
            $archiveStream = New-Object -TypeName System.IO.FileStream -ArgumentList $destinationPath,$fileMode
            $zipArchiveMode = [System.IO.Compression.ZipArchiveMode]::Read
            $archive = New-Object -TypeName System.IO.Compression.ZipArchive -ArgumentList $archiveStream,$zipArchiveMode
            $entry = $archive.GetEntry("OldFile.txt")
            $entry | Should -Not -BeNullOrEmpty

            $entry.LastWriteTime.Year | Should -BeExactly 1980
            $entry.LastWriteTime.Month| Should -BeExactly 1
            $entry.LastWriteTime.Day | Should -BeExactly 1
            $entry.LastWriteTime.Hour | Should -BeExactly 0
            $entry.LastWriteTime.Minute | Should -BeExactly 0
            $entry.LastWriteTime.Second | Should -BeExactly 0
            $entry.LastWriteTime.Millisecond | Should -BeExactly 0


            $archive.Dispose()
            $archiveStream.Dispose()
        }

        It "Compresses a directory whose last write time is before 1980" {
            $sourcePath = "TestDrive:/olddirectory"
            $destinationPath = "${TestDrive}/archive12.zip"

            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -Exist
            Test-ZipArchive $destinationPath @('olddirectory/')

            # Get the archive
            $fileMode = [System.IO.FileMode]::Open
            $archiveStream = New-Object -TypeName System.IO.FileStream -ArgumentList $destinationPath,$fileMode
            $zipArchiveMode = [System.IO.Compression.ZipArchiveMode]::Read
            $archive = New-Object -TypeName System.IO.Compression.ZipArchive -ArgumentList $archiveStream,$zipArchiveMode
            $entry = $archive.GetEntry("olddirectory/")
            $entry | Should -Not -BeNullOrEmpty

            $entry.LastWriteTime.Year | Should -BeExactly 1980
            $entry.LastWriteTime.Month| Should -BeExactly 1
            $entry.LastWriteTime.Day | Should -BeExactly 1
            $entry.LastWriteTime.Hour | Should -BeExactly 0
            $entry.LastWriteTime.Minute | Should -BeExactly 0
            $entry.LastWriteTime.Second | Should -BeExactly 0
            $entry.LastWriteTime.Millisecond | Should -BeExactly 0


            $archive.Dispose()
            $archiveStream.Dispose()
        }

        It "Writes a warning when compressing a file whose last write time is before 1980" {
            $sourcePath = "TestDrive:/OldFile.txt"
            $destinationPath = "${TestDrive}/archive13.zip"

            # Assert the last write time of the file is before 1980
            $dateProperty = Get-ItemPropertyValue -Path $sourcePath -Name "LastWriteTime"
            $dateProperty.Year | Should -BeLessThan 1980

            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WarningVariable warnings
            $warnings.Length | Should -Be 1
        }

        It "Writes a warning when compresing a directory whose last write time is before 1980" {
            $sourcePath = "TestDrive:/olddirectory"
            $destinationPath = "${TestDrive}/archive14.zip"

            # Assert the last write time of the file is before 1980
            $dateProperty = Get-ItemPropertyValue -Path $sourcePath -Name "LastWriteTime"
            $dateProperty.Year | Should -BeLessThan 1980

            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WarningVariable warnings
            $warnings.Length | Should -Be 1
        }
    }

    Context "Update tests" -Skip {
        
    }

    Context "DestinationPath and -WriteMode Overwrite tests" {
        BeforeAll {
            New-Item TestDrive:/SourceDir -Type Directory | Out-Null
    
            $content = "Some Data"
            $content | Out-File -FilePath TestDrive:/SourceDir/Sample-1.txt
            
            New-Item TestDrive:/archive3.zip -Type Directory | Out-Null

            New-Item TestDrive:/EmptyDirectory -Type Directory | Out-Null

            # Create a read-only archive
            $readOnlyArchivePath = "TestDrive:/readonly.zip"
            Compress-Archive -Path TestDrive:/SourceDir/Sample-1.txt -DestinationPath $readOnlyArchivePath
            Set-ItemProperty -Path $readOnlyArchivePath -Name IsReadOnly -Value $true

            # Create TestDrive:/archive.zip
            Compress-Archive -Path TestDrive:/SourceDir/Sample-1.txt -DestinationPath "TestDrive:/archive.zip"

            # Create Sample-2.txt
            $content | Out-File -FilePath TestDrive:/Sample-2.txt
        }

        It "Throws an error when archive file already exists and -Update and -Overwrite parameters are not specified" {
            $sourcePath = "TestDrive:/SourceDir"
            $destinationPath = "TestDrive:/archive1.zip"

            try
            {
                "Some Data" > $destinationPath
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                throw "Failed to validate that an archive file format $destinationPath already exists and -Update switch parameter is not specified."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "DestinationExists,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws a terminating error when archive file exists and -Update is specified but the archive is read-only" {
            $sourcePath = "TestDrive:/SourceDir"
            $destinationPath = "TestDrive:/readonly.zip"

            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Update
                throw "Failed to detect an that an error was thrown when archive $destinationPath already exists but it is read-only and -WriteMode Update is specified."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "ArchiveReadOnly,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws a terminating error when archive already exists as a directory and -Update and -Overwrite parameters are not specified" {
            $sourcePath = "TestDrive:/SourceDir/Sample-1.txt"
            $destinationPath = "TestDrive:/SourceDir"

            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect an error was thrown when archive $destinationPath exists as a directory and -WriteMode Update or -WriteMode Overwrite is not specified."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "DestinationExistsAsDirectory,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws a terminating error when DestinationPath is a directory and -Update is specified" {
            $sourcePath = "TestDrive:/SourceDir"
            $destinationPath = "TestDrive:/archive3.zip"

            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Update
                throw "Failed to validate that a directory $destinationPath exists and -Update switch parameter is specified."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "DestinationExistsAsDirectory,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws a terminating error when DestinationPath is a folder containing at least 1 item and Overwrite is specified" {
            $sourcePath = "TestDrive:/SourceDir"
            $destinationPath = "TestDrive:"

            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite
                throw "Failed to detect an error when $destinationPath is an existing directory containing at least 1 item and -Overwrite switch parameter is specified."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "DestinationIsNonEmptyDirectory,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws a terminating error when archive does not exist and -Update mode is specified" {
            $sourcePath = "TestDrive:/SourceDir"
            $destinationPath = "TestDrive:/archive2.zip"

            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Update
                throw "Failed to validate that an archive file format $destinationPath does not exist and -Update switch parameter is specified."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "ArchiveDoesNotExist,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        ## Overwrite tests
        It "Throws an error when trying to overwrite an empty directory, which is the working directory" {
            $sourcePath = "TestDrive:/Sample-2.txt"
            $destinationPath = "TestDrive:/EmptyDirectory"

            Push-Location $destinationPath

            try {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "CannotOverwriteWorkingDirectory,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }

            Pop-Location
        }

        It "Overwrites a directory containing no items when -Overwrite is specified" {
            $sourcePath = "TestDrive:/SourceDir"
            $destinationPath = "TestDrive:/EmptyDirectory"

            # Ensure $destinationPath is a directory
            Test-Path $destinationPath -PathType Container | Should -Be $true
            
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite

            # Ensure $destinationPath is now a file
            Test-Path $destinationPath -PathType Leaf | Should -Be $true
        }

        It "Overwrites an archive that already exists" {
            $destinationPath = "TestDrive:/archive.zip"

            # Ensure the original archive contains Sample-1.txt
            $destinationPath | Should -BeZipArchiveOnlyContaining @("Sample-1.txt") 

            # Overwrite the archive
            $sourcePath = "TestDrive:/Sample-2.txt"
            Compress-Archive -Path $sourcePath -DestinationPath "TestDrive:/archive.zip" -WriteMode Overwrite

            # Ensure the original entries and different than the new entries
            $destinationPath | Should -BeZipArchiveOnlyContaining @("Sample-2.txt") 
        }
    }

    Context "Relative Path tests" {
        BeforeAll {
            New-Item TestDrive:/SourceDir -Type Directory | Out-Null
            New-Item TestDrive:/SourceDir/ChildDir-1 -Type Directory | Out-Null
    
            $content = "Some Data"
            $content | Out-File -FilePath TestDrive:/SourceDir/Sample-1.txt
            $content | Out-File -FilePath TestDrive:/SourceDir/ChildDir-1/Sample-2.txt
        }

        # From 568
        It "Validate that relative path can be specified as Path parameter of Compress-Archive cmdlet" {
            $sourcePath = "./SourceDir"
            $destinationPath = "RelativePathForPathParameter.zip"
            try
            {
                Push-Location TestDrive:/
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	    Test-Path $destinationPath | Should -Be $true
            }
            finally
            {
                Pop-Location
            }
        }

        # From 582
        It "Validate that relative path can be specified as LiteralPath parameter of Compress-Archive cmdlet" {
            $sourcePath = "./SourceDir"
            $destinationPath = "RelativePathForLiteralPathParameter.zip"
            try
            {
                Push-Location TestDrive:/
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath
        	    Test-Path $destinationPath | Should -Be $true
            }
            finally
            {
                Pop-Location
            }
        }

        # From 596
        It "Validate that relative path can be specified as DestinationPath parameter of Compress-Archive cmdlet" -Tag this3 {
            $sourcePath = "TestDrive:/SourceDir"
            $destinationPath = "./RelativePathForDestinationPathParameter.zip"
            try
            {
                Push-Location TestDrive:/
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	    Test-Path $destinationPath | Should -Be $true
            }
            finally
            {
                Pop-Location
            }
        }
    }

    Context "Special and Wildcard Characters Tests" {
        BeforeAll {
            New-Item TestDrive:/SourceDir -Type Directory | Out-Null
    
            $content = "Some Data"
            $content | Out-File -FilePath TestDrive:/SourceDir/Sample-1.txt
            New-Item -LiteralPath "$TestDrive$($DS)Source[]Dir" -Type Directory | Out-Null
            $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)file1[].txt
        }

        It "Accepts DestinationPath parameter with wildcard characters that resolves to one path" {
            $sourcePath = "TestDrive:/SourceDir/Sample-1.txt"
            $destinationPath = "TestDrive:/Sample[]SingleFile.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	Test-Path -LiteralPath $destinationPath | Should -Be $true
            Remove-Item -LiteralPath $destinationPath
        }

        It "Accepts DestinationPath parameter with [ but no matching ]" {
            $sourcePath = "TestDrive:/SourceDir"
            $destinationPath = "TestDrive:/archive[2.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -BeZipArchiveOnlyContaining @("SourceDir/", "SourceDir/Sample-1.txt") -LiteralPath
            Remove-Item -LiteralPath $destinationPath
        }

        It "Accepts LiteralPath parameter for a directory with special characters in the directory name"  -skip:(($PSVersionTable.psversion.Major -lt 5) -and ($PSVersionTable.psversion.Minor -lt 0)) {
            $sourcePath = "$TestDrive$($DS)Source[]Dir"
            "Some Random Content" | Out-File -LiteralPath "$sourcePath$($DS)Sample[]File.txt"
            $destinationPath = "$TestDrive$($DS)archive3.zip"
            try
            {
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath
                $destinationPath | Should -Exist
            }
            finally
            {
                Remove-Item -LiteralPath $sourcePath -Force -Recurse
            }
        }

        It "Accepts LiteralPath parameter for a file with wildcards in the filename" {
            $sourcePath = "$TestDrive$($DS)file1[].txt"
            $destinationPath = "$TestDrive$($DS)archive4.zip"
            try
            {
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath
                $destinationPath | Should -Exist
            }
            finally
            {
                Remove-Item -LiteralPath $sourcePath -Force -Recurse
            }
        }
    }

    Context "PassThru tests" {
        BeforeAll {
            New-Item -Path TestDrive:/file.txt -ItemType File
        }

        It "Returns an object of type System.IO.FileInfo when PassThru is specified" {
            $output = Compress-Archive -Path TestDrive:/file.txt -DestinationPath TestDrive:/archive1.zip -PassThru
            $output | Should -BeOfType System.IO.FileInfo
            $destinationPath = Join-Path $TestDrive "archive1.zip"
            $output.FullName | Should -Be $destinationPath
        }

        It "Does not return an object when PassThru is not specified" {            
            $output = Compress-Archive -Path TestDrive:/file.txt -DestinationPath TestDrive:/archive2.zip
            $output | Should -BeNullOrEmpty
        }

        It "Does not return an object when PassThru is false" {            
            $output = Compress-Archive -Path TestDrive:/file.txt -DestinationPath TestDrive:/archive3.zip -PassThru:$false
            $output | Should -BeNullOrEmpty
        }
    }

    Context "File permissions, attributes, etc. tests" {
        BeforeAll {
            New-Item TestDrive:/file.txt -ItemType File
            "Hello, World!" | Out-File -Path TestDrive:/file.txt
        }


        It "Skips archiving a file in use" {
            $fileMode = [System.IO.FileMode]::Open
            $fileAccess = [System.IO.FileAccess]::Write
            $fileShare = [System.IO.FileShare]::None
            $archiveInUseStream = New-Object -TypeName "System.IO.FileStream" -ArgumentList "${TestDrive}/file.txt",$fileMode,$fileAccess,$fileShare

            Compress-Archive -Path TestDrive:/file.txt -DestinationPath TestDrive:/archive_in_use.zip -ErrorAction SilentlyContinue
            # Ensure it creates an empty zip archive
            "TestDrive:/archive_in_use.zip" | Should -BeZipArchiveOnlyContaining @()

            $archiveInUseStream.Dispose()
        }
    }
}
