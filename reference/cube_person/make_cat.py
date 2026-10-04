#!/usr/bin/env python3
"""从目标骨架生成「方块猫」YSM 模型。

- 骨架：读一份模板（`models/duck_pelvis.json` = 玩家 / `models/duck_hip.json` = NPC），**骨骼名与 pivot 原样保留**
- 方块：按骨骼名模式摆放（Hip/Pelvis、Spine.*、Head、Arm/UpperArm/Elbow/ForeArm、Hand、Leg/Thigh/Foot、Tail）
  → 两套骨架家族都能用（玩家那套是 A 姿势、NPC 那套四肢朝上，都跟随各自的 pivot）
- 单位：模型坐标 = 像素（16 px = 1 m），+y 上、+z 前 —— 与 YSM/Bedrock 一致
- 上色：写进 `texture.fills`（运行时 `PaintYsm` 优先用它，缺省才按骨骼名哈希）

用法： python3 make_cat.py [duck_pelvis|duck_hip]
输出： models/cat_<pelvis|hip>.json
"""
import json, io, sys, os

HERE = os.path.dirname(os.path.abspath(__file__))
PX = 16.0  # 每米像素数


def cube(center_m, size_m):
    """米 → YSM 的 origin/size（整数像素方块；origin = 最小角）"""
    cx, cy, cz = [round(v * PX) for v in center_m]
    sx, sy, sz = [max(1, int(round(v * PX))) for v in size_m]
    return {
        'origin': [cx - sx // 2, cy - sy // 2, cz - sz // 2],
        'size': [sx, sy, sz],
    }


# 身体计划：按骨骼名（模式）给方块。位置都在骨骼 pivot 附近 → 绑定姿势下自然
def body_plan(bone_name, pivot_m):
    """返回该骨骼的方块列表 [(中心(米), 尺寸(米)), ...]"""
    x, y, z = pivot_m
    n = bone_name

    if n in ('Hip', 'Pelvis'):
        # 胯/后腰：下边抬到 0.11，避免和大腿块重叠（重叠 → 游戏里 z-fighting 闪烁）
        return [((x, y + 0.02, z - 0.01), (0.24, 0.12, 0.24))]
    if n.startswith('Spine.0'):
        idx = int(n.split('.')[-1])
        if idx <= 2:
            return [((x, y, z - 0.01), (0.24, 0.09, 0.24))]              # 腰
        if idx == 3:
            return [((x, y, z - 0.01), (0.28, 0.12, 0.24))]              # 胸
        # Spine.004 = 肩所在：给一块"肩桥"把两侧手臂连上（手臂 pivot 在 ±0.27）
        return [((x, y, z - 0.01), (0.30, 0.11, 0.22)),
                ((x, y - 0.01, z), (0.46, 0.105, 0.19))]                 # ← 肩桥（±0.23，接到上臂内侧）
    if n == 'Head':
        return [((x, y + 0.005, z), (0.26, 0.235, 0.25)),                # 头
                ((x, y - 0.04, z + 0.145), (0.11, 0.085, 0.08)),         # 口鼻（朝前 +z）
                ((x + 0.085, y + 0.15, z), (0.06, 0.10, 0.035)),         # 右耳
                ((x - 0.085, y + 0.15, z), (0.06, 0.10, 0.035))]         # 左耳
    if n.startswith('UpperArm') or n == 'Arm.Root.R' or n == 'Arm.Root.L':
        return [((x, y, z), (0.095, 0.11, 0.095))]                       # 上臂 / 肩
    if n.startswith('Elbow') or n.startswith('Arm.Upper'):
        return [((x, y, z), (0.09, 0.095, 0.09))]                        # 肘
    if n.startswith('ForeArm') or n.startswith('Arm.Fore'):
        return [((x, y, z), (0.085, 0.12, 0.085))]                       # 前臂
    if n.startswith('Hand') and not n.startswith('Hand.Soket'):
        return [((x, y, z), (0.09, 0.085, 0.095))]                       # 爪（白）
    if n.startswith('Hand.Soket'):
        return []
    if n.startswith('Thigh') or n.startswith('Leg.Upper'):
        return [((x, y - 0.03, z - 0.01), (0.10, 0.10, 0.105))]          # 大腿（下移，避开胯）
    if n.startswith('Leg.Lower'):
        return [((x, y, z), (0.09, 0.075, 0.09))]                        # 小腿（仅 NPC 骨架有）
    if n.startswith('Foot'):
        return [((x, y, z + 0.03), (0.10, 0.06, 0.17))]                  # 脚（朝前，白）
    if n == 'Tail':
        return [((x, y + 0.01, z - 0.05), (0.085, 0.085, 0.13))]         # 尾根
    if n.startswith('Tail.'):
        return [((x, y + 0.03, z - 0.08), (0.075, 0.075, 0.15))]         # 尾梢（上翘）
    return []


FILLS = {
    'body': '#E0913C', 'head': '#E79C46', 'paw': '#F2E4CE', 'tail': '#D98634',
}


def color_for(bone_name):
    n = bone_name
    if n.startswith('Hand') and not n.startswith('Hand.Soket'): return FILLS['paw']
    if n.startswith('Foot'): return FILLS['paw']
    if n == 'Head': return FILLS['head']
    if n.startswith('Tail'): return FILLS['tail']
    return FILLS['body']


def build(template_path, out_path, identifier):
    src = json.load(io.open(template_path, encoding='utf-8'))
    geo = src['minecraft:geometry'][0]
    bones = geo['bones']

    TEXW = TEXH = 128
    cur_x = cur_y = row_h = 0
    fills, ncubes = {}, 0
    for b in bones:
        name = b['name']
        pivot = [v / PX for v in b['pivot']]          # 像素 → 米
        b['cubes'] = []
        for center, size in body_plan(name, pivot):
            c = cube(center, size)
            w, h, d = c['size']
            rw, rh = 2 * d + 2 * w, d + h
            if cur_x + rw > TEXW: cur_x = 0; cur_y += row_h + 1; row_h = 0
            assert cur_y + rh <= TEXH, f'贴图放不下：{name}'
            c['uv'] = [cur_x, cur_y]
            b['cubes'].append(c)
            cur_x += rw + 1; row_h = max(row_h, rh)
            ncubes += 1
        if name in ('Root',):                          # 根不给方块（会在脚下留个块）
            b['cubes'] = []
        if b['cubes']:
            fills[name] = color_for(name)

    geo['description']['identifier'] = identifier
    geo['description']['texture_width'] = TEXW
    geo['description']['texture_height'] = TEXH
    out = {'format_version': src['format_version'],
           'minecraft:geometry': [geo],
           'texture': {'fills': fills}}
    io.open(out_path, 'w', encoding='utf-8').write(json.dumps(out, ensure_ascii=False, indent=1) + "\n")
    print(f'生成 {out_path}：{len(bones)} 骨 / {ncubes} 方块 / 贴图 {TEXW}²（uv 到 y={cur_y + row_h}）')


if __name__ == '__main__':
    which = sys.argv[1] if len(sys.argv) > 1 else 'duck_pelvis'
    tpl = os.path.join(HERE, 'models', which + '.json')
    out = os.path.join(HERE, 'models', 'cat_' + which.split('_', 1)[1] + '.json')
    build(tpl, out, 'geometry.cat.' + which.split('_', 1)[1])
