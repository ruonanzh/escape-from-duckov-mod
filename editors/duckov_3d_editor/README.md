# Duckov 3D 编辑器（`editors/duckov_3d_editor/`）

**玩家 UI**：在文件栏点任意 `.glb`，右侧栏用 **Unity 坐标系**（+Y 上 · +Z 前）打开它。
契约见 [`docs/mod-repo-guide.md`](../../docs/mod-repo-guide.md) §10。

## 为什么不能"直接 load 就完事" ✗

游戏读 glb 时（`libs/mod-kit/GltfLoader.cs:80-91`）做了四件事：
**顶点 x 取反 · 法线 x 取反 · UV 的 v 取反 · 三角面绕序反转**。

⚠️ **编辑器不得照做** ✗ —— 踩过坑，记下实测（2026-10-12）：

| 编辑器怎么做 | 正对鸭子时，它的右臂在 | 结论 |
| --- | --- | --- |
| 跟着做一次 X 取反 | 画面**右**侧 | 与游戏左右相反 ✗ |
| **不取反** | 画面**左**侧 | 与游戏一致 ✓ |

原因：游戏那边那次取反是**坐标系转换**（glTF 右手 → Unity 左手），
不是显示效果；编辑器里再加上一次，就多翻了一遍。
⚠️ 这个坑只在**不对称模型**上看得出来 —— 枪大多左右对称，所以一直没暴露。

编辑器和游戏**只共用一条规则**（下表）。游戏还在做的两步启发式
（`OrientToUnity` / `ApplyFrontDeclaration`、`MuzzleAtPositiveZ`）**编辑器不做** ✗ ——
按 2026-10-12 定调：三类一律不摆正，原点对原点。

| 步骤 | 编辑器 | 游戏 |
| --- | --- | --- |
| 握把归零 | 枪托端起 8%~35% 的**最低点**移到原点（只有「枪」） | `GuessGrip` |

> 游戏仍会摆正 ⇒ 若模型产出时没摆对，**预览会与游戏不一致**。
> 两条出路见设计仓 [`docs/unity-3d-assets/07-duckov-3d-editor.md`](https://github.com/ruonanzh/desktop-gamer-agent-pi/blob/main/docs/unity-3d-assets/07-duckov-3d-editor.md)。

## 界面

- 左上：标题 · 视角按钮（3/4 · 前 +Z · 右 +X · 上 +Y · 后 −Z）· 坐标系说明
- **右下：「物品」**（玩家要调的东西都在这）
  - **类型**：**枪** · 近战 · 物品
  - **握把归零**：只有**枪**才出现（`GuessGrip` 的假设只对枪成立）
  - **尺寸**：填目标"最长边（米）"⇒ 等比缩放（配合 `config.json` 的 `size`）
- ⭐ **常开、不给开关**：**网格**（每格 10cm）
- 面板最下一行是**顶点 / 三角面**（诊断用）
- **原点白十字（±3.5cm）** = 枪的 0 点要对齐的地方
- **玩家鸭**：常驻参照物，站在地面。类型 = 枪 时，模型会被移到它拿枪的
  `Hand.R/MeleeWeaponSocket` 上（游戏里的挂接关系）

## 材质与光照（"像不像游戏"和"好不好看"都在这）

- ⭐ **材质只用 baseColor + 法线**：金属/光泽**贴图一律丢掉** —— 游戏那边 `GameApi.ApplyOurTexture`
  明写着把 `_MetallicGlossMap` / `_SpecGlossMap` 清成 `null`，所以 glTF 自带的 ORM 贴图**游戏根本不用** ✓
- **metalness / roughness 用常数**（`MAT`）：`0.3 / 0.6`（用户选定 ✓）；调试可用 URL `?mr=0.6,0.4` ✓
  （⚠️ "银"只能靠 metalness 表现 ⇒ 设成 0 会让银色件变**白灰** ✗）
  ⚠️ 游戏那个值**不是常数** ✗：`BorrowMaterial()` 是从**场上第一个用 `SodaCraft/SodaLit` 的材质克隆**来的 ⇒ 离线无法精确复现 ✓
- ⭐ **自建摄影棚环境贴图**（`buildStudioEnv`）：深色底 + 顶柔光/侧暖光/背冷光
  ⇒ 金属面出现**渐变反射与高光条** ✓（用 three 自带的 `RoomEnvironment`（全白房间）会把金属**洗白** ✗）
- **三点光**：key ✓ · fill（偏冷 ✓）· rim（勾轮廓 ✓）+ `ambient` 只兜底 ✓
- ⚠️ **不开阴影** ✗（用户："影子没必要"）—— `shadowMap` 关闭 ✓ 无 `castShadow`/阴影地面 ✓ 只靠明暗与轮廓 ✓

## 技术要点（改之前先看这几条）

- **离线**：宿主 CSP 是 `connect-src 'none'` ⇒ **不能走 CDN**，`three.bundle.js` 是 vendored 的（esbuild 打包）。
- ⚠️ **`delete window.createImageBitmap`**：three 默认用 `ImageBitmapLoader`（`fetch` 读 `blob:`）✗
  会被 CSP 挡 ⇒ 贴图全丢、看着像"模型没材质"。删掉它 ⇒ three 退回 `<img src=blob:>`（走 `img-src` ✓）。
- **读文件**走桥：`editor:ready` → `editor:init` → `editor:readFile` → `editor:fileContent`（base64）。
- ⚠️ **量长轴前必须清零所有变换**：否则读到的是"上一次摆好之后"的包围盒，长轴会在每次重算时来回翻。
- **开关顺序**：`root(缩放) → mirror(X 取反) → grip(归零) → flip(枪口 180°) → orient(front 旋转) → stage(原模型)`。
- 模式是 **`view`（只读）**：改数值请直接改 `config.json`（`edit` 模式属 P2，需要宿主放开跨文件只读）。
