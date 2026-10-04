# archive —— 探索过程的产物（只作参考）

这里的东西是**实现"改/加模型"能力过程中探索出来的**：机制、验证脚本、被放弃的路线、按类别整理的旧式做法。
**保留目的**：以后想回看"当时是怎么做的/为什么这么定"时能查到，也能直接复用其中的代码。

⚠️ **这不是当前路线** —— 当前只做一件事：**告诉 agent 该怎么做**（见仓库根 `AGENTS.md` 与 `.pi/skills/`）。

## 里面的东西

| 路径 | 是什么 | 为什么归档 |
|---|---|---|
| `docs/unity-3d/` | 早期按类别整理的**模型做法文档**（武器/物品/角色/建筑/模型格式 + 真实样例与参数化配方）| 走的是"参数化几何 + YSM 方块模型"路线 ✗ 已被**运行时读 GLB** 取代；但里面的**游戏事实**（挂点/四类零件/骨架家族/坑）仍有参考价值 |
| `skills/model-creator/` | 旧 SKILL：教 agent 用 YSM/零件清单做模型 | 同上，路线已换 |
| `reference/cube_person/` | 方块猫角色 demo（YSM 体素路线，实机跑通过）| 该路线已放弃（1px≈6.25cm 粒度 + 与游戏美术不符 ✗）|
| `reference/bundle_probe/` | 加载 AssetBundle 并渲染的探针 | 使命完成（bundle 我们不再走）|
| `reference/bone_probe/` | 骨骼/骨架研究脚手架 | 一次性研究用 |
| `tools/bundle-pack/` | 离线打 AssetBundle 的工具（改名 CAB / 写网格 / 丢网格 / 对齐 donor）| 主路线不用 bundle ✗；**将来若要做"给别的工具/DCM 用的自包含包"可直接复用** ✓（已验证：能改名、能把 Tripo 网格写进包并在游戏里显示）|

> 归档的东西**仍然能构建** ✓ —— 需要时直接 `dotnet build`（character/bundle demo 需 `export DUCKOV_DIR=<游戏目录>`）。

## 当前路线（一句话）

```
模型（Tripo 出的 GLB，或用图片/文本现生成）→ 放进 mod 目录 → agent 写一行 config
   → 运行时：读 GLB 建 Mesh+贴图 → 克隆游戏自己的 prefab → 换成我们的模型
```
不需要 Unity ✓ 不需要打包成 AssetBundle ✓ 不需要玩家装任何东西 ✓
