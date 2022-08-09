# Tests for Expand-Archive

Describe("Expand-Archive Tests") {
    BeforeAll {
        $CmdletClassName = "Microsoft.PowerShell.Archive.ExpandArchiveCommand"

        # Progress perference
        $originalProgressPref = $ProgressPreference
        $ProgressPreference = "SilentlyContinue"
    }

    AfterAll {
        $global:ProgressPreference = $originalProgressPref
    }

    Context "Parameter set validation tests" {
        BeforeAll {
            function ExpandArchivePathParameterSetValidator {
                param
                (
                    [string] $path,
                    [string] $destinationPath
                )
        
                try
                {
                    Expand-Archive -Path $path -DestinationPath $destinationPath
                    throw "ValidateNotNullOrEmpty attribute is missing on one of parameters belonging to Path parameterset."
                }
                catch
                {
                    $_.FullyQualifiedErrorId | Should -Be "ParameterArgumentValidationError,$CmdletClassName"
                }
            }
        
            function ExpandArchiveLiteralPathParameterSetValidator {
                param
                (
                    [string] $literalPath,
                    [string] $destinationPath
                )
        
                try
                {
                    Expand-Archive -LiteralPath $literalPath -DestinationPath $destinationPath
                    throw "ValidateNotNullOrEmpty attribute is missing on one of parameters belonging to LiteralPath parameterset."
                }
                catch
                {
                    $_.FullyQualifiedErrorId | Should -Be "ParameterArgumentValidationError,$CmdletClassName"
                }
            }
            
            # Set up files for tests
            New-Item $TestDrive/SourceDir -Type Directory | Out-Null
            $content = "Some Data"
            $content | Out-File -FilePath $TestDrive/Sample-1.txt

            # Create archives called archive1.zip and archive2.zip
            Compress-Archive -Path $TestDrive/Sample-1.txt -DestinationPath $TestDrive/archive1.zip
            Compress-Archive -Path $TestDrive/Sample-1.txt -DestinationPath $TestDrive/archive2.zip
        }


        It "Validate errors with NULL & EMPTY values for Path, LiteralPath, and DestinationPath" {
            $sourcePath = "$TestDrive/SourceDir"
            $destinationPath = "$TestDrive/SampleSingleFile.zip"

            ExpandArchivePathParameterSetValidator $null $destinationPath
            ExpandArchivePathParameterSetValidator $sourcePath $null
            ExpandArchivePathParameterSetValidator $null $null

            ExpandArchivePathParameterSetValidator "" $destinationPath
            ExpandArchivePathParameterSetValidator $sourcePath ""
            ExpandArchivePathParameterSetValidator "" ""

            ExpandArchiveLiteralPathParameterSetValidator $null $destinationPath
            ExpandArchiveLiteralPathParameterSetValidator $sourcePath $null
            ExpandArchiveLiteralPathParameterSetValidator $null $null

            ExpandArchiveLiteralPathParameterSetValidator "" $destinationPath
            ExpandArchiveLiteralPathParameterSetValidator $sourcePath ""
            ExpandArchiveLiteralPathParameterSetValidator "" ""
        }

        It "Throws when non-existing path is supplied for Path or LiteralPath parameters" {
            $path = "$TestDrive/non-existant.zip"
            $destinationPath = "$TestDrive($DS)DestinationFolder"
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

        It "Throws when invalid path non-filesystem path is supplied for Path or LiteralPath parameters" {
            $path = "Variable:DS"
            $destinationPath = "$TestDrive($DS)DestinationFolder"
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
                "$TestDrive/SourceDir/archive1.zip",
                "$TestDrive/SourceDir/archive2.zip")
            $destinationPath = "$TestDrive/DestinationFolder"

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
                "$TestDrive/SourceDir/archive1.zip",
                "$TestDrive/SourceDir/archive2.zip")
            $destinationPath = "$TestDrive/DestinationFolder"

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
            $destinationPath = "$TestDrive/SampleFromSystemDrive.zip"
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
            $sourcePath = "$TestDrive/archive1.zip"
            $destinationPath = $sourcePath

            try {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite
                throw "Failed to detect an error when Path and DestinationPath are the same and -Overwrite is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SamePathAndDestinationPath,$CmdletClassName"
            }
        }

        It "Throws an error when LiteralPath and DestinationPath are the same and WriteMode -Overwrite is specified" {
            $sourcePath = "$TestDrive/archive1.zip"
            $destinationPath = $sourcePath

            try {
                Expand-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite
                throw "Failed to detect an error when LiteralPath and DestinationPath are the same and -Overwrite is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SameLiteralPathAndDestinationPath,$CmdletClassName"
            }
        }

        It "Throws an error when an invalid path is supplied to DestinationPath" {
            $sourcePath = "$TestDrive/archive1.zip"
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
        # error when destination path is a file and overwrite is not specified
        # error when output has same name as existant file and overwrite is not specified

        # no error when destination path is existing folder
        # no error when output is folder 

        # output is directory w/ at least 1 item
        # output has same name as current working directory

        # overwrite file works
        # overwrite output file works done
        #  overwrite file w/file done
        # overwrite output file w/directory
        # overwrite directory w/file
        # overwrite non-existant path works

        # last write times

        BeforeAll {
            New-Item -Path "$TestDrive/file1.txt" -ItemType File
            "Hello, World!" | Out-File -FilePath "$TestDrive/file1.txt"
            Compress-Archive -Path "$TestDrive/file1.txt" -DestinationPath "$TestDrive/archive1.zip"

            New-Item -Path "$TestDrive/directory1" -ItemType Directory

            # Create archive2.zip containing directory1
            Compress-Archive -Path "$TestDrive/directory1" -DestinationPath "$TestDrive/archive2.zip"

            New-Item -Path "$TestDrive/ParentDir" -ItemType Directory
            New-Item -Path "$TestDrive/ParentDir/file1.txt" -ItemType Directory

            # Create a dir that is a container for items to be overwritten
            New-Item -Path "$TestDrive/ItemsToOverwriteContainer" -ItemType Directory
            New-Item -Path "$TestDrive/ItemsToOverwriteContainer/file2" -ItemType File
            New-Item -Path "$TestDrive/ItemsToOverwriteContainer/subdir1" -ItemType Directory
            New-Item -Path "$TestDrive/ItemsToOverwriteContainer/subdir1/file1.txt" -ItemType File
            New-Item -Path "$TestDrive/ItemsToOverwriteContainer/subdir2" -ItemType Directory
            New-Item -Path "$TestDrive/ItemsToOverwriteContainer/subdir2/file1.txt" -ItemType Directory
            New-Item -Path "$TestDrive/ItemsToOverwriteContainer/subdir4" -ItemType Directory
            New-Item -Path "$TestDrive/ItemsToOverwriteContainer/subdir4/file1.txt" -ItemType Directory
            New-Item -Path "$TestDrive/ItemsToOverwriteContainer/subdir4/file1.txt/somefile" -ItemType File

            # Create directory to override
            New-Item -Path "$TestDrive/ItemsToOverwriteContainer/subdir3" -ItemType Directory
            New-Item -Path "$TestDrive/ItemsToOverwriteContainer/subdir3/directory1" -ItemType File

            # Set the error action preference so non-terminating errors aren't displayed
            $ErrorActionPreference = 'SilentlyContinue'
        }

        AfterAll {
            # Reset to default value
            $ErrorActionPreference = 'Continue'
        }

        It "Throws an error when DestinationPath is an existing file" {
            $sourcePath = "$TestDrive/archive1.zip"
            $destinationPath = "$TestDrive/file1.txt"

            try {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "DestinationExists,$CmdletClassName"
            }
        }

        It "Does not throw an error when a directory in the archive has the same destination path as an existing directory" {
            $sourcePath = "$TestDrive/archive2.zip"
            $destinationPath = "$TestDrive"

            try {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -ErrorAction Stop
            } catch {
                throw "An error was thrown but an error was not expected"
            }
        }

        It "Writes a non-terminating error when a file in the archive has a destination path that already exists" {
            $sourcePath = "$TestDrive/archive1.zip"
            $destinationPath = "$TestDrive"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -ErrorVariable error
            $error.Count | Should -Be 1
            $error[0].FullyQualifiedErrorId | Should -Be "DestinationExists,$CmdletClassName"
        }

        It "Writes a non-terminating error when a file in the archive has a destination path that is an existing directory containing at least 1 item and -WriteMode Overwrite is specified" {
            $sourcePath = "$TestDrive/archive1.zip"
            $destinationPath = "$TestDrive/ItemsToOverwriteContainer/subdir4"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 1
            $error[0].FullyQualifiedErrorId | Should -Be "DestinationIsNonEmptyDirectory,$CmdletClassName"
        }

        It "Writes a non-terminating error when a file in the archive has a destination path that is the working directory and -WriteMode Overwrite is specified" {
            $sourcePath = "$TestDrive/archive1.zip"
            $destinationPath = "$TestDrive/ParentDir"

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
            $sourcePath = "$TestDrive/archive1.zip"
            $destinationPath = "$TestDrive/ItemsToOverwriteContainer/file2"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 0

            # Ensure the file in archive1.zip was expanded
            Test-Path "$TestDrive/ItemsToOverwriteContainer/file2/file1.txt" -PathType Leaf
        }

        It "Overwrites a file whose path is the same as the destination path of a file in the archive when -WriteMode Overwrite is specified" -Tag td {
            $sourcePath = "$TestDrive/archive1.zip"
            $destinationPath = "$TestDrive/ItemsToOverwriteContainer/subdir1"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 0

            # Ensure the file in archive1.zip was expanded
            Test-Path "$TestDrive/ItemsToOverwriteContainer/subdir1/file1.txt" -PathType Leaf

            # Ensure the contents of file1.txt is "Hello, World!"
            Get-Content -Path "$TestDrive/ItemsToOverwriteContainer/subdir1/file1.txt" | Should -Be "Hello, World!"
        }

        It "Overwrites a directory whose path is the same as the destination path of a file in the archive when -WriteMode Overwrite is specified" {
            $sourcePath = "$TestDrive/archive1.zip"
            $destinationPath = "$TestDrive/ItemsToOverwriteContainer/subdir2"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 0

            # Ensure the file in archive1.zip was expanded
            Test-Path "$TestDrive/ItemsToOverwriteContainer/subdir2/file1.txt" -PathType Leaf

            # Ensure the contents of file1.txt is "Hello, World!"
            Get-Content -Path "$TestDrive/ItemsToOverwriteContainer/subdir2/file1.txt" | Should -Be "Hello, World!"
        }

        It "Overwrites a file whose path is the same as the destination path of a directory in the archive when -WriteMode Overwrite is specified" {
            $sourcePath = "$TestDrive/archive2.zip"
            $destinationPath = "$TestDrive/ItemsToOverwriteContainer/subdir3"
            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 0

            # Ensure the file in archive1.zip was expanded
            Test-Path "$TestDrive/ItemsToOverwriteContainer/subdir3/directory1" -PathType Container
        }
    }

    Context "Basic functionality tests" {
        # extract to a directory works
        # extract to working directory works when DestinationPath is specified
        # expand archive works when -DestinationPath is not specified (and a single top level item which is a directory)
        # expand archive works when -DestinationPath is not specified (and there are mutiple top level items)

        BeforeAll {
            New-Item -Path "$TestDrive/file1.txt" -ItemType File
            "Hello, World!" | Out-File -FilePath "$TestDrive/file1.txt"
            Compress-Archive -Path "$TestDrive/file1.txt" -DestinationPath "$TestDrive/archive1.zip"

            New-Item -Path "$TestDrive/directory2" -ItemType Directory
            New-Item -Path "$TestDrive/directory3" -ItemType Directory
            New-Item -Path "$TestDrive/directory4" -ItemType Directory
            New-Item -Path "$TestDrive/directory5" -ItemType Directory

            New-Item -Path "$TestDrive/DirectoryToArchive" -ItemType Directory
            Compress-Archive -Path "$TestDrive/DirectoryToArchive" -DestinationPath "$TestDrive/archive2.zip"

            # Create an archive containing a file and an empty folder
            Compress-Archive -Path "$TestDrive/file1.txt","$TestDrive/DirectoryToArchive" -DestinationPath "$TestDrive/archive3.zip"
        }

        It "Expands an archive when a non-existent directory is specified as -DestinationPath" {
            $sourcePath = "$TestDrive/archive1.zip"
            $destinationPath = "$TestDrive/directory1"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath

            $itemsInDestinationPath = Get-ChildItem $destinationPath -Recurse
            $itemsInDestinationPath.Count | Should -Be 1
            $itemsInDestinationPath[0].Name | Should -Be "file1.txt"
        }

        It "Expands an archive when DestinationPath is an existing directory" {
            $sourcePath = "$TestDrive/archive1.zip"
            $destinationPath = "$TestDrive/directory1"

            try {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -ErrorAction Stop
            } catch {
                throw "An error was thrown but an error was not expected"
            }
        }

        It "Expands an archive to the working directory when it is specified as -DestinationPath" {
            $sourcePath = "$TestDrive/archive1.zip"
            $destinationPath = "$TestDrive/directory2"

            Push-Location $destinationPath

            Expand-Archive -Path $sourcePath -DestinationPath $PWD

            $itemsInDestinationPath = Get-ChildItem $PWD -Recurse
            $itemsInDestinationPath.Count | Should -Be 1
            $itemsInDestinationPath[0].Name | Should -Be "file1.txt"

            Pop-Location
        }

        It "Expands an archive containing a single top-level directory and no other top-level items to a directory with that directory's name when -DestinationPath is not specified" -Tag this1{
            $sourcePath = "$TestDrive/archive2.zip"
            $destinationPath = "$TestDrive/directory3"

            Push-Location $destinationPath

            Expand-Archive -Path $sourcePath

            $itemsInDestinationPath = Get-ChildItem "$TestDrive/directory3" -Recurse
            $itemsInDestinationPath.Count | Should -Be 1
            $itemsInDestinationPath[0].Name | Should -Be "DirectoryToArchive"

            Test-Path -Path "$TestDrive/directory3/DirectoryToArchive" -PathType Container

            Pop-Location
        }

        It "Expands an archive containing multiple top-level items to a directory with that archive's name when -DestinationPath is not specified" {
            $sourcePath = "$TestDrive/archive3.zip"
            $destinationPath = "$TestDrive/directory4"

            Push-Location $destinationPath

            Expand-Archive -Path $sourcePath

            $itemsInDestinationPath = Get-ChildItem $destinationPath -Name -Recurse
            $itemsInDestinationPath.Count | Should -Be 3
            "archive3" | Should -BeIn $itemsInDestinationPath
            "archive3${DS}DirectoryToArchive" | Should -BeIn $itemsInDestinationPath
            "archive3${DS}file1.txt" | Should -BeIn $itemsInDestinationPath
           

            Pop-Location
        }

        It "Expands an archive containing multiple files, non-empty directories, and empty directories" {
            
            # Create an archive containing multiple files, non-empty directories, and empty directories
            New-Item -Path "$TestDrive/file2.txt" -ItemType File
            "Hello, World!" | Out-File -FilePath "$TestDrive/file2.txt"
            New-Item -Path "$TestDrive/file3.txt" -ItemType File
            "Hello, World!" | Out-File -FilePath "$TestDrive/file3.txt"

            New-Item -Path "$TestDrive/emptydirectory1" -ItemType Directory
            New-Item -Path "$TestDrive/emptydirectory2" -ItemType Directory

            New-Item -Path "$TestDrive/nonemptydirectory1" -ItemType Directory
            New-Item -Path "$TestDrive/nonemptydirectory2" -ItemType Directory

            New-Item -Path "$TestDrive/nonemptydirectory1/subfile1.txt" -ItemType File
            New-Item -Path "$TestDrive/nonemptydirectory2/subemptydirectory1" -ItemType Directory

            $archive4Paths = @("$TestDrive/file2.txt", "$TestDrive/file3.txt", "$TestDrive/emptydirectory1", "$TestDrive/emptydirectory2", "$TestDrive/nonemptydirectory1", "$TestDrive/nonemptydirectory2")

            Compress-Archive -Path $archive4Paths -DestinationPath "$TestDrive/archive4.zip"

            $sourcePath = "$TestDrive/archive4.zip"
            $destinationPath = "$TestDrive/directory5"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath

            $expandedItems = Get-ChildItem $destinationPath -Recurse -Name

            $itemsInArchive = @("file2.txt", "file3.txt", "emptydirectory1", "emptydirectory2", "nonemptydirectory1", "nonemptydirectory2", "nonemptydirectory1${DS}subfile1.txt", "nonemptydirectory2${DS}subemptydirectory1")

            $expandedItems.Length | Should -Be $itemsInArchive.Count
            foreach ($item in $itemsInArchive) {
                $item | Should -BeIn $expandedItems
            }
        }

        It "Expands an archive containing a file whose LastWriteTime is in the past" {
            New-Item -Path "$TestDrive/oldfile.txt" -ItemType File
            Set-ItemProperty -Path "$TestDrive/oldfile.txt" -Name "LastWriteTime" -Value '2003-01-16 14:44'
            Compress-Archive -Path "$TestDrive/oldfile.txt" -DestinationPath "$TestDrive/archive_oldfile.zip"

            $sourcePath = "$TestDrive/archive_oldfile.zip"
            $destinationPath = "$TestDrive/destination6"
            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath

            $lastWriteTime = Get-ItemPropertyValue -Path "$TestDrive/oldfile.txt" -Name "LastWriteTime"

            $lastWriteTime.Year | Should -Be 2003
            $lastWriteTime.Month | Should -Be 1
            $lastWriteTime.Day | Should -Be 16
            $lastWriteTime.Hour | Should -Be 14
            $lastWriteTime.Minute | Should -Be 44
            $lastWriteTime.Second | Should -Be 0
            $lastWriteTime.Millisecond | Should -Be 0
        }

        It "Expands an archive containing a directory whose LastWriteTime is in the past" {
            New-Item -Path "$TestDrive/olddirectory" -ItemType Directory
            Set-ItemProperty -Path "$TestDrive/olddirectory" -Name "LastWriteTime" -Value '2003-01-16 14:44'
            Compress-Archive -Path "$TestDrive/olddirectory" -DestinationPath "$TestDrive/archive_olddirectory.zip"

            $sourcePath = "$TestDrive/archive_olddirectory.zip"
            $destinationPath = "$TestDrive/destination_olddirectory"
            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath

            $lastWriteTime = Get-ItemPropertyValue -Path "$TestDrive/destination_olddirectory/olddirectory" -Name "LastWriteTime"

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
            "Hello, World!" | Out-File -Path Test:/file1.txt
            $archivePath = "TestDrive:/archive.zip"
            Compress-Archive -Path TestDrive:/file1.txt -DestinationPath $archivePath
        }

        It "Returns a System.IO.DirectoryInfo object when PassThru is specified" {
            $destinationPath = "{TestDrive}/archive_contents"
            $output = Expand-Archive -Path $archivePath -DestinationPath $destinationPath -PassThru
            $output | Should -BeOfType System.IO.DirectoryInfo
            $output.FullName | SHould -Be $destinationPath
        }

        It "Does not return an object when PassThru is not specified" {            
            $output = Expand-Archive -Path $archivePath -DestinationPath TestDrive:/archive_contents2
            $output | Should -BeNullOrEmpty
        }

        It "Does not return an object when PassThru is false" {            
            $output = Compress-Archive -Path $archivePath -DestinationPath TestDrive:/archive_contents -PassThru:$false
            $output | Should -BeNullOrEmpty
        }
    }

    
}