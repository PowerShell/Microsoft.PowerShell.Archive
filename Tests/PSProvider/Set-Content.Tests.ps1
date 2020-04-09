# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
Describe "Set-Content cmdlet tests" -Tags "CI" {
    BeforeAll {
        Import-Module "$PSScriptRoot\..\..\Microsoft.PowerShell.Archive\Microsoft.PowerShell.Archive.psd1"
        New-PSDrive -Name PSProvider -PSProvider Microsoft.PowerShell.Archive -root "$PSScriptRoot/ZipFile.Zip" -ErrorAction "Stop"

        $testdrive = "PSProvider:\"

        $file1 = "file1.txt"
        $filePath1 = Join-Path $testdrive $file1

        try { New-Item PSProvider:\bfile.txt -Value "" -ErrorAction Continue }
        catch {}
    }

    It "A warning should be emitted if both -AsByteStream and -Encoding are used together" {
        $testfile = "${TESTDRIVE}\bfile.txt"
        "test" | Set-Content $testfile
        $result = Get-Content -AsByteStream -Encoding Unicode -Path $testfile -WarningVariable contentWarning *> $null
        $contentWarning.Message | Should -Match "-AsByteStream"
    }

    Context "Set-Content should create a file if it does not exist" {
        AfterEach {
          Remove-Item -Path $filePath1 -Force -ErrorAction SilentlyContinue
        }
        It "should create a file if it does not exist" {
            Set-Content -Path $filePath1 -Value "$file1"
            $result = Get-Content -Path $filePath1
            $result| Should -Be "$file1"
        }
    }
    Context "Set-Content/Get-Content should set/get the content of an exisiting file" {
        BeforeAll {
          New-Item -Path $filePath1 -ItemType File -Force
        }
        It "should set-Content of testdrive\$file1" {
            Set-Content -Path $filePath1 -Value "ExpectedContent"
            $result = Get-Content -Path $filePath1
            $result| Should -Be "ExpectedContent"
        }
        It "should return expected string from testdrive\$file1" {
            $result = Get-Content -Path $filePath1
            $result | Should -BeExactly "ExpectedContent"
        }
        It "should Set-Content to testdrive\dynamicfile.txt with dynamic parameters" {
            Set-Content -Path $testdrive\dynamicfile.txt -Value "ExpectedContent"
            $result = Get-Content -Path $testdrive\dynamicfile.txt
            $result | Should -BeExactly "ExpectedContent"
        }
        It "should return expected string from testdrive\dynamicfile.txt" {
            $result = Get-Content -Path $testdrive\dynamicfile.txt
            $result | Should -BeExactly "ExpectedContent"
        }
        It "should remove existing content from testdrive\$file1 when the -Value is `$null" {
            $AsItWas = Get-Content $filePath1
            $AsItWas | Should -BeExactly "ExpectedContent"
            Set-Content -Path $filePath1 -Value $null -ErrorAction Stop
            $AsItIs = Get-Content $filePath1
            $AsItIs | Should -Not -Be $AsItWas
        }
        It "should throw 'ParameterArgumentValidationErrorNullNotAllowed' when -Path is `$null" {
            { Set-Content -Path $null -Value "ShouldNotWorkBecausePathIsNull" -ErrorAction Stop } | Should -Throw -ErrorId "ParameterArgumentValidationErrorNullNotAllowed,Microsoft.PowerShell.Commands.SetContentCommand"
        }
        It "should throw 'ParameterArgumentValidationErrorNullNotAllowed' when -Path is `$()" {
            { Set-Content -Path $() -Value "ShouldNotWorkBecausePathIsInvalid" -ErrorAction Stop } | Should -Throw -ErrorId "ParameterArgumentValidationErrorNullNotAllowed,Microsoft.PowerShell.Commands.SetContentCommand"
        }
        #[BugId(BugDatabase.WindowsOutOfBandReleases, 9058182)]
        It "should be able to pass multiple [string]`$objects to Set-Content through the pipeline to output a dynamic Path file" {
            "hello","world"|Set-Content $testdrive\dynamicfile2.txt
            $result=Get-Content $testdrive\dynamicfile2.txt
            $result.length | Should -Be 2
            $result[0]     | Should -BeExactly "hello"
            $result[1]     | Should -BeExactly "world"
        }
    }
}