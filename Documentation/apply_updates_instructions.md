# apply_updates.json --- Rules for Update Instructions

This document defines how update instructions must be written when
generating project updates.

------------------------------------------------------------------------

## 1. Always Include Updated Instruction File

If at least one project file was modified, added, replaced, or deleted,
the response must include an updated instruction file.

The instruction file must always be included together with the changed
files.

------------------------------------------------------------------------

## 2. File Name Is Fixed

The instruction file must always be named:

apply_updates.json

Never use any other name.

------------------------------------------------------------------------

## 3. Required JSON Structure

The file must always follow this structure:

``` json
{
  "create_folders": [],
  "copy_files": [],
  "delete_files": []
}
```

All three fields must always be present, even if empty.

------------------------------------------------------------------------

## 4. Field Description

### create_folders

Array of folders that must exist before copying files.

All paths are relative to:

Assets/

Example:

Scripts/UI\
Resources/Localization

------------------------------------------------------------------------

### copy_files

Array describing which files must be copied.

Fields:

**source**\
File name located in the files folder.

The files folder is flat.\
Therefore source must be only the file name.

Correct: ObjectTreeUIController.cs\
ru.json

Incorrect: Scripts/UI/ObjectTreeUIController.cs

------------------------------------------------------------------------

**target**\
Destination path relative to Assets/.

Example:

Scripts/UI/ObjectTreeUIController.cs\
Resources/Localization/ru.json

------------------------------------------------------------------------

### delete_files

Array of paths to remove.

Rules: - Paths are relative to Assets - Can point to file or folder - If
path does not exist → warning only - Deletion must not stop execution

Example:

Scripts/UI/OldUI.cs\
Resources/Localization/old_ru.json

------------------------------------------------------------------------

## 5. Documentation Files Location

All documentation must always be placed in:

../../Documentation

Examples:

../../Documentation/ARCHITECTURE_STATE.md\
../../Documentation/PROJECT_MAP.md

These files must not be placed inside Assets.

------------------------------------------------------------------------

## 6. Complete Example

``` json
{
  "create_folders": [
    "Scripts/UI",
    "Resources/Localization"
  ],
  "copy_files": [
    {
      "source": "NewUI.cs",
      "target": "Scripts/UI/NewUI.cs"
    }
  ],
  "delete_files": [
    "Scripts/UI/OldUI.cs"
  ]
}
```

------------------------------------------------------------------------

## 7. Mandatory Rules

1.  Always output valid JSON
2.  Always include all three fields:
    -   create_folders
    -   copy_files
    -   delete_files
3.  Do not include comments inside JSON
4.  Every generated file must appear in copy_files exactly once
5.  Source paths must never include folders

------------------------------------------------------------------------

## 8. files.zip Structure

The archive always contains a flat structure.

Example:

files.zip ├─ apply_updates.json ├─ ObjectTreeUIController.cs ├─ ru.json
└─ ARCHITECTURE_STATE.md

No subfolders are allowed.
