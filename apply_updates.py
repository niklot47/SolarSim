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


class Console:
    RESET = "\033[0m"
    RED = "\033[91m"
    GREEN = "\033[92m"
    YELLOW = "\033[93m"
    CYAN = "\033[96m"

    @staticmethod
    def enable_windows_ansi() -> None:
        try:
            import ctypes

            kernel32 = ctypes.windll.kernel32
            handle = kernel32.GetStdHandle(-11)
            if handle == 0:
                return

            mode = ctypes.c_uint()
            if kernel32.GetConsoleMode(handle, ctypes.byref(mode)) == 0:
                return

            ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004
            kernel32.SetConsoleMode(handle, mode.value | ENABLE_VIRTUAL_TERMINAL_PROCESSING)
        except Exception:
            pass

    @classmethod
    def info(cls, message: str) -> None:
        print(f"{cls.CYAN}{message}{cls.RESET}")

    @classmethod
    def ok(cls, message: str) -> None:
        print(f"{cls.GREEN}{message}{cls.RESET}")

    @classmethod
    def warn(cls, message: str) -> None:
        print(f"{cls.YELLOW}{message}{cls.RESET}")

    @classmethod
    def error(cls, message: str) -> None:
        print(f"{cls.RED}{message}{cls.RESET}")


def ensure_exists(path: Path, name: str) -> None:
    if not path.exists():
        raise FileNotFoundError(f"{name} not found: {path}")


def resolve_target_path(base: Path, relative_path: str) -> Path:
    base_resolved = base.resolve()
    target = (base / relative_path).resolve()

    try:
        target.relative_to(base_resolved)
    except ValueError:
        Console.warn(f"[WARN] Target path goes outside Assets: {relative_path}")
        Console.warn(f"[WARN] Full resolved path: {target}")

    return target


def unpack_zip() -> None:
    Console.info("[INFO] Extracting files.zip")

    ensure_exists(ZIP_FILE, "files.zip")

    if FILES_DIR.exists():
        shutil.rmtree(FILES_DIR)

    FILES_DIR.mkdir()

    with zipfile.ZipFile(ZIP_FILE, "r") as z:
        z.extractall(FILES_DIR)

    Console.ok("[OK] Archive extracted")


def load_instructions(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


def create_folders(folders: list[str]) -> None:
    for folder in folders:
        target_dir = resolve_target_path(ASSETS_DIR, folder)
        target_dir.mkdir(parents=True, exist_ok=True)
        Console.ok(f"[OK] Directory ready: {target_dir}")


def resolve_source_file(source_value: str) -> Path:
    source_name = Path(source_value).name
    source_path = FILES_DIR / source_name

    if not source_path.exists():
        raise FileNotFoundError(f"Source file not found: {source_path}")

    if not source_path.is_file():
        raise FileNotFoundError(f"Source is not a file: {source_path}")

    return source_path


def copy_files(files: list[dict]) -> None:
    for item in files:
        source_value = item["source"]
        target_rel = item["target"]

        source_path = resolve_source_file(source_value)
        target_path = resolve_target_path(ASSETS_DIR, target_rel)

        existed = target_path.exists()

        target_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_path, target_path)

        if existed:
            Console.ok(f"[OK] Replaced: {target_path}")
        else:
            Console.ok(f"[OK] Created: {target_path}")


def delete_paths(paths: list[str]) -> None:
    for rel_path in paths:
        target_path = resolve_target_path(ASSETS_DIR, rel_path)

        if target_path.is_file():
            target_path.unlink()
            Console.ok(f"[OK] Deleted file: {target_path}")
        elif target_path.is_dir():
            shutil.rmtree(target_path)
            Console.ok(f"[OK] Deleted directory: {target_path}")
        else:
            Console.warn(f"[WARN] Path not found for deletion: {target_path}")


def cleanup() -> None:
    Console.info("[INFO] Cleaning temporary files")

    if FILES_DIR.exists():
        shutil.rmtree(FILES_DIR)

    if ZIP_FILE.exists():
        ZIP_FILE.unlink()

    Console.ok("[OK] Cleanup complete")


def validate_instructions(instructions: dict) -> tuple[list[str], list[dict], list[str]]:
    create_folders = instructions.get("create_folders", [])
    copy_files_list = instructions.get("copy_files", [])
    delete_files_list = instructions.get("delete_files", [])

    if not isinstance(create_folders, list):
        raise TypeError("'create_folders' must be a list")

    if not isinstance(copy_files_list, list):
        raise TypeError("'copy_files' must be a list")

    if not isinstance(delete_files_list, list):
        raise TypeError("'delete_files' must be a list")

    return create_folders, copy_files_list, delete_files_list


def main() -> None:
    Console.enable_windows_ansi()
    Console.info("[INFO] Starting update process")

    ensure_exists(ASSETS_DIR, "Assets directory")

    unpack_zip()
    ensure_exists(INSTRUCTIONS_FILE, "instructions file")

    instructions = load_instructions(INSTRUCTIONS_FILE)
    folders, files, delete_list = validate_instructions(instructions)

    create_folders(folders)
    copy_files(files)
    delete_paths(delete_list)

    cleanup()

    Console.ok("[OK] Update process completed successfully")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        Console.enable_windows_ansi()
        Console.error(f"[ERROR] {exc}")
        raise
