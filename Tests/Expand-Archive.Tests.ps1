# Tests for Expand-Archive

Describe("Expand-Archive Tests") {
    BeforeAll {
        $CmdletClassName = "Microsoft.PowerShell.Archive.ExpandArchiveCommand"

        # Progress perference
        $originalProgressPref = $ProgressPreference
        $ProgressPreference = "SilentlyContinue"

        function Add-FileExtensionBasedOnFormat {
            Param (
                [string] $Path,
                [string] $Format
            )

            if ($Format -eq "Zip") {
                return $Path += ".zip"
            }
            if ($Format -eq "Tar") {
                return $Path += ".tar"
            }
            throw "Format type is not supported"
        }
    }

    AfterAll {
        $global:ProgressPreference = $originalProgressPref
    }

    Context "Parameter set validation tests" {
        BeforeAll {
            # Set up files for tests
            New-Item TestDrive:/SourceDir -Type Directory | Out-Null
            $content = "Some Data"
            $content | Out-File -FilePath TestDrive:/Sample-1.txt

            # Create archives called archive1.zip and archive2.zip
            Compress-Archive -Path TestDrive:/Sample-1.txt -DestinationPath TestDrive:/archive1.zip
            Compress-Archive -Path TestDrive:/Sample-1.txt -DestinationPath TestDrive:/archive2.zip
        }


        It "Validate errors with NULL & EMPTY values for Path, LiteralPath, and DestinationPath" -ForEach @(
            @{ Path = $null; DestinationPath = "TestDrive:/destination" }
            @{ Path = "TestDrive:/archive1.zip"; DestinationPath = $null }
            @{ Path = $null; DestinationPath = $null }
            @{ Path = ""; DestinationPath = "TestDrive:/destination" }
            @{ Path = "TestDrive:/archive1.zip"; DestinationPath = "" }
            @{ Path = ""; DestinationPath = "" }
        ) {

            try
            {
                Expand-Archive -Path $Path -DestinationPath $DestinationPath
                throw "ValidateNotNullOrEmpty attribute is missing on one of parameters belonging to Path parameterset."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "ParameterArgumentValidationError,$CmdletClassName"
            }

            try
            {
                Expand-Archive -LiteralPath $Path -DestinationPath $DestinationPath
                throw "ValidateNotNullOrEmpty attribute is missing on one of parameters belonging to LiteralPath parameterset."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "ParameterArgumentValidationError,$CmdletClassName"
            }
        }

        It "Throws when non-existing path is supplied for Path or LiteralPath parameters" {
            $path = "TestDrive:/non-existant.zip"
            $destinationPath = "TestDrive:($DS)DestinationFolder"
            try
            {
                Expand-Archive -Path $path -DestinationPath $destinationPath
                throw "Failed to validate that an invalid Path $invalidPath was supplied as input to Expand-Archive cmdlet."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "PathNotFound,$CmdletClassName"
            }

            try
            {
                Expand-Archive -LiteralPath $path -DestinationPath $destinationPath
                throw "Failed to validate that an invalid LiteralPath $invalidPath was supplied as input to Expand-Archive cmdlet."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "PathNotFound,$CmdletClassName"
            }
        }

        It "Throws when path non-filesystem path is supplied for Path or LiteralPath parameters" {
            $path = "Variable:/PWD"
            $destinationPath = "TestDrive:($DS)DestinationFolder"
            try
            {
                Expand-Archive -Path $path -DestinationPath $destinationPath
                throw "Failed to validate that an invalid Path $invalidPath was supplied as input to Expand-Archive cmdlet."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "InvalidPath,$CmdletClassName"
            }

            try
            {
                Expand-Archive -LiteralPath $path -DestinationPath $destinationPath
                throw "Failed to validate that an invalid LiteralPath $invalidPath was supplied as input to Expand-Archive cmdlet."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "InvalidPath,$CmdletClassName"
            }
        }

        It "Throws an error when multiple paths are supplied as input to Path parameter" {
            $sourcePath = @(
                "TestDrive:/SourceDir/archive1.zip",
                "TestDrive:/SourceDir/archive2.zip")
            $destinationPath = "TestDrive:/DestinationFolder"

            try
            {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect that duplicate Path $sourcePath is supplied as input to Path parameter."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "CannotConvertArgument,$CmdletClassName"
            }
        }

        It "Throws an error when multiple paths are supplied as input to LiteralPath parameter" {
            $sourcePath = @(
                "TestDrive:/SourceDir/archive1.zip",
                "TestDrive:/SourceDir/archive2.zip")
            $destinationPath = "TestDrive:/DestinationFolder"

            try
            {
                Expand-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect that duplicate Path $sourcePath is supplied as input to LiteralPath parameter."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "CannotConvertArgument,$CmdletClassName"
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

        It "Throws an error when Path and DestinationPath are the same and -WriteMode Overwrite is specified" {
            $sourcePath = "TestDrive:/archive1.zip"
            $destinationPath = $sourcePath

            try {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite
                throw "Failed to detect an error when Path and DestinationPath are the same and -Overwrite is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SamePathAndDestinationPath,$CmdletClassName"
            }
        }

        It "Throws an error when LiteralPath and DestinationPath are the same and WriteMode -Overwrite is specified" {
            $sourcePath = "TestDrive:/archive1.zip"
            $destinationPath = $sourcePath

            try {
                Expand-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite
                throw "Failed to detect an error when LiteralPath and DestinationPath are the same and -Overwrite is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SameLiteralPathAndDestinationPath,$CmdletClassName"
            }
        }

        It "Throws an error when an invalid path is supplied to DestinationPath" {
            $sourcePath = "TestDrive:/archive1.zip"
            $destinationPath = "Variable:/PWD"
            
            try {
                Expand-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect an error when an invalid path is supplied to DestinationPath"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "InvalidPath,$CmdletClassName"
            }
        }
    }

    Context "DestinationPath and Overwrite Tests" {
        BeforeAll {
            New-Item -Path "TestDrive:/file1.txt" -ItemType File
            "Hello, World!" | Out-File -FilePath "TestDrive:/file1.txt"
            Compress-Archive -Path "TestDrive:/file1.txt" -DestinationPath "TestDrive:/archive1.zip"

            New-Item -Path "TestDrive:/directory1" -ItemType Directory

            # Create archive2.zip containing directory1
            Compress-Archive -Path "TestDrive:/directory1" -DestinationPath "TestDrive:/archive2.zip"

            New-Item -Path "TestDrive:/ParentDir" -ItemType Directory
            New-Item -Path "TestDrive:/ParentDir/file1.txt" -ItemType Directory

            # Create a dir that is a container for items to be overwritten
            New-Item -Path "TestDrive:/ItemsToOverwriteContainer" -ItemType Directory
            New-Item -Path "TestDrive:/ItemsToOverwriteContainer/file2" -ItemType File
            New-Item -Path "TestDrive:/ItemsToOverwriteContainer/subdir1" -ItemType Directory
            New-Item -Path "TestDrive:/ItemsToOverwriteContainer/subdir1/file1.txt" -ItemType File
            New-Item -Path "TestDrive:/ItemsToOverwriteContainer/subdir2" -ItemType Directory
            New-Item -Path "TestDrive:/ItemsToOverwriteContainer/subdir2/file1.txt" -ItemType Directory
            New-Item -Path "TestDrive:/ItemsToOverwriteContainer/subdir4" -ItemType Directory
            New-Item -Path "TestDrive:/ItemsToOverwriteContainer/subdir4/file1.txt" -ItemType Directory
            New-Item -Path "TestDrive:/ItemsToOverwriteContainer/subdir4/file1.txt/somefile" -ItemType File

            # Create directory to override
            New-Item -Path "TestDrive:/ItemsToOverwriteContainer/subdir3" -ItemType Directory
            New-Item -Path "TestDrive:/ItemsToOverwriteContainer/subdir3/directory1" -ItemType File

            # Set the error action preference so non-terminating errors aren't displayed
            $ErrorActionPreference = 'SilentlyContinue'
        }

        AfterAll {
            # Reset to default value
            $ErrorActionPreference = 'Continue'
        }

        It "Throws an error when DestinationPath is an existing file" -Tag debug2 {
            $sourcePath = "TestDrive:/archive1.zip"
            $destinationPath = "TestDrive:/file1.txt"

            try {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "DestinationExists,$CmdletClassName"
            }
        }

        It "Does not throw an error when a directory in the archive has the same destination path as an existing directory" {
            $sourcePath = "TestDrive:/archive2.zip"
            $destinationPath = "TestDrive:"

            try {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -ErrorAction Stop
            } catch {
                throw "An error was thrown but an error was not expected"
            }
        }

        It "Writes a non-terminating error when a file in the archive has a destination path that already exists" {
            $sourcePath = "TestDrive:/archive1.zip"
            $destinationPath = "TestDrive:"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -ErrorVariable error
            $error.Count | Should -Be 1
            $error[0].FullyQualifiedErrorId | Should -Be "DestinationExists,$CmdletClassName"
        }

        It "Writes a non-terminating error when a file in the archive has a destination path that is an existing directory containing at least 1 item and -WriteMode Overwrite is specified" {
            $sourcePath = "TestDrive:/archive1.zip"
            $destinationPath = "TestDrive:/ItemsToOverwriteContainer/subdir4"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 1
            $error[0].FullyQualifiedErrorId | Should -Be "DestinationIsNonEmptyDirectory,$CmdletClassName"
        }

        It "Writes a non-terminating error when a file in the archive has a destination path that is the working directory and -WriteMode Overwrite is specified" {
            $sourcePath = "TestDrive:/archive1.zip"
            $destinationPath = "TestDrive:/ParentDir"

            Push-Location "$destinationPath/file1.txt"

            try {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
                $error.Count | Should -Be 1
                $error[0].FullyQualifiedErrorId | Should -Be "CannotOverwriteWorkingDirectory,$CmdletClassName"
            } finally {
                Pop-Location
            }
        }

        It "Overwrites a file when it is DestinationPath and -WriteMode Overwrite is specified" {
            $sourcePath = "TestDrive:/archive1.zip"
            $destinationPath = "TestDrive:/ItemsToOverwriteContainer/file2"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 0

            # Ensure the file in archive1.zip was expanded
            Test-Path "TestDrive:/ItemsToOverwriteContainer/file2/file1.txt" -PathType Leaf
        }

        It "Overwrites a file whose path is the same as the destination path of a file in the archive when -WriteMode Overwrite is specified" -Tag td {
            $sourcePath = "TestDrive:/archive1.zip"
            $destinationPath = "TestDrive:/ItemsToOverwriteContainer/subdir1"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 0

            # Ensure the file in archive1.zip was expanded
            Test-Path "TestDrive:/ItemsToOverwriteContainer/subdir1/file1.txt" -PathType Leaf

            # Ensure the contents of file1.txt is "Hello, World!"
            Get-Content -Path "TestDrive:/ItemsToOverwriteContainer/subdir1/file1.txt" | Should -Be "Hello, World!"
        }

        It "Overwrites a directory whose path is the same as the destination path of a file in the archive when -WriteMode Overwrite is specified" {
            $sourcePath = "TestDrive:/archive1.zip"
            $destinationPath = "TestDrive:/ItemsToOverwriteContainer/subdir2"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 0

            # Ensure the file in archive1.zip was expanded
            Test-Path "TestDrive:/ItemsToOverwriteContainer/subdir2/file1.txt" -PathType Leaf

            # Ensure the contents of file1.txt is "Hello, World!"
            Get-Content -Path "TestDrive:/ItemsToOverwriteContainer/subdir2/file1.txt" | Should -Be "Hello, World!"
        }

        It "Overwrites a file whose path is the same as the destination path of a directory in the archive when -WriteMode Overwrite is specified" {
            $sourcePath = "TestDrive:/archive2.zip"
            $destinationPath = "TestDrive:/ItemsToOverwriteContainer/subdir3"
            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 0

            # Ensure the file in archive1.zip was expanded
            Test-Path "TestDrive:/ItemsToOverwriteContainer/subdir3/directory1" -PathType Container
        }
    }

    Context "Basic functionality tests"  -ForEach @(
        @{Format = "Zip"},
        @{Format = "Tar"}
    ) {
        # extract to a directory works
        # extract to working directory works when DestinationPath is specified
        # expand archive works when -DestinationPath is not specified (and a single top level item which is a directory)
        # expand archive works when -DestinationPath is not specified (and there are mutiple top level items)

        BeforeAll {
            New-Item -Path "TestDrive:/file1.txt" -ItemType File
            "Hello, World!" | Out-File -FilePath "TestDrive:/file1.txt"
            Compress-Archive -Path "TestDrive:/file1.txt" -DestinationPath (Add-FileExtensionBasedOnFormat "TestDrive:/archive1" -Format $Format)

            New-Item -Path "TestDrive:/directory2" -ItemType Directory
            New-Item -Path "TestDrive:/directory3" -ItemType Directory
            New-Item -Path "TestDrive:/directory4" -ItemType Directory
            New-Item -Path "TestDrive:/directory5" -ItemType Directory
            New-Item -Path "TestDrive:/directory6" -ItemType Directory

            New-Item -Path "TestDrive:/DirectoryToArchive" -ItemType Directory
            Compress-Archive -Path "TestDrive:/DirectoryToArchive" -DestinationPath (Add-FileExtensionBasedOnFormat "TestDrive:/archive2" -Format $Format)

            # Create an archive containing a file and an empty folder
            Compress-Archive -Path "TestDrive:/file1.txt","TestDrive:/DirectoryToArchive" -DestinationPath "TestDrive:/archive3"
        }

        It "Expands an archive when a non-existent directory is specified as -DestinationPath with format <Format>" {
            $sourcePath = Add-FileExtensionBasedOnFormat "TestDrive:/archive1" -Format $Format
            $destinationPath = "TestDrive:/directory1"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -Format $Format

            $itemsInDestinationPath = Get-ChildItem $destinationPath -Recurse
            $itemsInDestinationPath.Count | Should -Be 1
            $itemsInDestinationPath[0].Name | Should -Be "file1.txt"
        }

        It "Expands an archive when DestinationPath is an existing directory" {
            $sourcePath = Add-FileExtensionBasedOnFormat "TestDrive:/archive1" -Format $Format
            $destinationPath = "TestDrive:/directory2"

            try {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -ErrorAction Stop
            } catch {
                throw "An error was thrown but an error was not expected"
            }
        }

        It "Expands an archive to the working directory when it is specified as -DestinationPath" {
            $sourcePath = Add-FileExtensionBasedOnFormat "TestDrive:/archive1" -Format $Format
            $destinationPath = "TestDrive:/directory3"

            Push-Location $destinationPath

            Expand-Archive -Path $sourcePath -DestinationPath $PWD

            $itemsInDestinationPath = Get-ChildItem $PWD -Recurse
            $itemsInDestinationPath.Count | Should -Be 1
            $itemsInDestinationPath[0].Name | Should -Be "file1.txt"

            Pop-Location
        }

        It "Expands an archive to a directory with that archive's name when -DestinationPath is not specified" {
            $sourcePath = Add-FileExtensionBasedOnFormat "TestDrive:/archive2" -Format $Format
            $destinationPath = "TestDrive:/directory4"

            Push-Location $destinationPath

            Expand-Archive -Path $sourcePath

            $itemsInDestinationPath = Get-ChildItem $destinationPath -Recurse
            $itemsInDestinationPath.Count | Should -Be 2

            $directoryContents = @()
            $directoryContents += $itemsInDestinationPath[0].FullName
            $directoryContents += $itemsInDestinationPath[1].FullName

            $directoryContents | Should -Contain (Join-Path $TestDrive "directory4/archive2")
            $directoryContents | Should -Contain (Join-Path $TestDrive "directory4/archive2/DirectoryToArchive")

            Pop-Location
        }

        It "Throws an error when expanding an archive whose name does not have an extension and -DestinationPath is not specified" {
            Push-Location  "TestDrive:/"
            try {
                Expand-Archive -Path "TestDrive:/archive3"
            }
            catch {
                $_.FullyQualifiedErrorId | Should -Be "CannotDetermineDestinationPath,${CmdletClassName}"
            }
            finally {
                Pop-Location
            }
        }

        It "Expands an archive containing multiple files, non-empty directories, and empty directories" {
            
            # Create an archive containing multiple files, non-empty directories, and empty directories
            New-Item -Path "TestDrive:/file2.txt" -ItemType File
            "Hello, World!" | Out-File -FilePath "TestDrive:/file2.txt"
            New-Item -Path "TestDrive:/file3.txt" -ItemType File
            "Hello, World!" | Out-File -FilePath "TestDrive:/file3.txt"

            New-Item -Path "TestDrive:/emptydirectory1" -ItemType Directory
            New-Item -Path "TestDrive:/emptydirectory2" -ItemType Directory

            New-Item -Path "TestDrive:/nonemptydirectory1" -ItemType Directory
            New-Item -Path "TestDrive:/nonemptydirectory2" -ItemType Directory

            New-Item -Path "TestDrive:/nonemptydirectory1/subfile1.txt" -ItemType File
            New-Item -Path "TestDrive:/nonemptydirectory2/subemptydirectory1" -ItemType Directory

            $archive4Paths = @("TestDrive:/file2.txt", "TestDrive:/file3.txt", "TestDrive:/emptydirectory1", "TestDrive:/emptydirectory2", "TestDrive:/nonemptydirectory1", "TestDrive:/nonemptydirectory2")

            $sourcePath = Add-FileExtensionBasedOnFormat "TestDrive:/archive4" -Format $Format
            Compress-Archive -Path $archive4Paths -DestinationPath $sourcePath -Format $Format

            
            $destinationPath = "TestDrive:/directory6"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -Format $Format

            $expandedItems = Get-ChildItem $destinationPath -Recurse -Name

            $itemsInArchive = @("file2.txt", "file3.txt", "emptydirectory1", "emptydirectory2", "nonemptydirectory1", "nonemptydirectory2", (Join-Path "nonemptydirectory1" "subfile1.txt"), (Join-Path "nonemptydirectory2" "subemptydirectory1"))

            $expandedItems.Length | Should -Be $itemsInArchive.Count
            foreach ($item in $itemsInArchive) {
                $item | Should -BeIn $expandedItems
            }
        }

        It "Expands an archive containing a file whose LastWriteTime is in the past" {
            New-Item -Path "TestDrive:/oldfile.txt" -ItemType File
            Set-ItemProperty -Path "TestDrive:/oldfile.txt" -Name "LastWriteTime" -Value '2003-01-16 14:44'
            $sourcePath = Add-FileExtensionBasedOnFormat "TestDrive:/archive_oldfile" -Format $Format
            Compress-Archive -Path "TestDrive:/oldfile.txt" -DestinationPath $sourcePath -Format $Format

            
            $destinationPath = "TestDrive:/destination7"
            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -Format $Format

            $lastWriteTime = Get-ItemPropertyValue -Path (Join-Path $destinationPath "oldfile.txt") -Name "LastWriteTime"

            $lastWriteTime.Year | Should -Be 2003
            $lastWriteTime.Month | Should -Be 1
            $lastWriteTime.Day | Should -Be 16
            $lastWriteTime.Hour | Should -Be 14
            $lastWriteTime.Minute | Should -Be 44
            $lastWriteTime.Second | Should -Be 0
            $lastWriteTime.Millisecond | Should -Be 0
        }

        It "Expands an archive containing a directory whose LastWriteTime is in the past" {
            New-Item -Path "TestDrive:/olddirectory" -ItemType Directory
            Set-ItemProperty -Path "TestDrive:/olddirectory" -Name "LastWriteTime" -Value '2003-01-16 14:44'

            $sourcePath = Add-FileExtensionBasedOnFormat "TestDrive:/archive_olddirectory" -Format $Format
            Compress-Archive -Path "TestDrive:/olddirectory" -DestinationPath $sourcePath -Format $Format

            
            $destinationPath = "TestDrive:/destination_olddirectory"
            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -Format $Format

            $lastWriteTime = Get-ItemPropertyValue -Path "TestDrive:/destination_olddirectory/olddirectory" -Name "LastWriteTime"

            $lastWriteTime.Year | Should -Be 2003
            $lastWriteTime.Month | Should -Be 1
            $lastWriteTime.Day | Should -Be 16
            $lastWriteTime.Hour | Should -Be 14
            $lastWriteTime.Minute | Should -Be 44
            $lastWriteTime.Second | Should -Be 0
            $lastWriteTime.Millisecond | Should -Be 0
        }
    }

    Context "PassThru tests" {
        BeforeAll {
            New-Item -Path TestDrive:/file1.txt -ItemType File
            "Hello, World!" | Out-File -Path TestDrive:/file1.txt
            $archivePath = "TestDrive:/archive.zip"
            Compress-Archive -Path TestDrive:/file1.txt -DestinationPath $archivePath
        }

        It "Returns a System.IO.DirectoryInfo object when PassThru is specified" {
            $destinationPath = "TestDrive:/archive_contents"
            $output = Expand-Archive -Path $archivePath -DestinationPath $destinationPath -PassThru
            $output | Should -BeOfType System.IO.DirectoryInfo
            $output.FullName | Should -Be (Convert-Path $destinationPath)
        }

        It "Does not return an object when PassThru is not specified" {            
            $output = Expand-Archive -Path $archivePath -DestinationPath TestDrive:/archive_contents2
            $output | Should -BeNullOrEmpty
        }

        It "Does not return an object when PassThru is false" {            
            $output = Expand-Archive -Path $archivePath -DestinationPath TestDrive:/archive_contents3 -PassThru:$false
            $output | Should -BeNullOrEmpty
        }
    }

    Context "Special and Wildcard Character Tests" {
        BeforeAll {
            New-Item TestDrive:/file.txt -ItemType File
            "Hello, World!" | Out-File -Path TestDrive:/file.txt

            Compress-Archive -Path TestDrive:/file.txt -DestinationPath TestDrive:/archive_containing_file.zip
            Compress-Archive -Path TestDrive:/file.txt -DestinationPath TestDrive:/archive_with_number_1.zip
            Compress-Archive -Path TestDrive:/file.txt -DestinationPath TestDrive:/archive_with_number_2.zip
            Compress-Archive -Path TestDrive:/file.txt -DestinationPath TestDrive:/archive_with_[.zip
        }

        AfterAll {
            Remove-Item -LiteralPath "TestDrive:/archive_with_[.zip"
        }

        It "Expands an archive when -Path contains wildcard character and resolves to 1 path" {
            Expand-Archive -Path TestDrive:/archive_containing* -DestinationPath TestDrive:/destination1
            (Convert-Path TestDrive:/destination1/file.txt) | Should -Exist
        }

        It "Throws a terminating error when archive when -Path contains wildcard character and resolves to multiple paths" {
            try {
                Expand-Archive -Path TestDrive:/archive_with* -DestinationPath TestDrive:/destination2
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "PathResolvedToMultiplePaths,$CmdletClassName"
            }
        }

        It "Expands an archive when -LiteralPath contains [ but no matching ]" {
            Expand-Archive -LiteralPath TestDrive:/archive_with_[.zip -DestinationPath TestDrive:/destination3
            (Convert-Path TestDrive:/destination3/file.txt) | Should -Exist
        }

        It "Expands an archive when -DestinationPath contains [ but no matching ]" {
            Expand-Archive -Path TestDrive:/archive_containing_file.zip -DestinationPath TestDrive:/destination[
            Test-Path -LiteralPath TestDrive:/destination[/file.txt | Should -Be $true
            Remove-Item -LiteralPath "${TestDrive}/destination[" -Recurse
        }
    }

    Context "File permssions, attributes, etc tests" -Tag td2 {
        BeforeAll {
            New-Item TestDrive:/file.txt -ItemType File
            "Hello, World!" | Out-File -Path TestDrive:/file.txt

            # Create a readonly archive
            Compress-Archive -Path TestDrive:/file.txt -DestinationPath TestDrive:/readonly.zip
            Set-ItemProperty -Path TestDrive:/readonly.zip -Name "IsReadOnly" -Value $true

            # Create an archive in-use
            Compress-Archive -Path TestDrive:/file.txt -DestinationPath TestDrive:/archive_in_use.zip
            $fileMode = [System.IO.FileMode]::Open
            $fileAccess = [System.IO.FileAccess]::Read
            $fileShare = [System.IO.FileShare]::Read
            $archiveInUseStream = New-Object -TypeName "System.IO.FileStream" -ArgumentList "${TestDrive}/archive_in_use.zip",$fileMode,$fileAccess,$fileShare

            [console]::InputEncoding = [console]::OutputEncoding = New-Object System.Text.UTF8Encoding
            # Create an archive containing an entry with non-latin characters
            New-Item TestDrive:/ملف -ItemType File
            "Hello, World!" | Out-File -Path TestDrive:/ملف
            $archiveWithNonLatinEntryPath = Join-Path $TestDrive "archive_with_nonlatin_entry.zip"
            if ($IsWindows) {
                7z.exe a $archiveWithNonLatinEntryPath (Join-Path $TestDrive ملف)
            } else {
                7z a $archiveWithNonLatinEntryPath (Join-Path $TestDrive ملف)
            }
            
         }

         AfterAll {
            $archiveInUseStream.Dispose()
         }
        
        It "Expands a read-only archive" {
            Expand-Archive -Path TestDrive:/readonly.zip -DestinationPath TestDrive:/readonly_output
            "TestDrive:/readonly_output/file.txt" | Should -Exist
        }

        It "Expands an archive in-use" {
            Expand-Archive -Path TestDrive:/archive_in_use.zip -DestinationPath TestDrive:/archive_in_use_output
            "TestDrive:/archive_in_use_output/file.txt" | Should -Exist
        }

        It "Expands an archive containing an entry with non-latin characters" {
            Expand-Archive -Path $archiveWithNonLatinEntryPath -DestinationPath TestDrive:/archive_with_nonlatin_entry_output
            "TestDrive:/archive_with_nonlatin_entry_output/ملف" | Should -Exist
        }
    }

    Context "Large File Tests" {

    }
}