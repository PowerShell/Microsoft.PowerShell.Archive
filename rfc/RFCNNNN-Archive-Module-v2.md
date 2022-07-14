---
RFC:
Author: Abdullah Yousuf
Status: Draft
SupercededBy: 
Version: 1.0
Area: Archive
Comments Due: 8/6/2022
Plan to implement: Yes
---

# Archive Module Version 2

This RFC proposes new features and changes for the existing Microsoft.PowerShell.Archive module.
The goal for the Archive module is to build a high-performing and maintainable module that offers high utility and works cross-platform (especially with regard to file paths).

Currently, the archive module has a number of limitations.
First, the module supports the zip32 format only.
There is an opportunity to support additional formats by taking advantage of new .NET APIs.

Second, the module has limited performance compared to other archive software.
Writing the next version of the module in C# instead of PowerShell Script is expected to improve the overall performance of the module.
The module has limited cross-platform support because archive entries are written in an OS-specifc way due to different characters being used as path seperators in different OSs.
This makes it difficult for Unix-based OS users to use archives compressed on a Windows computer or vice versa.

There are also a number of usability issues reported by users.
For example, there are issues with wildcard characters in paths.
Error reporting in some circumstances can be improved and more descriptive.
Compatibility with other archive software can also be improved as there are cases where an archive program may not recognize an archive produced by this module as valid.

Interactions with other parts of PowerShell, such as the job system, advanced functions, and common parameters can be further improved.

The next version of the archive module, Microsoft.PowerShell.Archive v2.0.0, plans on resolving these limitations and usability issues.

As for non-goals, this RFC does not intend to support an exhaustive number of archive formats.
This RFC also does not intend to support an exhaustive number of options for the user to finely control operation.
For example, some cmdlets offer parameters to precisely choose which files and folders to use, such as parameters for file attributes, hidden files, symbolic links, etc.
This RFC does not intend to support a cmdlet for listing the contents of an archive (e.g. Get-Archive).
Support for password-protected archives is not a goal.
Furthermore, additional feature requests are out of scope for the next release of the archive module, but can be implemented in future releases beyond v2.0.0.

## Motivation

    As a PowerShell user,
    I can create archives larger than 4GB and store large files (>4GB) in them,
    so that I store files in a portable and easily compressable format.

Currently, archives are limited to a size of approximately 4GB and individual files in an archive have the same limit.

    As a PowerShell user,
    I can create tar archives,
    so that I can reliably deliver files to Linux-based computers, which have tar support pre-installed.

    As a PowerShell user,
    I can create compressed tar archives,
    so that I can reliably store and deliver content while taking up less storage space.

    As a PowerShell user,
    I can create an archive that preserves the relative path structure, so that I can keep track of which folders the contents of the archive came from and so that I can replicate the same structure on other computers.

Relative path structure refers to the hierarchial structure of a path relative to the current working directory.

    As a PowerShell user,
    I can filter what files to include in an archive,
    so that I store the necessary files only.

    As a PowerShell user,
    I can filter what files to extract from an archive,
    so that I obtain the necessary files only.

    As a PowerShell user,
    I can expand archives compressed on other OSs,
    so that I can access files in an archive without worrying about which OS it came from.

    As a PowerShell user,
    I can expand archives quickly and compress files and folders quickly,
    so that I can reduce the cost of operating my server.

## User Experience

### Compress-Archive

Parameter sets:

```powershell
Compress-Archive [-Path] <string[]> [-DestinationPath] <string> [-CompressionLevel {Optimal | NoCompression | Fastest | SmallestSize}] [-PassThru] [-Format {zip | tar | tgz}] [-Filter <string[]>] [-Flatten] [-WhatIf] [-Confirm] [<CommonParameters>]

Compress-Archive [-Path] <string[]> [-DestinationPath] <string> -Overwrite [-CompressionLevel {Optimal | NoCompression | Fastest | SmallestSize}] [-PassThru] [-Format {zip | tar | tgz}] [-Filter <string[]>] [-Flatten] [-WhatIf] [-Confirm] [<CommonParameters>]

Compress-Archive [-Path] <string[]> [-DestinationPath] <string> -Update [-CompressionLevel {Optimal |NoCompression | Fastest | SmallestSize}] [-PassThru] [-Format {zip | tar | tgz}] [-Filter <string[]>] [-Flatten] [-WhatIf] [-Confirm] [<CommonParameters>]

Compress-Archive [-DestinationPath] <string> -LiteralPath <string[]> [-CompressionLevel {Optimal | NoCompression | Fastest | SmallestSize}] [-PassThru] [-Format {zip | tar | tgz}] [-Filter <string[]>] [-Flatten] [-WhatIf] [-Confirm] [<CommonParameters>]

Compress-Archive [-DestinationPath] <string> -LiteralPath <string[]> -Overwrite [-CompressionLevel {Optimal | NoCompression | Fastest | SmallestSize}] [-PassThru] [-Format {zip | tar | tgz}] [-Filter <string[]>] [-Flatten] [-WhatIf] [-Confirm] [<CommonParameters>]

Compress-Archive [-DestinationPath] <string> -LiteralPath <string[]> -Update [-CompressionLevel {Optimal | NoCompression | Fastest | SmallestSize}] [-PassThru] [-Format {zip | tar | tgz}] [-Filter <string[]>] [-Flatten] [-WhatIf] [-Confirm] [<CommonParameters>]

```

```powershell
Compress-Archive -Path MyFolder -DestinationPath destination.zip -Format zip
```

A zip archive is created with the name `destination.zip`.

```powershell
Compress-Archive -Path MyFolder -DestinationPath destination.tar -Format tar
```

A tar archive is created with the name `destination.tar`.

```powershell
Compress-Archive -Path MyFolder -DestinationPath destination.tar.gz -Format tgz
```

A tar.gz archive is created with the name `destination.tar.gz`.

```powershell
Compress-Archive -Path MyFolder -DestinationPath destination.zip -Filter *.txt
```

A zip archive is created with the name `destination.zip`.
Notice the cmdlet sees the `.zip` extension and uses the `zip` format.
The only files in the archive are those with the `.txt` extension.
The directory structure of `MyFolder` is maintained in the archive.

```powershell
Compress-Archive -Path MyGrandparentFolder/MyParentFolder/MyFolder -DestinationPath destination.zip 
```

A zip archive is created with the name `destination.zip`. The archive is structured as:

```
destination.zip
|---MyGrantparentFolder
    |---MyParentFolder
        |---MyFolder
            |---*
```

The archive preserves the relative structure of the input path.

```powershell
Compress-Archive -Path MyGrandparentFolder/MyParentFolder/MyFolder -DestinationPath destination.zip -Flatten
```

A zip archive is created with the name `destination.zip`.
The archive contains all the files in or descendents of MyFolder.
The archive **does not** retain the directory structure since `-Flatten` is specified.

### Expand-Archive

Parameter sets:

```powershell
Expand-Archive [-Path] <string> [[-DestinationPath] <string>] [-Overwrite] [-PassThru] [-Filter <string[]>] [-WhatIf] [-Confirm] [<CommonParameters>]

Expand-Archive [[-DestinationPath] <string>] -LiteralPath <string> [-Overwrite] [-PassThru] [-Filter <string[]>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

```powershell
Expand-Archive -Path MyArchive.tar.gz
```

If the archive has only 1 top-level folder and no other top-level items, a new folder is created in the current working directory with the name of the top-level folder.
Otherwise, a new folder is created in the current working directory called `MyArchive`.
The files and directories contained in the archive are added to the folder which was created.

```powershell
Expand-Archive -Path MyArchive.tar.gz -Format zip
```

This does the same as above except that the archive is forcably interpreted as a zip archive rather than a compressed tar archive.
A warning is reported notifying user about the mismatch between the extension of `MyArchive.tar.gz` and the value of the `-Format` parameter.

```powershell
Expand-Archive -Path MyArchive.tar.gz -Format zip -Filter *.txt
```

This does the same as above except that only the files ending with `.txt` extension are extracted. The directory structure is still maintained.

```powershell
Expand-Archive -Path MyArchive.tar.gz -DestinationPath MyFolder
```

The archive is determined as a `tar.gz` archive based on the extension.
The entire contents of the archive are put inside `MyFolder`.

## Specification

### Compress-Archive

#### `-Format` parameter

`Compress-Archive` has an optional parameter called `-Format`, which accepts one of three options: `zip`, `tar`, or `tgz`.
This parameter supports tab completion.

The format of the archive is determined by the extension of `-DestinationPath` required parameter or the value supplied to the `-Format` parameter.
If the extension is `.zip`, `.tar`, or `.tar.gz`, the appropriate archive format is determined by the command.

Example: `Compress-Archive -Path MyFolder -DestinationPath destination.tar`

The tar format is chosen for the archive since destination.tar has the `.tar` extension.

When the `-DestinationPath` parameter does not have a matching extension and the `-Format` parameter is not specified, by default the zip format is chosen.

In the case when both `-DestinationPath` parameter has a matching extension and `-Format` parameter are specified, the `-Format` parameter takes precedence.
When `-DestinationPath` has no extension or the extension does not match the value supplied to `-Format` or the default format value (zip) if `-Format` is not specified, a warning notifying the user about the mismatch is reported.

Example: `Compress-Archive -Path MyFolder -DestinationPath destination.tar -Format zip`

The zip format is chosen for the archive since the `-Format` parameter takes precedence over the extension of destination.tar.
A warning is reported to the user about the mismatch between the archive extension and chosen format value.

Example: `Compress-Archive -Path MyFolder -DestinationPath destination`

The zip format is chosen for the archive by default.
A warning, notifying the user that value of `-DestinationPath` has a missing extension and that the zip format is chosen as the archive format by default, is reported to the user.
Note that `.zip` is not appended to the archive name.

#### **Relative Path Structure Preservation**

When valid path(s) is supplied to the `-Path` or `-LiteralPath` parameter, the relative structure of the path is preserved as long as the path is not absolute, and does not contain ~ or .. (the path can contain other wildcard characters such as *, [, ], ?, and `).

Example: `Compress-Archive -Path Documents\MyFile.txt -DestinationPath destination.zip`
creates an archive with the structure:

```
destination.zip
|---Documents
    |---MyFile.txt
```

Example: `Compress-Archive -Path Documents -DestinationPath destination.zip` creates an archive in which the `Documents` folder is the only top-level item and the contents of the folder are retained with the same structure.
The directory structure of `Documents` is retained in the archive.

Example: `Compress-Archive -Path C:\Users\<user>\Documents -DestinationPath destination.zip` does the same as above.
Note that the grandparent and parent directories `User` and `<user>` of the `Documents` folder are not preserved in the archive because the path is absolute.

Similarly, `Compress-Archive -Path ~\Documents -DestinationPath destination.zip` and `Compress-Archive -Path C:\Users\<user>\..\<user>\Documents -DestinationPath destination.zip` exhibit the same behavior as above.

The relative path(s) supplied to the `-Path` or `-LiteralPath` parameter must be relative to the current working directory.

Example: Suppose for this example the curent working directory is `~/Documents` and we want to archive `~/Pictures`.
`Compress-Archive -Path Pictures -DestinationPath archive.zip` will throw a terminating error as long as `~/Documents/Pictures` is not an existing file or folder.

The `-Flatten` switch parameter can be used to remove directories from the archive structure (it keeps the archive structure flat).
When `-Flatten` is specified, the archive contains only files which are supplied to `-Path` or `-LiteralPath` or files that are descendents of folders supplied to either of `-Path` or `-LiteralPath`.

Example: `Compress-Archive -Path Documents\MyFile.txt -DestinationPath destination.zip -Flatten` creates an archive with the following structure:

```
destination.zip
|---MyFile.txt
```

Note that the `Documents` folder is not retained in the archive.

Example: `Compress-Archive -Path Documents -DestinationPath destination.zip -Flatten` creates an archive which only contains the files in or descended from `Documents` and these files are the top-level items.

The `-Flatten` parameter can be used with `-Update`.
In such case, only the added items are flattened.

Example: Suppose we have `archive.zip` which has the following structure:

```
archive.zip
|---Folder1
|   |---file1.txt
```

After calling `Compress-Archive -Path Documents/file2.txt -DestinationPath archive.zip -Update`, `archive.zip` becomes as follows:

```
archive.zip
|---Folder1
|   |---file1.txt
|---Documents
    |---file2.txt
```

When the `-Flatten` parameter is specified to the command above, `archive.zip` instead becomes:

```
archive.zip
|---Folder1
|   |---file1.txt
|---file2.txt
```

#### `-Filter` parameter

A folder is specified if it is supplied as part of `-Path` or `-LiteralPath` or is a descendent of any folder supplied to these two parameters.

A file is specified if it is supplied as part of `-Path` or `-LiteralPath` or is a descendent or any folder supplied to these two parameters.

When the `-Filter` parameter is supplied with a value, the cmdlet adds each file specifed to the archive as long as its filename matches the filter.

The `-Filter` parameter does not affect the directory structure except that empty folders and folders which do not have descendent files that match the filter are omitted.
Except for this behavior, the directory structure of the archive would be identical if `-Filter` was not specified.

The `-Filter` parameter can be used in conjunction with `-Flatten`.

Example: Suppose we want to archive `Folder1` which has the following structure:

```
Folder1
|---ChildFolder1
    |---file.txt
|---ChildFolder2
    |---file.md
```

`Compress-Archive -Path Folder1 -DestinationPath destination.zip -Filter *.txt` creates an archive with the following structure:

```
destination.zip
|---Folder
    |---ChildFolder1
        |---file.txt
```

Example: Suppose we want to archive `Folder2` which has the following structure:

```
Folder2
|---A
|   |---B
|       |---C
|           |---file.txt
|---D
    |---file.md
```

`Compress-Archive -Path Folder2 -DestinationPath destination.zip -Filter *.txt` creates an archive with the following structure:

```
destination.zip
|---Folder
    |---A
        |---B
            |---C
                |---file.txt
```

The directory B is preserved in the archive even though it does not have any immediate children that match the filter because it has descendent files that match the filter.

Example: `Compress-Archive -Path Folder2/A/B/C/file.txt -DestinationPath -Filter *.txt` does the same as above. Note that due to path structure preservation, the folders Folder2, A, B, and C are retained.

Example: `Compress-Archive -Path Folder2/A/B/C/file.txt -DestinationPath destinaton.zip -Filter *.txt -Flatten` creates an archive with the following structure:

```
destination.zip
|---file.txt
```

Note that the folders Folder2, A, B, and C are **not** retained in the archive because `-Flatten` is specified.

Example: `Compress-Archive -Path Folder2 -DestinationPath destinaton.zip -Filter *.txt -Flatten` does the same as above.

The filter accepts standard PowerShell wildcard characters.
The filter performs matching based on the filename, so filtering based on a path does not work.

Example: `Compress-Archive -Path Folder -DestinationPath destination.zip -Filter /ChildFolder/*` will output an empty archive.

Example: `Compress-Archive -Path Folder -DestinationPath destination.zip -Filter s*` outputs an archive whose files start with s only.
The directory structure of the folder is retained in the archive (as long as they contain at least 1 descendent file after applying the filter).

When `-Filter` is used with paths containing wildcard characters, filtering is performed after globbing.

Example: `Compress-Archive -Path ~\Downloads\*.txt -DestinationPath destination.zip -Filter *blue*` creates an archive with the name `destination.zip`.
The archive contains all the files in or descended from the user's downloads directory that end with `.txt` and contain `blue` somewhere in the filename.
The relative path structure is not maintained since the path contains `~` (i.e., the archive does not contain the parent folder `Downloads`).
The directory structure is maintained except for empty directories or directories that are empty after applying the filter.

#### Collision Information

When files or folders with duplicate paths are specified to the cmdlet, a terminating error is thrown.

Example: `Compress-Archive -Path file1.txt,file1.txt -DestinationPath destination.zip` results in an error

When files and/or folders with different paths resolve to the same entry name in the archive, the last write wins.

Example: `Compress-Archive -Path ~/Documents/Folder1/file.txt,~/Documents/Folder2/file.txt -DestinationPath destination.zip -Flatten` creates an archive that contains `file.txt` from `~/Documents/Folder2/file.txt` because it overwrites the file from `~/Documents/Folder1/file.txt`.

If the destination path already exists when `Compress-Archive` is called and `-Update` is not specified, a terminating error is thrown.
`-Overwrite` can be specified to overwrite the file at the destination path (as long as it is a file and not a folder).

Example: Suppose `archive.tar` already exists. `Compress-Archive -Path file.txt -DestinationPath archive.tar` results in a terminating error.

Example: Suppose `archive.tar` already exists and is a file. `Compress-Archive -Path file.txt -DestinationPath archive.tar -Overwrite` creates a new archive overwriting `archive.tar`.

Example: Suppose `archive.tar` already exists and is a folder. `Compress-Archive -Path file.txt -DestinationPath archive.tar -Overwrite` results in a terminating error.

### Expand-Archive

#### `-Format` parameter

The `-Format` parameter accepts one of three options: `zip`, `tar`, or `tgz`. This parameter supports tab completion.

The format of the archive is determined by the extension of `-Path` or `-LiteralPath` (one of which is required) parameter or the value supplied to the `-Format` parameter. If the extension is `.zip`, `.tar`, or `.tar.gz`, the appropriate archive format is determined by the command.

Example: `Expand-Archive -Path archive.tar`

The tar format is chosen for the archive since archive.tar has the `.tar` extension.

When the `-Path` or `-LiteralPath` parameter does not have a matching extension and the `-Format` parameter is not specified, by default the zip format is chosen.

In the case when `-Path` or `-LiteralPath` parameter has a matching extension and `-Format` parameter are specified, the `-Format` parameter takes precedence.
When `-Path` or `-LiteralPath` has no extension or the extension does not match the value supplied to `-Format` or the default format value (zip) if `-Format` is not specified, a warning notifying the user about the mismatch is reported.

Example: `Expand-Archive -Path archive.tar -Format zip`

The zip format is chosen for the archive since the `-Format` parameter takes precedence over the extension of destination.tar.
A warning is reported to the user about the mismatch between the archive extension and chosen format value.

Example: `Expand-Archive -Path archive`

The zip format is chosen for the archive by default.
A warning, notifying the user that `archive` has a missing extension and that the zip format is chosen as the archive format by default, is reported to the user.

For `Expand-Archive`, when an archive format is determined by the cmdlet that does not match the actual format of the archive supplied to it, a terminating error is thrown (e.g., if `-Format zip` is specified for a tar archive).

#### `-Filter` parameter

When the `-Filter` parameter is supplied with a value, the cmdlet extracts files in the archive as long as its filename matches the filter.

The filter applies to all files in the archive no matter how deep they are in the hierarchy.

The `-Filter` parameter does not affect the directory structure except that empty folders and folders which do not have descendent files that match the filter are omitted.
Except for this behavior, the directory structure of the output would be identical if `-Filter` was not specified.

Example: Suppose we want to expand `archive1.zip` which has the following structure:

```
archive1.zip
|---Folder1
    |---ChildFolder1
    |   |---file.txt
    |---ChildFolder2
        |---file.md
```

`Expand-Archive -Path archive1.zip -DestinationPath DestinationFolder -Filter *.txt` creates a folder with the following structure:

```
DestinationFolder
|---Folder1
    |---ChildFolder1
        |---file.txt
```

Example: Suppose we want to expand `archive2.tar` which has the following structure:

```
archive2.tar
|---A
|   |---B
|       |---C
|           |---file.txt
|---D
    |---file.md
```

`Expand-Archive -Path archive2.tar -Filter *.txt` creates a folder `archive2` with the following structure:

```
archive2
    |---A
        |---B
            |---C
                |---file.txt
```

The directory B is preserved in the archive even though it does not have any immediate children that match the filter because it has descendent files that match the filter.

The filter accepts standard PowerShell wildcard characters.
The filter performs matching based on the file name, so filtering based on a path does not work.

Example: `Expand-Archive archive.zip -Filter /ChildFolder/*` creates a folder with the name `archive` whose contents will be empty because no filename in the archive matches the filter.

Example: `Expand-Archive archive.zip -Filter s*` creates a folder (with the name `archive` or with the name of the top-level folder) whose files start with s.
The directory structure is maintained.

#### Default output location for `Expand-Archive`

Currently, when `Expand-Archive archive.zip` is called, the contents of the archive are added to the current working directory. This is unintuitive because the user does not necessarily know what the contents of the archive are.

The solution is to first check if the archive contains one top-level folder and no other top-level items.
If this is the case, the cmdlet creates a folder in the current working directory with the same name as the top-level folder and puts all the contents of the top-level folder into that folder.

Example: Suppose `archive1.zip` contains only one top-level item, a folder called `TopLevelFolder`.
After calling `Expand-Archive archive1.zip`, the structure of the current working directory becomes:

```
$PWD
|---TopLevelFolder
|   |---*
~~~ everything else ~~~
```

If there are multiple top-level items in the archive, the cmdlet creates a folder in the current working directory with the name of the archive without the extension e.g. `archive`) and puts all the contents of the archive into that folder.

Example: Suppose `archive2.zip` contains two top-level items, a folder called `TopLevelFolder` and a file called `file.txt`.
After calling `Expand-Archive archive2.zip`, the structure of the current working directory becomes:

```
$PWD
|---archive2
    |---TopLevelFolder
    |   |---*
    |---file.txt
~~~ everything else ~~~
```

If the user wants to put the contents of the archive in the current working directory, supplying the path of the current working directory to the `-DestinationPath` parameter exhibits such behavior.

Example: Consider `archive2.zip` from the previous example.
After calling `Expand-Archive archive2.zip .`, the structure of the current working directory becomes:

```
$PWD
|---TopLevelFolder
|   |---*
|---file.txt
~~~ everything else ~~~
```

In both cases, whether there is only one top-level item which is a folder, and when there are multiple top-level items, if the folder to be created already exists, the cmdlet continues operation as normal without throwing an error or warning the user.

#### Collision Information

When a file in the archive has the same destination path as a pre-existing file or folder, a terminating error is thrown.
In such case, no files are replaced before the error is thrown.
The user can specify `-Overwrite` to overwrite the pre-existing files or folders.
However, pre-exisiting folders cannot be overwritten with `-Overwrite` if they have children and the terminating error persists in such case.

## Downlevel Support

The enhancements discussed in this RFC depend on System.Formats.Tar v7 and System.IO.Compression .NET Framework 4.8/.NET Core 1.0+.

The current plan is to support PowerShell 7.
Supporting Windows PowerShell requires additional work related to loading the correct assembly because the RFC requires a newer version of System.IO.Compression, and Windows PowerShell depends on an older version of it.

## Alternate Proposals and Considerations

Instead of `tgz`, `tar.gz` can be used as a possible value for the `-Format` parameter.
However, this could impact tab completion as both `tar` and `tar.gz` start with tar.

The default archive format can be different depending on the platform.
Tar can be the default on macOS and Linux whereas Zip can be the default on Windows.

For Compress-Archive, if the value of `-DestinationPath` does not have an extension, the cmdlet can append an extension based on the specified value of the `-Format` parameter or the default archive format.

For Compress-Archive, collisions (two files or folders ending up with the same name in the archive) can result in a terminating error thrown instead of the last write wins behavior.

For Expand-Archive, collisions (two files or folders with the same path) result in a terminating error being thrown.
Instead of this behavior, last write wins can be used.
