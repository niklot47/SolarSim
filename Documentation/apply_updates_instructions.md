# apply_updates.json --- Rules for Update Instructions

This document defines how update instructions must be written when
generating project updates.

------------------------------------------------------------------------

## 1. Always Include Updated Instruction File

If at least one project file was modified, added, or replaced, the
response must include an updated instruction file.

The instruction file must always be included together with the changed
files.

------------------------------------------------------------------------

## 2. File Name Is Fixed

The instruction file must always be named:

apply_updates.json

Never use any other name.

Incorrect examples: - instructions.json - update.json -
update_instructions.json - apply_update.json

Only this exact name is allowed: apply_updates.json

------------------------------------------------------------------------

## 3. Required JSON Structure

The file must always follow this structure:

``` json
{
  "create_folders": [
    "path",
    "path"
  ],
  "copy_files": [
    {
      "source": "FileName.ext",
      "target": "Target/Path/FileName.ext"
    }
  ]
}
```

### Field Description

create_folders

Array of folders that must exist before copying files.

All paths are relative to:

Assets/

Example:

Scripts/UI Resources/Localization Simulation/Economy

------------------------------------------------------------------------

copy_files

Array describing which files must be copied.

source\
File name located in the files folder.

The files folder is flat, therefore source must be only the file name.

Correct: ObjectTreeUIController.cs ru.json

Incorrect: Scripts/UI/ObjectTreeUIController.cs
Resources/Localization/ru.json

target\
Destination path relative to Assets/.

Example:

Scripts/UI/ObjectTreeUIController.cs\
Resources/Localization/ru.json\
Simulation/Economy/EconomyInitializer.cs

------------------------------------------------------------------------

## 4. Documentation Files Location

All architecture and documentation files must always be placed in:

../../Documentation

Examples:

../../Documentation/ARCHITECTURE_STATE.md\
../../Documentation/PROJECT_MAP.md\
../../Documentation/ECONOMY_SYSTEM.md

These files must not be placed inside Assets.

------------------------------------------------------------------------

## 5. Complete Example

``` json
{
  "create_folders": [
    "Scripts/UI",
    "Resources/Localization"
  ],
  "copy_files": [
    {
      "source": "ObjectTreeUIController.cs",
      "target": "Scripts/UI/ObjectTreeUIController.cs"
    },
    {
      "source": "ru.json",
      "target": "Resources/Localization/ru.json"
    },
    {
      "source": "ARCHITECTURE_STATE.md",
      "target": "../../Documentation/ARCHITECTURE_STATE.md"
    }
  ]
}
```

------------------------------------------------------------------------

## 6. Additional Rules

1.  Always output valid JSON.
2.  Do not include comments inside JSON.
3.  Do not omit create_folders even if it is empty.
4.  Do not omit copy_files even if it is empty.
5.  Every generated file must appear in copy_files.

Correct empty example:

``` json
{
  "create_folders": [],
  "copy_files": []
}
```

------------------------------------------------------------------------

## 7. files.zip Structure

The archive always contains a flat structure.

Example:

files.zip ├─ apply_updates.json ├─ ObjectTreeUIController.cs ├─
ShipMovementSystem.cs ├─ ru.json └─ ARCHITECTURE_STATE.md

No subfolders are allowed inside the archive.
