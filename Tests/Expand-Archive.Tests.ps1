# Tests for Expand-Archive

Describe("Expand-Archive Tests") {
    BeforeAll {
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
        $CmdletClassName = "Microsoft.PowerShell.Archive.ExpandArchiveCommand"
        $DS = [System.IO.Path]::DirectorySeparatorChar
        Add-CompressionAssemblies

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
            New-Item $TestDrive$($DS)SourceDir -Type Directory | Out-Null
            $content = "Some Data"
            $content | Out-File -FilePath $TestDrive$($DS)Sample-1.txt

            # Create archives called archive1.zip and archive2.zip
            Compress-Archive -Path $TestDrive$($DS)Sample-1.txt -DestinationPath $TestDrive$($DS)archive1.zip
            Compress-Archive -Path $TestDrive$($DS)Sample-1.txt -DestinationPath $TestDrive$($DS)archive2.zip
        }


        It "Validate errors with NULL & EMPTY values for Path, LiteralPath, and DestinationPath" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)SampleSingleFile.zip"

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

        It "Throws when invalid path non-existing path is supplied for Path or LiteralPath parameters" {
            $path = "$TestDrive$($DS)non-existant.zip"
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
                "$TestDrive$($DS)SourceDir$($DS)archive1.zip",
                "$TestDrive$($DS)SourceDir$($DS)archive2.zip")
            $destinationPath = "$TestDrive$($DS)DestinationFolder"

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
                "$TestDrive$($DS)SourceDir$($DS)archive1.zip",
                "$TestDrive$($DS)SourceDir$($DS)archive2.zip")
            $destinationPath = "$TestDrive$($DS)DestinationFolder"

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
            $sourcePath = "$env:SystemDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)SampleFromSystemDrive.zip"
            New-Item $sourcePath -Type Directory | Out-Null # not enough permissions to write to drive root on Linux
            "Some Data" | Out-File -FilePath $sourcePath$($DS)SampleSourceFileForArchive.txt
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
            $sourcePath = "$TestDrive$($DS)archive1.zip"
            $destinationPath = $sourcePath

            try {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite
                throw "Failed to detect an error when Path and DestinationPath are the same and -Overwrite is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SamePathAndDestinationPath,$CmdletClassName"
            }
        }

        It "Throws an error when LiteralPath and DestinationPath are the same and WriteMode -Overwrite is specified" {
            $sourcePath = "$TestDrive$($DS)archive1.zip"
            $destinationPath = $sourcePath

            try {
                Expand-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite
                throw "Failed to detect an error when LiteralPath and DestinationPath are the same and -Overwrite is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SameLiteralPathAndDestinationPath,$CmdletClassName"
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
        # overwrite output file w/directory
        # overwrite non-existant path works

        # last write times

        BeforeAll {
            "Hello, World!" | Out-File -FilePath "$TestDrive$($DS)file1.txt"
            Compress-Archive -Path "$TestDrive$($DS)file1.txt" -DestinationPath "$TestDrive$($DS)archive1.zip"

            New-Item -Path "$TestDrive$($DS)directory1" -ItemType Directory

            # Create archive2.zip containing directory1
            Compress-Archive -Path "$TestDrive$($DS)directory1" -DestinationPath "$TestDrive$($DS)archive2.zip"

            New-Item -Path "$TestDrive$($DS)ParentDir" -ItemType Directory
            New-Item -Path "$TestDrive$($DS)ParentDir/file1.txt" -ItemType Directory

            # Create a dir that is a container for items to be overwritten
            New-Item -Path "$TestDrive$($DS)ItemsToOverwriteContainer" -ItemType Directory
            New-Item -Path "$TestDrive$($DS)ItemsToOverwriteContainer/file2" -ItemType File
            New-Item -Path "$TestDrive$($DS)ItemsToOverwriteContainer/subdir1" -ItemType Directory
            New-Item -Path "$TestDrive$($DS)ItemsToOverwriteContainer/subdir1/file1.txt" -ItemType File
        }

        It "Throws an error when DestinationPath is an existing file" {
            $sourcePath = "$TestDrive$($DS)archive1.zip"
            $destinationPath = "$TestDrive$($DS)file1.txt"

            try {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "DestinationExists,$CmdletClassName"
            }
        }

        It "Does not throw an error when DestinationPath is an existing directory" {
            $sourcePath = "$TestDrive$($DS)archive1.zip"
            $destinationPath = "$TestDrive$($DS)directory1"

            try {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -ErrorAction Stop
            } catch {
                throw "An error was thrown but an error was not expected"
            }
        }

        It "Does not throw an error when a directory in the archive has the same destination path as an existing directory" {
            $sourcePath = "$TestDrive$($DS)archive2.zip"
            $destinationPath = "$TestDrive"

            try {
                Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -ErrorAction Stop
            } catch {
                throw "An error was thrown but an error was not expected"
            }
        }

        It "Writes a non-terminating error when a file in the archive has a destination path that already exists" {
            $sourcePath = "$TestDrive$($DS)archive1.zip"
            $destinationPath = "$TestDrive"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -ErrorVariable error
            $error.Count | Should -Be 1
            $error[0].FullyQualifiedErrorId | Should -Be "DestinationExists,$CmdletClassName"
        }

        It "Writes a non-terminating error when a file in the archive has a destination path that is an existing directory containing at least 1 item and -WriteMode Overwrite is specified" {
            $sourcePath = "$TestDrive$($DS)archive1.zip"
            $destinationPath = "$TestDrive"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 1
            $error[0].FullyQualifiedErrorId | Should -Be "DestinationExists,$CmdletClassName"
        }

        It "Writes a non-terminating error when a file in the archive has a destination path that is the working directory and -WriteMode Overwrite is specified" {
            $sourcePath = "$TestDrive$($DS)archive1.zip"
            $destinationPath = "$TestDrive$($DS)ParentDir/file1.txt"

            Push-Location $destinationPath

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 1
            $error[0].FullyQualifiedErrorId | Should -Be "CannotOverwriteWorkingDirectory,$CmdletClassName"

            Pop-Location
        }

        It "Overwrites a file when it is DestinationPath and -WriteMode Overwrite is specified" {
            $sourcePath = "$TestDrive$($DS)archive1.zip"
            $destinationPath = "$TestDrive$($DS)ItemsToOverwriteContainer/file2"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 0

            # Ensure the file in archive1.zip was expanded
            Test-Path "$TestDrive$($DS)ItemsToOverwriteContainer/file2/file1.txt"
        }

        It "Overwrites a file whose path is the same as the destination path of a file in the archive when -WriteMode Overwrite is specified" {
            $sourcePath = "$TestDrive$($DS)archive1.zip"
            $destinationPath = "$TestDrive$($DS)ItemsToOverwriteContainer/subdir1"

            Expand-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite -ErrorVariable error
            $error.Count | Should -Be 0

            # Ensure the file in archive1.zip was expanded
            Test-Path "$TestDrive$($DS)ItemsToOverwriteContainer/subdir1/file1.txt"

            # Ensure the contents of file1.txt is "Hello, World!"
            Get-Content -Path "$TestDrive$($DS)ItemsToOverwriteContainer/subdir1/file1.txt" | Should -Be "Hello, World!"
        }
    }
}