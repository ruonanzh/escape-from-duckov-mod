# 05 · 社区生态（做模型前先看能不能复用）

> 来源：Steam 创意工坊（appid 3167020）+ 社区仓库，2026-10-02 实测。

## 结论：分三块，做法完全不同

| 需求 | 社区事实标准 | 我们怎么做 |
|---|---|---|
| **角色 / 宠物 / NPC 换模型** | **DCM（Duckov Custom Model）** + **YSM 模型**（`ysm.json`，**文本 JSON 几何**，带动画轮盘）| **复用**（见下）|
| **物品 / 武器 数据与行为** | `item.yaml` / `recipe.yaml` **声明式框架** | 交给框架或我们的 C#；**模型**是本目录负责的部分 |
| **物品 / 武器 模型** | 现成 mod 几乎都**自带 AssetBundle**（Unity 打；`config.json` 里 `BundleFile`/`PrefabName`）| 我们走 [`README.md`](README.md) 的 **① 代码参数化生成** |

## 1. DCM + YSM（角色类的事实标准，可直接复用）

- 仓库：`github.com/Duckov-Custom-Model/DuckovCustomModel`（**MIT**，v2.0.1 起，仍在更新）｜文档站：`duckov-custom-model.ritsukage.com`｜SDK：`DuckovCustomModel-SDK`（Unity 包，建 Mod DLL / 打 AssetBundle）。
- 依赖：需要 **HarmonyLib** 前置；模型 mod 常以 DCM 为前置（如「明日方舟铃兰模型替换 YSM Suzuran」）。
- **模型放置**：`.ysm` 文件或含 `ysm.json` 的文件夹 → 放进 `<游戏>/ModConfigs/DuckovCustomModel/Models`；**不需要 AssetBundle**（原有 bundle 模型的加载流程保留）。
- **目标**：`built-in:Character` / `built-in:Pet` / `built-in:AICharacter_*`（`*` = 全部 AI）；**可通过 SDK 注册扩展目标**（`extension:` 前缀）。
- **游戏内**：`\` 开模型界面；模型带**动作轮盘**（按 `Z`；分页/分类/循环/打断），支持 **Molang**（`query.*` / `ysm.*` 如 `ysm.food_level`）。
- **侧车配置**（`<模型名>.ysm.duckov.json`，同目录）：
  ```json
  { "Name": "自定义模型", "ModelTarget": "player/main",
    "TargetTypes": ["built-in:Character", "built-in:AICharacter_*"],
    "Scale": 1.0, "HeadPitchLimit": 45, "HeadYawLimit": 85,
    "LocatorMappings": { "RightHandLocator": "模型内实际定位器名" },
    "BundlePath": "props.assetbundle", "DeathLootBoxPrefabPath": "Assets/Props/Corpse.prefab" }
  ```
- 生态规模（工坊条目数）：`模型替换` **402**、`DCM` **10**、`YSM` **5**。

## 2. 物品/武器：声明式框架管数据，**不管模型**

「自定义你的物品（框架mod）」等：用 **`item.yaml` + `recipe.yaml`** 定义武器与配方 —— 数值/品质、口径、开火方式、装弹方式、**开火特效与弹道**、**自定义贴图**、插槽筛选、自定义子弹。
框架作者明确写了 **“不会改模型”** —— **这正是我们要补的空白**：数据/行为用框架，**几何**用本目录的做法。

## 3. 物品模型 mod 的现实做法（与我们不同）

工坊里「(带模型) …」类条目（三角洲收藏品、hololive 手办、蔚蓝档案玩偶、M200、C96…）几乎都是：
- 自带 `bundles/<name>` + `.manifest` + `config.json`（`BundleFile` / `PrefabName` / 克隆来源 / 数值 / 特效参数）；
- 用 **Unity 2022.3.62f2** 打包（版本与游戏一致，**不可跨版本**），材质走 **URP**。

→ 我们走参数化生成**不依赖 Unity**，代价是形状偏程序化；需要"真实感模型"时才回落到这条。

## 4. 还没覆盖 / 待议

- **武器特效（`MuzzleFx` / `Trail`）**：现成 mod 在 config 里给参数数组，我们暂不覆盖。
- **贴图**：社区 mod 都带贴图/图标；YSM 模型自带贴图目录 —— 我们目前只用纯色。
- **扩展目标**：DCM SDK 可注册 `extension:` 目标类型（例如物品类），**未验证**能否直接用于我们的场景。
- **手办 / 收藏品 / 玩偶**是「带模型」最大宗的应用场景 —— 参数化（盒体+旋转体拼小物件）恰好合适。

## 参考链接

- DCM 仓库 <https://github.com/Duckov-Custom-Model/DuckovCustomModel>｜文档 <https://duckov-custom-model.ritsukage.com>
- YSM 使用说明（DCM 仓库 `docs/YSM_USAGE.md`）
- 社区 mod 示例库 <https://github.com/xvrsl/duckov_modding>（本 repo `docs/mod-api.md` 的来源）
