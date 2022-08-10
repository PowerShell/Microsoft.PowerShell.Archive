# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
[CmdletBinding(SupportsShouldProcess=$true)]
param (
    [string]$SignedPath
    )


$root = (Resolve-Path -Path "${PSScriptRoot}/../")[0]
$Name = "Microsoft.PowerShell.Archive"
$BuildOutputDir = Join-Path $root "\src\bin\Release"
$ManifestPath = "${BuildOutputDir}\${Name}.psd1"
$ManifestData = Import-PowerShellDataFile -Path $ManifestPath
$Version = $ManifestData.ModuleVersion
$Prelease = $ManifestPath.PrivateData.PSData.Prerelease

# this takes the files for the module and publishes them to a created, local repository
# so the nupkg can be used to publish to the PSGallery
function Export-Module
{
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("PSAvoidUsingWriteHost", "")]
    param()
    $packageRoot = $SignedPath

    if ( -not (Test-Path $packageRoot)) {
        throw "'$PubDir' does not exist"
    }

    # now constuct a nupkg by registering a local repository and calling publish module
    $repoName = [guid]::newGuid().ToString("N")
    Register-PSRepository -Name $repoName -SourceLocation $packageRoot -InstallationPolicy Trusted
    Publish-Module -Path $packageRoot -Repository $repoName
    Unregister-PSRepository -Name $repoName
    Get-ChildItem -Recurse -Name $packageRoot | Write-Verbose
    $nupkgName = "{0}.{1}-{2}.nupkg" -f ${Name},${Version},${Prerelease}


    $nupkgPath = Join-Path $packageRoot $nupkgName
    if ($env:TF_BUILD) {
        # In Azure DevOps
        Write-Host "##vso[artifact.upload containerfolder=$nupkgName;artifactname=$nupkgName;]$nupkgPath"
    }
}

# The SBOM should already be in -SignedPath, so there is no need to copy it

Export-Module
