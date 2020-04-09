# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

# get a random string of characters a-z and A-Z
function Get-RandomString
{
    param ( [int]$Length = 8 )
    $chars = .{ ([int][char]'a')..([int][char]'z');([int][char]'A')..([int][char]'Z') }
    ([char[]]($chars | Get-Random -Count $Length)) -join ""
}

# get a random string which is not the name of an existing provider
function Get-NonExistantProviderName
{
   param ( [int]$Length = 8 )
   do {
       $providerName = Get-RandomString -Length $Length
   } until ( $null -eq (Get-PSProvider -PSProvider $providername -ErrorAction SilentlyContinue) )
   $providerName
}

# get a random string which is not the name of an existing drive
function Get-NonExistantDriveName
{
    param ( [int]$Length = 8 )
    do {
        $driveName = Get-RandomString -Length $Length
    } until ( $null -eq (Get-PSDrive $driveName -ErrorAction SilentlyContinue) )
    $drivename
}

# get a random string which is not the name of an existing function
function Get-NonExistantFunctionName
{
    param ( [int]$Length = 8 )
    do {
        $functionName = Get-RandomString -Length $Length
    } until ( (Test-Path -Path function:$functionName) -eq $false )
    $functionName
}

Describe "Clear-Content cmdlet tests" -Tags "CI" {
    BeforeAll {
        Import-Module "$PSScriptRoot\..\..\Microsoft.PowerShell.Archive\Microsoft.PowerShell.Archive.psd1"
        New-PSDrive -Name TestDrive -PSProvider Microsoft.PowerShell.Archive -root "$PSScriptRoot/ZipFile.Zip" -ErrorAction "Stop"

        $file1 = "file1.txt"
        $file2 = "file2.txt"
        $file3 = "file3.txt"
        $content1 = "This is content"
        $content2 = "This is content for alternate stream tests"


        New-Item -Path "TestDrive:\$file1" -ItemType File -Force
        New-Item -Path "TestDrive:\$file2" -ItemType File -Value $content1 -Force
        New-Item -Path "TestDrive:\$file3" -ItemType File -Value $content2 -Force

        $streamContent = "content for alternate stream"
        $streamName = "altStream1"
    }

    Context "Clear-Content should actually clear content" {
        It "should clear-Content of TestDrive:\$file1" {
            Set-Content -Path TestDrive:\$file1 -Value "ExpectedContent" -PassThru | Should -BeExactly "ExpectedContent"
            Clear-Content -Path TestDrive:\$file1
        }

        It "shouldn't get any content from TestDrive:\$file1" {
            $result = Get-Content -Path TestDrive:\$file1
            $result | Should -BeNullOrEmpty
        }

        # we could suppress the WhatIf output here if we use the testhost, but it's not necessary
        It "The filesystem provider supports should process" -skip:(!$IsWindows) {
            Clear-Content -Path TestDrive:\$file2 -WhatIf
            Get-Content -Path "TestDrive:\$file2" | Should -be "This is content"
        }

        It "The filesystem provider should support ShouldProcess (reference ProviderSupportsShouldProcess member)" {
            $cci = ((Get-Command -Name Clear-Content).ImplementingType)::new()
            $cci.SupportsShouldProcess | Should -BeTrue
        }

    }
    Context "Proper errors should be delivered when bad locations are specified" {
        It "should throw when targetting a directory." {
            { Clear-Content -Path . -ErrorAction Stop } | Should -Throw -ErrorId "ClearDirectoryContent"
        }

        It "should throw `"Cannot bind argument to parameter 'Path'`" when -Path is `$null" {
            { Clear-Content -Path $null -ErrorAction Stop } |
                Should -Throw -ErrorId "ParameterArgumentValidationErrorNullNotAllowed,Microsoft.PowerShell.Commands.ClearContentCommand"
        }

        #[BugId(BugDatabase.WindowsOutOfBandReleases, 903880)]
        It "should throw `"Cannot bind argument to parameter 'Path'`" when -Path is `$()" {
            { Clear-Content -Path $() -ErrorAction Stop } |
                Should -Throw -ErrorId "ParameterArgumentValidationErrorNullNotAllowed,Microsoft.PowerShell.Commands.ClearContentCommand"
        }

        #[DRT][BugId(BugDatabase.WindowsOutOfBandReleases, 906022)]
        It "should throw 'PSNotSupportedException' when you clear-content to an unsupported provider" {
            $functionName = Get-NonExistantFunctionName
            $null = New-Item -Path function:$functionName -Value { 1 }
            { Clear-Content -Path function:$functionName -ErrorAction Stop } |
                Should -Throw -ErrorId "NotSupported,Microsoft.PowerShell.Commands.ClearContentCommand"
        }

        It "should throw FileNotFound error when referencing a non-existant file" {
            $badFile = "TestDrive:/badfilename.txt"
            { Clear-Content -Path $badFile -ErrorAction Stop } |
                Should -Throw -ErrorId "PathNotFound,Microsoft.PowerShell.Commands.ClearContentCommand"

        }
    }
}