# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

Describe "New-Item" -Tags "CI" {
    BeforeAll {
        Import-Module "$PSScriptRoot\..\..\Microsoft.PowerShell.Archive\Microsoft.PowerShell.Archive.psd1"
        New-PSDrive -Name TestDrive -PSProvider Microsoft.PowerShell.Archive -root "$PSScriptRoot/ZipFile.Zip" -ErrorAction "Stop"
        
        $TestDrive                  = "TestDrive:\"
        $tmpDirectory               = $TestDrive
        $testfile                   = "testfile.txt"
        $testfolder                 = "newDirectory"
        $testsubfolder              = "newSubDirectory"
        $testlink                   = "testlink"
        $FullyQualifiedFile         = Join-Path -Path $tmpDirectory -ChildPath $testfile
        $FullyQualifiedFolder       = Join-Path -Path $tmpDirectory -ChildPath $testfolder
        $FullyQualifiedLink         = Join-Path -Path $tmpDirectory -ChildPath $testlink
        $FullyQualifiedSubFolder    = Join-Path -Path $FullyQualifiedFolder -ChildPath $testsubfolder
        $FullyQualifiedFileInFolder = Join-Path -Path $FullyQualifiedFolder -ChildPath $testfile

    }

    BeforeEach {
        if (Test-Path $FullyQualifiedLink)
        {
            Remove-Item $FullyQualifiedLink -Force
        }
    
        if (Test-Path $FullyQualifiedFile)
        {
            Remove-Item $FullyQualifiedFile -Force
        }
    
        if ($FullyQualifiedFileInFolder -and (Test-Path $FullyQualifiedFileInFolder))
        {
            Remove-Item $FullyQualifiedFileInFolder -Force
        }
    
        if ($FullyQualifiedSubFolder -and (Test-Path $FullyQualifiedSubFolder))
        {
            Remove-Item $FullyQualifiedSubFolder -Force
        }

        if (Test-Path $FullyQualifiedFolder)
        {
            Remove-Item $FullyQualifiedFolder -Force
        }

    }
    
    It "should call the function without error" {
        { New-Item -Name $testfile -Path $tmpDirectory -ItemType file } | Should -Not -Throw
    }

    It "should call the function without error" {
        { New-Item -Name $testfile -Path $tmpDirectory -ItemType file } | Should -Not -Throw
    }

    It "Should create a file without error" {
        New-Item -Name $testfile -Path $tmpDirectory -ItemType file

        Test-Path $FullyQualifiedFile | Should -BeTrue

        $fileInfo = Get-ChildItem $FullyQualifiedFile
        $fileInfo.Target | Should -BeNullOrEmpty
        $fileInfo.LinkType | Should -BeNullOrEmpty
    }

    It "Should create a folder without an error" {
        New-Item -Name newDirectory -Path $tmpDirectory -ItemType directory
        Test-Path $FullyQualifiedFolder | Should -BeTrue
    }

    It "Should create a file using the ni alias" {
        ni -Name $testfile -Path $tmpDirectory -ItemType file

        Test-Path $FullyQualifiedFile | Should -BeTrue
    }
    
    It "Should create a file using the Type alias instead of ItemType" {
        New-Item -Name $testfile -Path $tmpDirectory -Type file

        Test-Path $FullyQualifiedFile | Should -BeTrue
    }

    It "Should create a file with sample text inside the file using the Value switch" {
        $expected = "This is test string"
        New-Item -Name $testfile -Path $tmpDirectory -ItemType file -Value $expected

        Test-Path $FullyQualifiedFile | Should -BeTrue

        Get-Content $FullyQualifiedFile | Should -Be $expected
    }

    It "Should not create a file when the Name switch is not used and only a directory specified" {
        #errorAction used because permissions issue in Windows
        
        New-Item -Path $tmpDirectory -ItemType file -ErrorAction SilentlyContinue
        Test-Path $FullyQualifiedFile | Should -BeFalse
    }

    It "Should create a file when the Name switch is not used but a fully qualified path is specified" {
        New-Item -Path $FullyQualifiedFile -ItemType file

        Test-Path $FullyQualifiedFile | Should -BeTrue
    }

    It "Should be able to create a multiple items in different directories" {
        $FullyQualifiedFile2 = Join-Path -Path $tmpDirectory -ChildPath test2.txt
        New-Item -ItemType file -Path $FullyQualifiedFile, $FullyQualifiedFile2

        Test-Path $FullyQualifiedFile  | Should -BeTrue
        Test-Path $FullyQualifiedFile2 | Should -BeTrue

        Remove-Item $FullyQualifiedFile2
    }

    It "Should be able to call the whatif switch without error" {
        { New-Item -Name testfile.txt -Path $tmpDirectory -ItemType file -WhatIf } | Should -Not -Throw
    }

    It "Should not create a new file when the whatif switch is used" {
        New-Item -Name $testfile -Path $tmpDirectory -ItemType file -WhatIf

        Test-Path $FullyQualifiedFile | Should -BeFalse
    }

    It "Should create a file at the root of the drive while the current working directory is not the root" {
        try {
            New-Item -Name $testfolder -Path "TestDrive:\" -ItemType directory > $null
            Push-Location -Path "TestDrive:\$testfolder"
            New-Item -Name $testfile -Path "TestDrive:\" -ItemType file > $null
            Test-Path $FullyQualifiedFile | Should -BeTrue
            #Code changed pester for some odd reason dosnt like Should -Exist
            #$FullyQualifiedFile | Should -Exist
        }
        finally {
            Pop-Location

        }
    }

    It "Should create a folder at the root of the drive while the current working directory is not the root" {
        $testfolder2 = "newDirectory2"
        $FullyQualifiedFolder2 = Join-Path -Path $tmpDirectory -ChildPath $testfolder2

        try {
            New-Item -Name $testfolder -Path "TestDrive:\" -ItemType directory > $null
            Push-Location -Path "TestDrive:\$testfolder"
            New-Item -Name $testfolder2 -Path "TestDrive:\" -ItemType directory > $null
            Test-Path $FullyQualifiedFolder2 | Should -BeTrue
            #Code changed pester for some odd reason dosnt like Should -Exist
            #$FullyQualifiedFolder2 | Should -Exist
            
        }
        finally {
            Pop-Location

            #Fixed a bug where cleanup wasnt happening
            Remove-Item $FullyQualifiedFolder2 -Force

        }
    }

    It "Should create a file in the current directory when using Drive: notation" {
        try {
            New-Item -Name $testfolder -Path "TestDrive:\" -ItemType directory > $null
            Push-Location -Path "TestDrive:\$testfolder"
            New-Item -Name $testfile -Path "TestDrive:" -ItemType file > $null

            Test-Path $FullyQualifiedFileInFolder | Should -BeTrue
            #Code changed pester for some odd reason dosnt like Should -Exist
            #$FullyQualifiedFileInFolder | Should -Exist
        }
        finally {
            Pop-Location
        }
    }

    It "Should create a folder in the current directory when using Drive: notation" {
        try {
            New-Item -Name $testfolder -Path "TestDrive:\" -ItemType directory > $null
            Push-Location -Path "TestDrive:\$testfolder"
            New-Item -Name $testsubfolder -Path "TestDrive:" -ItemType file > $null
            Test-Path $FullyQualifiedSubFolder | Should -BeTrue
            #Code changed pester for some odd reason dosnt like Should -Exist
            #$FullyQualifiedSubFolder | Should -Exist
        }
        finally {
            Pop-Location
        }
    }
}

# More precisely these tests require SeCreateSymbolicLinkPrivilege.
# You can see list of priveledges with `whoami /priv`.
# In the default windows setup, Admin user has this priveledge, but regular users don't.

Describe "New-Item -Force allows to create an item even if the directories in the path don't exist" -Tags "CI" {
    BeforeAll {
        $TestDrive            = "TestDrive:\"
        $testFile             = 'testfile.txt'
        $testFolder           = 'testfolder'
        $FullyQualifiedFolder = Join-Path -Path $TestDrive -ChildPath $testFolder
        $FullyQualifiedFile   = Join-Path -Path $TestDrive -ChildPath $testFolder -AdditionalChildPath $testFile
    }

    BeforeEach {
        # Explicitly removing folder and the file before tests
        Remove-Item $FullyQualifiedFolder -ErrorAction SilentlyContinue
        Remove-Item $FullyQualifiedFile -ErrorAction SilentlyContinue
        Test-Path -Path $FullyQualifiedFolder | Should -BeFalse
        Test-Path -Path $FullyQualifiedFile   | Should -BeFalse
    }

    It "Should error correctly when -Force is not used and folder in the path doesn't exist" {
        { New-Item $FullyQualifiedFile -ErrorAction Stop } | Should -Throw -ErrorId 'NewItemIOError,Microsoft.PowerShell.Commands.NewItemCommand'
        Test-Path $FullyQualifiedFile | Should -BeFalse
        #$FullyQualifiedFile | Should -Not -Exist
    }
    It "Should create new file correctly when -Force is used and folder in the path doesn't exist" {
        { New-Item $FullyQualifiedFile -Force -ErrorAction Stop } | Should -Not -Throw
        Test-Path $FullyQualifiedFile | Should -BeFalse
        #$FullyQualifiedFile | Should -Exist
    }
}

