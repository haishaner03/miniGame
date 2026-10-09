"""Apply the Unity dependency audit, preserving an archive before removing assets."""

import argparse
import csv
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile


PROJECT = Path(__file__).resolve().parents[2]
ASSETS = (PROJECT / "Assets").resolve()
TABLES = PROJECT / "Assets/GameMain/DataTables"


def checked_asset(relative):
    path = (PROJECT / relative).resolve()
    if not path.is_relative_to(ASSETS) or path == ASSETS:
        raise ValueError(f"Refusing asset outside Assets: {relative}")
    if path.suffix in {".cs", ".dll", ".asmdef", ".rsp"}:
        raise ValueError(f"Code is not part of this resource cleanup: {relative}")
    return path


class Table:
    def __init__(self, name):
        self.path = TABLES / (name + ".txt")
        self.encoding = "utf-8-sig" if self.path.read_bytes().startswith(b"\xef\xbb\xbf") else "utf-8"
        self.lines = self.path.read_text(encoding=self.encoding).splitlines(keepends=True)
        self.records = []
        for index, line in enumerate(self.lines):
            fields = next(csv.reader([line], delimiter="\t"))
            if len(fields) > 1 and fields[1].isdigit() and not fields[0].startswith("#"):
                self.records.append((index, fields))

    def filter(self, predicate):
        selected = [(index, row) for index, row in self.records if predicate(row)]
        removed = {index for index, _ in self.records} - {index for index, _ in selected}
        self.lines = [line for index, line in enumerate(self.lines) if index not in removed]
        self.records = selected
        return len(removed)

    def save(self):
        self.path.write_text("".join(self.lines), encoding=self.encoding, newline="")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("audit", type=Path)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    audit_path = args.audit.resolve()
    if not audit_path.is_relative_to((PROJECT / "Backups").resolve()):
        raise ValueError("Audit must be inside this project's Backups directory")
    audit = json.loads(audit_path.read_text(encoding="utf-8-sig"))
    protected = set(audit["retainedDependencies"])
    candidates = audit["candidates"]
    assert not set(candidates) & protected
    paths = [checked_asset(path) for path in candidates]
    if not all(path.is_file() for path in paths):
        raise ValueError("Asset inventory changed since the dependency audit")
    removed_guids = set()
    archived = set(paths)
    parent_dirs = set()
    for path in paths:
        meta = Path(str(path) + ".meta")
        if meta.exists():
            archived.add(meta)
            match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.MULTILINE)
            if match:
                removed_guids.add(match[1])
        parent = path.parent
        while parent != ASSETS:
            parent_dirs.add(parent)
            folder_meta = Path(str(parent) + ".meta")
            if folder_meta.is_file():
                archived.add(folder_meta)
            parent = parent.parent
    table_names = ["AssetsPath", "Scene", "UIForm", "UIGroup", "Sound", "SoundGroup", "SoundPlayParam"]
    for name in table_names:
        for suffix in (".txt", ".bytes", ".txt.meta", ".bytes.meta"):
            archived.add(TABLES / (name + suffix))
    collection_path = PROJECT / "Assets/GameMain/Configs/ResourceCollection.xml"
    archived.add(collection_path)
    byte_count = sum(path.stat().st_size for path in archived if path.is_file())
    print(json.dumps({"assetCount": len(paths), "archiveFiles": len(archived), "archiveBytes": byte_count,
                      "retainedLegacy": audit["protectedLegacy"], "apply": args.apply}))
    if not args.apply:
        return

    archive_path = audit_path.parent / "removed_assets_and_tables.zip"
    if archive_path.exists():
        raise ValueError("Backup already exists; refusing to overwrite it")
    with ZipFile(archive_path, "w", ZIP_DEFLATED, compresslevel=1) as archive:
        for path in sorted(archived):
            if path.is_file():
                archive.write(path, path.relative_to(PROJECT).as_posix())
    with ZipFile(archive_path) as archive:
        if archive.testzip() is not None:
            raise ValueError("Backup verification failed")

    # Every deletion is a reviewed file inside Assets; shared parent directories are never recursively removed.
    for path in paths:
        path.unlink()
        Path(str(path) + ".meta").unlink(missing_ok=True)
    removed_folders = []
    for folder in sorted(parent_dirs, key=lambda path: len(path.parts), reverse=True):
        relative = folder.relative_to(PROJECT).as_posix()
        if relative in protected or any(folder.iterdir()):
            continue
        assert folder.resolve().is_relative_to(ASSETS) and folder.resolve() != ASSETS
        folder.rmdir()
        Path(str(folder) + ".meta").unlink(missing_ok=True)
        removed_folders.append(relative)

    tables = {name: Table(name) for name in table_names}
    removed_rows = {}
    removed_rows["AssetsPath"] = tables["AssetsPath"].filter(lambda row: (PROJECT / row[3]).is_file())
    asset_ids = {int(row[1]) for _, row in tables["AssetsPath"].records}
    removed_rows["Scene"] = tables["Scene"].filter(lambda row: int(row[3]) in asset_ids)
    removed_rows["UIForm"] = tables["UIForm"].filter(lambda row: int(row[5]) in asset_ids)
    ui_groups = {int(row[4]) for _, row in tables["UIForm"].records}
    removed_rows["UIGroup"] = tables["UIGroup"].filter(lambda row: int(row[1]) in ui_groups)
    removed_rows["Sound"] = tables["Sound"].filter(lambda row: int(row[4]) in asset_ids)
    sound_groups = {int(row[5]) for _, row in tables["Sound"].records}
    sound_parameters = {int(row[6]) for _, row in tables["Sound"].records}
    removed_rows["SoundGroup"] = tables["SoundGroup"].filter(lambda row: int(row[1]) in sound_groups)
    removed_rows["SoundPlayParam"] = tables["SoundPlayParam"].filter(lambda row: int(row[1]) in sound_parameters)
    for table in tables.values():
        table.save()

    existing_guids = set()
    for meta in ASSETS.rglob("*.meta"):
        match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8-sig"), re.MULTILINE)
        if match:
            existing_guids.add(match[1])
    tree = ET.parse(collection_path)
    assets_node = tree.getroot().find("./ResourceCollection/Assets")
    resources_node = tree.getroot().find("./ResourceCollection/Resources")
    removed_entries = 0
    for node in list(assets_node):
        if node.get("Guid") not in existing_guids or node.get("Guid") in removed_guids:
            assets_node.remove(node)
            removed_entries += 1
    used_resources = {(node.get("ResourceName"), node.get("ResourceVariant", "")) for node in assets_node}
    removed_resources = 0
    for node in list(resources_node):
        if (node.get("Name"), node.get("Variant", "")) not in used_resources:
            resources_node.remove(node)
            removed_resources += 1
    ET.indent(tree, space="  ")
    tree.write(collection_path, encoding="UTF-8", xml_declaration=True)
    report = {"deletedAssets": candidates, "deletedFolders": removed_folders, "removedTableRows": removed_rows,
              "removedResourceAssetEntries": removed_entries, "removedResources": removed_resources,
              "backup": str(archive_path), "tablesToRegenerate": table_names}
    (audit_path.parent / "cleanup_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"deletedAssets": len(paths), "deletedFolders": len(removed_folders),
                      "removedTableRows": removed_rows, "removedResourceAssetEntries": removed_entries,
                      "removedResources": removed_resources, "backup": str(archive_path)}))


if __name__ == "__main__":
    main()
