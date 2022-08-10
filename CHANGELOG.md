# Changelog

## 2.0.1-preview2

- Rewrite `Expand-Archive` cmdlet in C#
- Added `-Format` parameter to `Expand-Archive`
- Added `-WriteMode` parameter to `Expand-Archive`
- Added support for zip64
- Fixed a bug where the entry names of files in a directory would not be correct when compressing an archive

## 2.0.1-preview1

- Rewrite `Compress-Archive` cmdlet in C#
- Added `-Format` parameter to `Compress-Archive`
- Added `-WriteMode` parameter to `Compress-Archive`
- Added support for relative path structure preservating when paths relative to the working directory are specified to `-Path` or `-LiteralPath` in `Compress-Archive`
- Added support for zip64
- Fixed a bug where empty directories would not be compressed
- Fixed a bug where an abrupt stop when compressing empty directories would not delete the newly created archive
