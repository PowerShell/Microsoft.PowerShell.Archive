[CmdletBinding(SupportsShouldProcess=$true)]
param (
    [switch]$test,
    [switch]$build,
    [switch]$publish,
    [switch]$signed,
    [switch]$package,
    [switch]$coverage,
    [switch]$CopySBOM,
    [string]$SignedPath
    )


$root = (Resolve-Path -Path "${PSScriptRoot}/../")[0]
$Name = "Microsoft.PowerShell.Archive"
$BuildOutputDir = Join-Path $root "\src\bin\Release"
$ManifestPath = "${BuildOutputDir}\${Name}.psd1"
$ManifestData = Import-PowerShellDataFile -Path $ManifestPath
$Version = $ManifestData.ModuleVersion

$SignRoot = "${root}\signed\${Name}"
$SignVersion = "$SignRoot\$Version"

$PubBase  = "${root}\out"
$PubRoot  = "${PubBase}\${Name}"
$PubDir   = "${PubRoot}\${Version}"

if (-not $test -and -not $build -and -not $publish -and -not $package) {
    throw "must use 'build', 'test', 'publish', 'package'"
}

[bool]$verboseValue = $PSBoundParameters['Verbose'].IsPresent ? $PSBoundParameters['Verbose'].ToBool() : $false

$FileManifest = @(
    @{ SRC = "${$BuildOutputDir}"; NAME = "Microsoft.PowerShell.Archive.dll"; SIGN = $true ; DEST = "OUTDIR" }
    @{ SRC = "${$BuildOutputDir}"; NAME = "Microsoft.PowerShell.Archive.psm1"; SIGN = $true ; DEST = "OUTDIR" }
)

if ($build) {
    Write-Verbose -Verbose -Message "No action for build"
}

# this takes the files for the module and publishes them to a created, local repository
# so the nupkg can be used to publish to the PSGallery
function Export-Module
{
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("PSAvoidUsingWriteHost", "")]
    param()
    if ( $signed ) {
        $packageRoot = $SignedPath
    }
    else {
        $packageRoot = $PubRoot
    }

    if ( -not (test-path $packageRoot)) {
        throw "'$PubDir' does not exist"
    }

    # now constuct a nupkg by registering a local repository and calling publish module
    $repoName = [guid]::newGuid().ToString("N")
    Register-PSRepository -Name $repoName -SourceLocation $packageRoot -InstallationPolicy Trusted
    Publish-Module -Path $packageRoot -Repository $repoName
    Unregister-PSRepository -Name $repoName
    Get-ChildItem -Recurse -Name $packageRoot | Write-Verbose
    $nupkgName = "{0}.{1}-preview1.nupkg" -f ${Name},${Version}
    $nupkgPath = Join-Path $packageRoot $nupkgName
    if ($env:TF_BUILD) {
        # In Azure DevOps
        Write-Host "##vso[artifact.upload containerfolder=$nupkgName;artifactname=$nupkgName;]$nupkgPath"
    }
}

if ($publish) {
    Write-Verbose "Publishing to '$PubDir'"
    if (-not (test-path $PubDir)) {
        $null = New-Item -ItemType Directory $PubDir -Force
    }
    foreach ($file in $FileManifest) {
        if ($signed -and $file.SIGN) {
            $src = Join-Path -Path $PSScriptRoot -AdditionalChildPath $file.NAME -ChildPath signed
        }
        else {
            $src = Join-Path -Path $file.SRC -ChildPath $file.NAME
        }
        $targetDir = $file.DEST -creplace "OUTDIR","$PubDir"
        if (-not (Test-Path $src)) {
            throw ("file '" + $src + "' not found")
        }
        if (-not (Test-Path $targetDir)) {
            $null = New-Item -ItemType Directory $targetDir -Force
        }
        Copy-Item -Path $src -destination $targetDir -Verbose:$verboseValue

    }
}

# this copies the manifest before creating the module nupkg
# if -CopySBOM is used.
if ($package) {
    if($CopySBOM) {
        #Copy-Item -Recurse -Path "signed/_manifest" -Destination $SignVersion
    }
    Export-Module
}
