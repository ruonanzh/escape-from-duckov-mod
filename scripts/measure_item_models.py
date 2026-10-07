#!/usr/bin/env python3
"""量出游戏里物品**模型包**的真实尺寸（离线 ✓ 只读 ✓ 不启动游戏 ✓）。

用途：给"替换物品模型"写 best-practice 提示词时，需要知道**游戏那把模型多大**，
     才能让 Tripo/Blender 出的模型尺寸对得上（我们全程真实米制 ✓ 不缩放 ✓）。

链路（实测走通 ✓）：
    Item(typeID) → itemGraphic(ItemGraphicInfo) → m_GameObject
      → 组件里找 CharacterSubVisuals（装备类走这个 ✓）/ 或直接找 MeshFilter（普通物品 ✓）
      → renderers[] / m_Mesh → Mesh → m_SubMeshes[].localAABB(m_Center, m_Extent)
    ⇒ 尺寸 = 2 × m_Extent（模型本地尺度；再乘上变换链的缩放 —— 装备类实测基本是 1 ✓）

用法：
    python3 scripts/measure_item_models.py 43 1367 26 679 36 40
    （不给参数就用内置的一组常用物品 ✓）

⚠️ 维护者脚本（AGENTS.md 里 scripts/ 的定位 ✓）：玩家/agent 不需要跑它。
"""
import re
import subprocess
import sys
import os
from functools import lru_cache

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DUCKOV = os.environ.get(
    "DUCKOV_DIR",
    os.path.expanduser("~/Library/Application Support/Steam/steamapps/common/Escape from Duckov"),
)
MANAGED = os.path.join(DUCKOV, "Duckov.app/Contents/Resources/Data/Managed")
DATA = os.path.join(DUCKOV, "Duckov.app/Contents/Resources/Data")
PROBE = os.path.join(ROOT, "tools/data-probe/bin/Release/net8.0/data-probe.dll")


@lru_cache(maxsize=4096)
def dump(path_id: int, depth: int = 3) -> str:
    """dump 一个资源（带缓存 ✓ 同一 pathID 只查一次 ✓）。"""
    out = subprocess.run(
        ["dotnet", PROBE, "--managed", MANAGED, "--data", DATA,
         "--action", "dump", "--pathid", str(path_id), "--file", "resources.assets",
         "--depth", str(depth)],
        capture_output=True, text=True,
    )
    return out.stdout


def pptrs(block: str) -> list:
    """从一段文本里取出所有 `pathID N`。"""
    return [int(m) for m in re.findall(r"pathID (\d+)", block)]


def section(text: str, start: str, end: str = None) -> str:
    i = text.find(start)
    if i < 0:
        return ""
    if end:
        j = text.find(end, i + len(start))
        if j > 0:
            return text[i:j]
    return text[i:]


@lru_cache(maxsize=2048)
def item_pathid(type_id: int) -> int:
    """typeID → 资产 pathID（`--action dump --class Item --typeid N` 打的那个 ✓）。"""
    out = subprocess.run(
        ["dotnet", PROBE, "--managed", MANAGED, "--data", DATA,
         "--action", "dump", "--class", "Item", "--typeid", str(type_id),
         "--file", "resources.assets", "--depth", "1"],
        capture_output=True, text=True,
    ).stdout
    m = re.search(r"pathID (\d+)", out)
    return int(m.group(1)) if m else 0


def type_name(text: str) -> str:
    m = re.search(r"=== (\S+)", text)
    return m.group(1) if m else "?"


def aabb_of_mesh(mesh_id: int) -> list:
    """取 Mesh 的所有子网格 localAABB → 返回 [(center, extent), …]。"""
    t = dump(mesh_id, 6)
    boxes = []
    for m in re.finditer(r"localAABB(.*?)(?=localAABB|m_Shapes|$)", t, re.S):
        seg = m.group(1)
        c = re.search(r"m_Center(.*?)m_Extent", seg, re.S)
        e = re.search(r"m_Extent(.*)", seg, re.S)
        if not (c and e):
            continue
        def vec(s):
            xs = re.findall(r"[xyz] = (-?[0-9.]+(?:E[+-]?[0-9]+)?)", s)
            out = []
            for v in xs[:3]:
                try:
                    out.append(float(v))
                except ValueError:      # `8.940697E` 这类被截断的 ✓ 当 0 ✓（AABB 里基本都是极小值 ✓）
                    out.append(0.0)
            return out if len(out) == 3 else None
        cv, ev = vec(c.group(1)), vec(e.group(1))
        if cv and ev:
            boxes.append((cv, ev))
    return boxes


def merge(boxes):
    if not boxes:
        return None
    mn = [min(c[i] - e[i] for c, e in boxes) for i in range(3)]
    mx = [max(c[i] + e[i] for c, e in boxes) for i in range(3)]
    return [mx[i] - mn[i] for i in range(3)]


@lru_cache(maxsize=4096)
def node_scale(tf_id: int) -> float:
    """从某个 Transform 一路往上乘 `m_LocalScale`（最多 12 层 ✓）。"""
    scale = 1.0
    cur = tf_id
    for _ in range(12):
        t = dump(cur, 1)
        m = re.search(r"m_LocalScale(.*?)(?=m_Children|m_Father|m_GameObject|$)", t, re.S)
        if m:
            vals = re.findall(r"[xyz] = (-?[0-9.]+(?:E[+-]?[0-9]+)?)", m.group(1))
            got = []
            for v in vals[:3]:
                try:
                    got.append(abs(float(v)))
                except ValueError:
                    got.append(1.0)
            if got and all(v > 1e-6 for v in got):
                for v in set(got):          # 三个轴取"最像的那个"（非等比缩放极少见 ✓）
                    scale *= v
                break
        f = re.search(r"m_Father -> pathID (\d+)", t)
        if not f or int(f.group(1)) == 0:
            break
        cur = int(f.group(1))
    return scale


def mesh_ids_of_graphic(go_id: int) -> list:
    """一个图形 GameObject → [(Mesh, 该 mesh 所在节点的 Transform), …]（要拿 Transform 乘缩放 ✓）。"""
    t = dump(go_id, 4)
    comps = pptrs(section(t, "m_Component", "m_Layer"))
    meshes = []

    def tf_of(components: list) -> int:
        for x in components:
            if type_name(dump(x, 1)) == "Transform":
                return x
        return 0

    for c in comps:
        ct = dump(c, 5)                                         # ⚠️ 要 depth 5，数组才展开 ✓（depth 2 只有 AssetsTools.NET.AssetTypeArrayInfo ✗）
        n = type_name(ct)
        if n == "MeshFilter":                                   # 普通物品：直接挂 MeshFilter ✓
            mid = re.search(r"m_Mesh -> pathID (\d+)", ct)
            if mid:
                meshes.append((int(mid.group(1)), tf_of(comps)))
        elif n == "CharacterSubVisuals":                        # 装备类：renderers[] ✓
            for r in pptrs(section(ct, "renderers", "particles"))[:8]:
                rt = dump(r, 2)
                rgo = re.search(r"m_GameObject -> pathID (\d+)", rt)
                if not rgo:
                    continue
                inner = pptrs(section(dump(int(rgo.group(1)), 4), "m_Component", "m_Layer"))
                for ic in inner:
                    ict = dump(ic, 2)
                    if type_name(ict) == "MeshFilter":
                        mid = re.search(r"m_Mesh -> pathID (\d+)", ict)
                        if mid:
                            meshes.append((int(mid.group(1)), tf_of(inner)))
    return meshes


def measure(type_id: int):
    pid = item_pathid(type_id)
    if pid == 0:
        return "?", None, "找不到这个 typeID ✗"
    it = dump(pid, 2)
    name = type_name(it)
    g = re.search(r"itemGraphic -> pathID (\d+)", it)
    if not g:
        return name, None, "没有 itemGraphic ✗"
    gi = dump(int(g.group(1)), 3)
    go = re.search(r"m_GameObject -> pathID (\d+)", gi)
    if not go:
        return name, None, "图形没有 GameObject ✗"
    meshes = mesh_ids_of_graphic(int(go.group(1)))
    if not meshes:
        return name, None, "没找到画出来的 Mesh ✗"
    boxes = []
    for mid, tf in meshes:
        k = node_scale(tf) if tf else 1.0
        for c, e in aabb_of_mesh(mid):
            boxes.append(([c[i] * k for i in range(3)], [e[i] * k for i in range(3)]))
    size = merge(boxes)
    extra = " ×缩放" if any(tf and abs(node_scale(tf) - 1) > 0.01 for _, tf in meshes) else ""
    return name, size, f"{len(meshes)} 个 mesh / {len(boxes)} 个 submesh{extra}"


def main():
    ids = [int(a) for a in sys.argv[1:]] or [43, 104, 1312, 1367, 1141, 26, 973, 1650, 679, 1252, 36, 40]
    print(f"{'typeID':>7}  {'名称':<34} {'尺寸 (X,Y,Z) m':<26} 备注")
    print("-" * 100)
    for tid in ids:
        try:
            name, size, note = measure(tid)
        except Exception as ex:                                   # noqa: BLE001
            name, size, note = "?", None, f"出错：{ex}"
        if size:
            s = f"{size[0]:.3f}, {size[1]:.3f}, {size[2]:.3f}"
            longest = max(size)
            print(f"{tid:>7}  {name:<34} {s:<26} 最长边 {longest:.3f} m · {note}")
        else:
            print(f"{tid:>7}  {name:<34} {'—':<26} {note}")


if __name__ == "__main__":
    main()
