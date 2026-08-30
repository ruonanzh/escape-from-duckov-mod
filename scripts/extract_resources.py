#!/usr/bin/env python3
"""Duckov resource lister — list game assets (textures/sprites/meshes/audio/materials)
so an agent knows which assets exist and can plan replacements/additions.

Usage:
    python3 scripts/extract_resources.py <dataDir> <outDir>

    dataDir: Duckov Data dir (Duckov.app/Contents/Resources/Data)
    outDir:  docs/data

Requires: UnityPy. Maintainer-only tool — players/agents only grep the generated
docs/data/resources.csv, never run this.

The game uses Unity's Resources system (no Addressables/AssetBundles), so assets
are referenced by m_Name. We list type + m_Name for asset kinds a mod might
replace or add: textures, sprites, meshes, audio, materials, shaders, animations.
"""
import csv
import os
import sys

# Asset types a mod author cares about for replacement/addition.
ASSET_TYPES = {
    "Texture2D",
    "Sprite",
    "Mesh",
    "AudioClip",
    "Material",
    "Shader",
    "AnimationClip",
    "Font",
    "RenderTexture",
    "VideoClip",
}


def main():
    if len(sys.argv) != 3:
        print("Usage: python3 scripts/extract_resources.py <dataDir> <outDir>")
        sys.exit(2)

    data_dir = os.path.expanduser(sys.argv[1])
    out_dir = sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)

    try:
        import UnityPy
    except ImportError:
        print("ERROR: UnityPy not installed. Run: pip install UnityPy", file=sys.stderr)
        sys.exit(1)

    # Scan all .assets files (resources.assets + sharedassets*.assets)
    asset_files = sorted(
        f for f in os.listdir(data_dir) if f.endswith(".assets") and not f.endswith(".resS")
    )

    rows = {}  # (type, name) -> 1, dedup across files
    for af in asset_files:
        path = os.path.join(data_dir, af)
        try:
            env = UnityPy.load(path)
        except Exception as e:
            print(f"WARN: cannot load {af}: {e}", file=sys.stderr)
            continue

        count = 0
        for obj in env.objects:
            if obj.type.name not in ASSET_TYPES:
                continue
            try:
                name = obj.read().m_Name
            except Exception:
                name = "(unreadable)"
            if not name:
                continue
            rows[(obj.type.name, name)] = 1
            count += 1
        print(f"{af}: {count} asset objects")

    out_path = os.path.join(out_dir, "resources.csv")
    with open(out_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f, lineterminator="\n")
        writer.writerow(["type", "name"])
        for (typ, name) in sorted(rows.keys()):
            writer.writerow([typ, name])

    print(f"\nDone: {len(rows)} unique assets -> {out_path}")


if __name__ == "__main__":
    main()
