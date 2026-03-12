from __future__ import annotations

import json
import shutil
import zipfile
from pathlib import Path


ROOT_DIR = Path(__file__).resolve().parent
ASSETS_DIR = ROOT_DIR / "SolarSim" / "Assets"

ZIP_FILE = ROOT_DIR / "files.zip"
FILES_DIR = ROOT_DIR / "files"
INSTRUCTIONS_FILE = FILES_DIR / "apply_updates.json"


def ensure_exists(path: Path, name: str) -> None:
    if not path.exists():
        raise FileNotFoundError(f"{name} not found: {path}")


def safe_target_path(base: Path, relative_path: str) -> Path:
    target = (base / relative_path).resolve()

    try:
        target.relative_to(base.resolve())
    except ValueError as exc:
        raise ValueError(f"Unsafe target path outside Assets: {relative_path}") from exc

    return target


def unpack_zip() -> None:
    print("[INFO] Extracting files.zip")

    ensure_exists(ZIP_FILE, "files.zip")

    if FILES_DIR.exists():
        shutil.rmtree(FILES_DIR)

    FILES_DIR.mkdir()

    with zipfile.ZipFile(ZIP_FILE, "r") as z:
        z.extractall(FILES_DIR)

    print("[OK] Archive extracted")


def load_instructions(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


def create_folders(folders: list[str]) -> None:
    for folder in folders:
        target_dir = safe_target_path(ASSETS_DIR, folder)
        target_dir.mkdir(parents=True, exist_ok=True)
        print(f"[OK] Directory ready: {target_dir}")


def resolve_source_file(source_value: str) -> Path:
    source_name = Path(source_value).name
    source_path = FILES_DIR / source_name

    if not source_path.exists():
        raise FileNotFoundError(f"Source file not found: {source_path}")

    return source_path


def copy_files(files: list[dict]) -> None:
    for item in files:
        source_value = item["source"]
        target_rel = item["target"]

        source_path = resolve_source_file(source_value)
        target_path = safe_target_path(ASSETS_DIR, target_rel)

        existed = target_path.exists()

        target_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_path, target_path)

        if existed:
            print(f"[OK] Replaced: {target_path}")
        else:
            print(f"[OK] Created: {target_path}")


def cleanup() -> None:
    print("[INFO] Cleaning temporary files")

    if FILES_DIR.exists():
        shutil.rmtree(FILES_DIR)

    if ZIP_FILE.exists():
        ZIP_FILE.unlink()

    print("[OK] Cleanup complete")


def main() -> None:
    print("[INFO] Starting update process")

    ensure_exists(ASSETS_DIR, "Assets directory")

    unpack_zip()

    ensure_exists(INSTRUCTIONS_FILE, "instructions file")

    instructions = load_instructions(INSTRUCTIONS_FILE)

    folders = instructions.get("create_folders", [])
    files = instructions.get("copy_files", [])

    create_folders(folders)
    copy_files(files)

    cleanup()

    print("[INFO] Update process completed successfully")


if __name__ == "__main__":
    main()