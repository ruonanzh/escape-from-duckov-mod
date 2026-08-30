#!/usr/bin/env python3
"""Duckov data extractor — extract game data tables + localization from Unity assets.

Usage:
    python3 scripts/extract_data.py <dataDir> <outDir>

    dataDir: Duckov Data dir containing resources.assets
             (Mac: Duckov.app/Contents/Resources/Data)
    outDir:  docs/data

Requires: UnityPy (pip install UnityPy). Maintainer-only tool — players/agents
only grep the generated docs/data/, never run this.

Extracts TextAsset data tables (Items/UIText/Quests/Dialogues/...) from
resources.assets. Each TextAsset has N language variants (en/jp/zh-Hant/zh-Hans);
we pick the richest variant (largest byte size, usually English+Chinese) and
write one CSV per table.
"""
import csv
import io
import json
import os
import sys


def main():
    if len(sys.argv) != 3:
        print("Usage: python3 scripts/extract_data.py <dataDir> <outDir>")
        sys.exit(2)

    data_dir = os.path.expanduser(sys.argv[1])
    out_dir = sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)

    try:
        import UnityPy
    except ImportError:
        print("ERROR: UnityPy not installed. Run: pip install UnityPy", file=sys.stderr)
        sys.exit(1)

    assets_path = os.path.join(data_dir, "resources.assets")
    if not os.path.exists(assets_path):
        print(f"ERROR: {assets_path} not found", file=sys.stderr)
        sys.exit(1)

    env = UnityPy.load(assets_path)

    # Collect TextAssets grouped by name (each name has N language variants)
    tables = {}
    for obj in env.objects:
        if obj.type.name != "TextAsset":
            continue
        data = obj.read()
        text = getattr(data, "m_Script", "") or ""
        name = data.m_Name
        if not text.strip():
            continue
        tables.setdefault(name, []).append(text)

    # Unity-internal / third-party manifests, not game content — skip
    SKIP = {"package", "SceneGuidToPathMap.generated"}

    written = 0
    for name, variants in sorted(tables.items()):
        if name in SKIP or "SceneGuid" in name:
            continue
        # Pick the English variant: exclude Japanese variants (header contains "jp"),
        # then pick the largest remaining (English+Chinese is usually richest).
        non_jp = [v for v in variants if "jp" not in v.lstrip("\ufeff").split("\n")[0].lower()]
        pool = non_jp if non_jp else variants
        richest = max(pool, key=lambda t: len(t.encode("utf-8")))
        rows = parse_csv(richest)

        # Only keep tables that look like key->text localization maps
        if not rows or len(rows) < 2:
            continue

        out_path = os.path.join(out_dir, f"{name}.csv")
        write_csv(out_path, rows)
        written += 1
        print(f"{name}: {len(rows)} rows -> {out_path}")

    print(f"\nDone: {written} tables written to {out_dir}")


def parse_csv(text: str):
    """Parse the TextAsset CSV. First line is a header (key,value,...);
    returns rows as list of lists (without header)."""
    text = text.lstrip("\ufeff")  # strip BOM
    reader = csv.reader(io.StringIO(text))
    rows = []
    for i, row in enumerate(reader):
        if i == 0:
            continue  # skip header
        if row and any(cell.strip() for cell in row):
            rows.append(row)
    return rows


def write_csv(path: str, rows):
    with open(path, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f, lineterminator="\n")
        writer.writerows(rows)


if __name__ == "__main__":
    main()
