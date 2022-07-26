<############################################################################################
 # File: Compress-Archive.Tests.ps1
 ############################################################################################>
 $script:TestSourceRoot = $PSScriptRoot
 $DS = [System.IO.Path]::DirectorySeparatorChar
 if ($IsWindows -eq $null) {
     $IsWindows = $PSVersionTable.PSEdition -eq "Desktop"
 } 
 
 Describe("Microsoft.PowerShell.Archive tests") {
    BeforeAll {

        $DS = [System.IO.Path]::DirectorySeparatorChar

        $originalProgressPref = $ProgressPreference
        $ProgressPreference = "SilentlyContinue"
        $originalPSModulePath = $env:PSModulePath
        # make sure we use the one in this repo
        $env:PSModulePath = "$($script:TestSourceRoot)\..;$($env:PSModulePath)"

        # Add compression assemblies
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

        Add-CompressionAssemblies

        # Used for validating an archive's contents
        function Test-ZipArchive {
            param
            (
                [string] $archivePath,
                [string[]] $expectedEntries
            )
    
            try
            {
                $archiveFileStreamArgs = @($archivePath, [System.IO.FileMode]::Open)
                $archiveFileStream = New-Object -TypeName System.IO.FileStream -ArgumentList $archiveFileStreamArgs
    
                $zipArchiveArgs = @($archiveFileStream, [System.IO.Compression.ZipArchiveMode]::Read, $false)
                $zipArchive = New-Object -TypeName System.IO.Compression.ZipArchive -ArgumentList $zipArchiveArgs
    
                $actualEntryCount = $zipArchive.Entries.Count
                $actualEntryCount | Should -Be $expectedEntries.Length

                # Get a list of entry names in the zip archive
                $archiveEntries = @()
                ForEach ($archiveEntry in $zipArchive.Entries) {
                    $archiveEntries += $archiveEntry.FullName
                }

                # Ensure each entry in the archive is in the list of expected entries
                ForEach ($expectedEntry in $expectedEntries) {
                    $expectedEntry | Should -BeIn $archiveEntries
                }
                
            }
            finally
            {
                if ($null -ne $zipArchive) { $zipArchive.Dispose()}
                if ($null -ne $archiveFileStream) { $archiveFileStream.Dispose() }
            }
        }

        # This function gets a list of a directories descendants formatted as archive entries
        function Get-Descendants {
            param (
                [string] $Path
            )
            

            # Get the folder name
            $folderName =  Split-Path -Path $Path -Leaf

            # Get descendents
            $descendants = Get-ChildItem -Path $Path -Recurse -Name

            $output = @()
            
            # Prefix each descendant name with folder name
            foreach ($name in $descendants) {
                $output += ($folderName + '/' + $name).Replace([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
            }

            return $output
        }
    }
    
    AfterAll {
        $global:ProgressPreference = $originalProgressPref
        $env:PSModulePath = $originalPSModulePath
    }

    Context "Parameter set validation tests" {
        BeforeAll {
            function CompressArchivePathParameterSetValidator {
                param
                (
                    [string[]] $path,
                    [string] $destinationPath
                )
        
                try
                {
                    Compress-Archive -Path $path -DestinationPath $destinationPath
                    throw "ValidateNotNullOrEmpty attribute is missing on one of parameters belonging to Path parameterset."
                }
                catch
                {
                    $_.FullyQualifiedErrorId | Should -Be "ParameterArgumentValidationError,Microsoft.PowerShell.Archive.CompressArchiveCommand"
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
                    Compress-Archive -LiteralPath $literalPath -DestinationPath $destinationPath
                    throw "ValidateNotNullOrEmpty attribute is missing on one of parameters belonging to LiteralPath parameterset."
                }
                catch
                {
                    $_.FullyQualifiedErrorId | Should -Be "ParameterArgumentValidationError,Microsoft.PowerShell.Archive.CompressArchiveCommand"
                }
            }
        
        
            function CompressArchiveInvalidPathValidator {
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
                    $_.FullyQualifiedErrorId | Should -Be $expectedFullyQualifiedErrorId
                }

                try
                {
                    Compress-Archive -LiteralPath $path -DestinationPath $destinationPath
                    throw "Failed to validate that an invalid LiteralPath $invalidPath was supplied as input to Compress-Archive cmdlet."
                }
                catch
                {
                    $_.FullyQualifiedErrorId | Should -Be $expectedFullyQualifiedErrorId
                }
            }
            
            # Set up files for tests
            New-Item $TestDrive$($DS)SourceDir -Type Directory | Out-Null
            $content = "Some Data"
            $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)Sample-1.txt

            New-Item $TestDrive$($DS)EmptyDirectory -Type Directory | Out-Null
        }


        It "Validate errors from Compress-Archive with NULL & EMPTY values for Path, LiteralPath, DestinationPath, CompressionLevel parameters" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)SampleSingleFile.zip"

            CompressArchivePathParameterSetValidator $null $destinationPath
            CompressArchivePathParameterSetValidator $sourcePath $null
            CompressArchivePathParameterSetValidator $null $null

            CompressArchivePathParameterSetValidator "" $destinationPath
            CompressArchivePathParameterSetValidator $sourcePath ""
            CompressArchivePathParameterSetValidator "" ""

            CompressArchiveLiteralPathParameterSetValidator $null $destinationPath
            CompressArchiveLiteralPathParameterSetValidator $sourcePath $null
            CompressArchiveLiteralPathParameterSetValidator $null $null

            CompressArchiveLiteralPathParameterSetValidator "" $destinationPath
            CompressArchiveLiteralPathParameterSetValidator $sourcePath ""
            CompressArchiveLiteralPathParameterSetValidator "" ""
        }

        It "Validate errors from Compress-Archive when invalid path (non-existing path / non-filesystem path) is supplied for Path or LiteralPath parameters" {
            CompressArchiveInvalidPathValidator "$TestDrive$($DS)InvalidPath" "$TestDrive($DS)archive.zip" "$TestDrive$($DS)InvalidPath" "PathNotFound,Microsoft.PowerShell.Archive.CompressArchiveCommand"

            $path = @("$TestDrive", "$TestDrive$($DS)InvalidPath")
            CompressArchiveInvalidPathValidator $path "$TestDrive($DS)archive.zip" "$TestDrive$($DS)InvalidPath" "PathNotFound,Microsoft.PowerShell.Archive.CompressArchiveCommand"
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
                $_.FullyQualifiedErrorId | Should -Be "DuplicatePaths,Microsoft.PowerShell.Archive.CompressArchiveCommand"
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
                $_.FullyQualifiedErrorId | Should -Be "DuplicatePaths,Microsoft.PowerShell.Archive.CompressArchiveCommand"
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

        # This cannot happen in -WriteMode Create because another error will be throw before
        It "Throws an error when Path and DestinationPath are the same" -Skip {
            $sourcePath = "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt"
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
            $sourcePath = "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt"
            $destinationPath = $sourcePath

            try {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Update
                throw "Failed to detect an error when Path and DestinationPath are the same and -Update is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SamePathAndDestinationPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws an error when Path and DestinationPath are the same and -Overwrite is specified" -Tag td {
            $sourcePath = "$TestDrive$($DS)EmptyDirectory"
            $destinationPath = $sourcePath

            try {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite
                throw "Failed to detect an error when Path and DestinationPath are the same and -Overwrite is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SamePathAndDestinationPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws an error when LiteralPath and DestinationPath are the same" -Skip {
            $sourcePath = "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt"
            $destinationPath = $sourcePath

            try {
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath
                throw "Failed to detect an error when LiteralPath and DestinationPath are the same"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SameLiteralPathAndDestinationPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws an error when LiteralPath and DestinationPath are the same and -Update is specified" {
            $sourcePath = "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt"
            $destinationPath = $sourcePath

            try {
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath -WriteMode Update
                throw "Failed to detect an error when LiteralPath and DestinationPath are the same and -Update is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SameLiteralPathAndDestinationPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws an error when LiteralPath and DestinationPath are the same and -Overwrite is specified" {
            $sourcePath = "$TestDrive$($DS)EmptyDirectory"
            $destinationPath = $sourcePath

            try {
                Compress-Archive -LiteralPath $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite
                throw "Failed to detect an error when LiteralPath and DestinationPath are the same and -Overwrite is specified"
            } catch {
                $_.FullyQualifiedErrorId | Should -Be "SameLiteralPathAndDestinationPath,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }


    }

    Context "Basic functional tests" {
        BeforeAll {
            New-Item $TestDrive$($DS)SourceDir -Type Directory | Out-Null
            New-Item $TestDrive$($DS)SourceDir$($DS)ChildDir-1 -Type Directory | Out-Null
            New-Item $TestDrive$($DS)SourceDir$($DS)ChildDir-2 -Type Directory | Out-Null
            New-Item $TestDrive$($DS)SourceDir$($DS)ChildEmptyDir -Type Directory | Out-Null

            # create an empty directory
            New-Item $TestDrive$($DS)EmptyDir -Type Directory | Out-Null
    
            $content = "Some Data"
            $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)Sample-1.txt
            $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-2.txt
            $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)ChildDir-2$($DS)Sample-3.txt
        }

        It "Validate that a single file can be compressed" {
            $sourcePath = "$TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-2.txt"
            $destinationPath = "$TestDrive$($DS)archive1.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -Exist
            Test-ZipArchive $destinationPath @('Sample-2.txt')
        }

        It "Validate that an empty folder can be compressed" {
            $sourcePath = "$TestDrive$($DS)EmptyDir"
            $destinationPath = "$TestDrive$($DS)archive2.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -Exist
            Test-ZipArchive $destinationPath @('EmptyDir/')
        }

        It "Validate a folder containing files, non-empty folders, and empty folders can be compressed" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)archive3.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            $destinationPath | Should -Exist
            $contents = Get-Descendants -Path $sourcePath
            $contents += "SourceDir/"
            Test-ZipArchive $destinationPath $contents
        }
    }

    Context "Update tests" -Skip {
        
    }

    Context "DestinationPath tests" {
        BeforeAll {
            New-Item $TestDrive$($DS)SourceDir -Type Directory | Out-Null
    
            $content = "Some Data"
            $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)Sample-1.txt
            
            New-Item $TestDrive$($DS)archive3.zip -Type Directory | Out-Null

            New-Item $TestDrive$($DS)EmptyDirectory -Type Directory | Out-Null
        }

        It "Throws an error when archive file already exists and -Update and -Overwrite parameters are not specified" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)archive1.zip"

            try
            {
                "Some Data" > $destinationPath
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
                throw "Failed to validate that an archive file format $destinationPath already exists and -Update switch parameter is not specified."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "ArchiveExists,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws a terminating error when archive does not exist and -Update mode is specified" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)archive2.zip"

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

        It "Throws a terminating error when DestinationPath is a directory and -Update is specified" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)archive3.zip"

            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Update
                throw "Failed to validate that a directory $destinationPath exists and -Update switch parameter is specified."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "ArchiveExistsAsDirectory,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }

        It "Throws a terminating error when DestinationPath is a folder containing at least 1 item and Overwrite is specified" -Tag td2 {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive"

            try
            {
                Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite
                throw "Failed to detect an error when $destinationPath is an existing directory containing at least 1 item and -Overwrite switch parameter is specified."
            }
            catch
            {
                $_.FullyQualifiedErrorId | Should -Be "ArchiveIsNonEmptyDirectory,Microsoft.PowerShell.Archive.CompressArchiveCommand"
            }
        }
    }

    Context "-Overwrite Tests" {
        BeforeAll {
            New-Item $TestDrive$($DS)SourceDir -Type Directory | Out-Null
    
            $content = "Some Data"
            $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)Sample-1.txt

            New-Item $TestDrive$($DS)EmptyDirectory -Type Directory | Out-Null

            # Create $TestDrive$($DS)archive.zip
            Compress-Archive -Path $TestDrive$($DS)SourceDir$($DS)Sample-1.txt -DestinationPath "$TestDrive$($DS)archive.zip"

            # Create Sample-2.txt
            $content | Out-File -FilePath $TestDrive$($DS)Sample-2.txt
        }

        It "Overwrites a directory containing no items when -Overwrite is specified" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)EmptyDirectory"

            (Get-Item $destinationPath) -is [System.IO.DirectoryInfo] | Should -Be $true
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath -WriteMode Overwrite

            # Ensure $destiationPath is now a file
            $destinationPathInfo = Get-Item $destinationPath
            $destinationPathInfo -is [System.IO.DirectoryInfo] | Should -Be $false
            $destinationPathInfo -is [System.IO.FileInfo] | Should -Be $true
        }

        It "Overwrites an archive that already exists" {
            $destinationPath = "$TestDrive$($DS)archive.zip"

            # Get the entries of the original zip archive
            Test-ZipArchive $destinationPath @("Sample-1.txt") 

            # Overwrite the archive
            $sourcePath = "$TestDrive$($DS)Sample-2.txt"
            Compress-Archive -Path $sourcePath -DestinationPath "$TestDrive$($DS)archive.zip" -WriteMode Overwrite

            # Ensure the original entries and different than the new entries
            Test-ZipArchive $destinationPath @("Sample-2.txt") 
        }
    }

    Context "Relative Path tests" -Skip {
        BeforeAll {
            New-Item $TestDrive$($DS)SourceDir -Type Directory | Out-Null
            New-Item $TestDrive$($DS)SourceDir$($DS)ChildDir-1 -Type Directory | Out-Null
    
            $content = "Some Data"
            $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)Sample-1.txt
            $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)ChildDir-1$($DS)Sample-2.txt
        }

        # From 568
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

        # From 582
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

        # From 596
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
    }

    Context "Special and Wildcard Characters Tests" {
        BeforeAll {
            New-Item $TestDrive$($DS)SourceDir -Type Directory | Out-Null
    
            $content = "Some Data"
            $content | Out-File -FilePath $TestDrive$($DS)SourceDir$($DS)Sample-1.txt
        }


        It "Accepts DestinationPath parameter with wildcard characters that resolves to one path" {
            $sourcePath = "$TestDrive$($DS)SourceDir$($DS)Sample-1.txt"
            $destinationPath = "$TestDrive$($DS)Sample[]SingleFile.zip"
            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
        	Test-Path -LiteralPath $destinationPath | Should -Be $true
            Remove-Item -LiteralPath $destinationPath
        }

        It "Accepts DestinationPath parameter with [ but no matching ]" {
            $sourcePath = "$TestDrive$($DS)SourceDir"
            $destinationPath = "$TestDrive$($DS)archive[2.zip"

            Compress-Archive -Path $sourcePath -DestinationPath $destinationPath
            Test-Path -LiteralPath $destinationPath | Should -Be $true
            Test-ZipArchive $destinationPath @("SourceDir/", "SourceDir/Sample-1.txt")
            Remove-Item -LiteralPath $destinationPath
        }
    }
}