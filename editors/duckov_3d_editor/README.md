# Duckov 3D 编辑器（`editors/duckov_3d_editor/`）

**玩家 UI**：在文件栏点任意 `.glb`，右侧栏用 **Unity 坐标系**（+Y 上 · +Z 前）打开它。
契约见 [`docs/mod-repo-guide.md`](../../docs/mod-repo-guide.md) §10。

## 为什么不能"直接 load 就完事" ✗

游戏读 glb 时（`libs/mod-kit/GltfLoader.cs:80-91`）做了四件事：
**顶点 x 取反 · 法线 x 取反 · UV 的 v 取反 · 三角面绕序反转** —— 合起来 = **一次 X 镜像**
（glTF 右手 → Unity 左手）。不做这一步，预览里看到的和进游戏后看到的是**左右相反**的两个模型。

另外两步游戏也在做，编辑器同样跟着做（规则同源）：

| 步骤 | 编辑器 | 游戏 |
| --- | --- | --- |
| 长轴 → 目标轴 | 枪：+Z · 立起来（`up`）：+Y | `OrientToUnity` / `ApplyFrontDeclaration` |
| 枪口朝 +Z | 两端各看 6%/15%，**细**的那端是枪管 | `MuzzleAtPositiveZ` |
| 握把归零 | 枪托端起 8%~35% 的**最低点**移到原点 | `GuessGrip` |

## 界面

- 左上：标题 · 视角按钮（3/4 · 前 +Z · 右 +X · 上 +Y · 后 −Z）· 坐标系说明
- **右下：「游戏朝向」**（玩家要调的东西都在这）
  - **类型**：枪（长轴 → +Z）· 立起来（长轴 → +Y，近战刀用）· 原始（不摆正）
  - **枪口方向：规定就是朝 +Z** ✓ 不给选项（判据同游戏 `MuzzleAtPositiveZ`：两端各看 6%/15%，**细**的那端是枪管）
  - **握把归零** ☑
  - **尺寸**：填目标"最长边（米）"⇒ 等比缩放（配合 `config.json` 的 `size`）
- ⭐ **常开、不给开关**：**Unity 镜像**（= 游戏里看到的样子 ✗ 关掉会与游戏左右相反）· **网格**（每格 10cm）
- 原点白十字 = 游戏里的放置点（握把 / 手抓点 / 挂点基准）

## 技术要点（改之前先看这几条）

- **离线**：宿主 CSP 是 `connect-src 'none'` ⇒ **不能走 CDN**，`three.bundle.js` 是 vendored 的（esbuild 打包）。
- ⚠️ **`delete window.createImageBitmap`**：three 默认用 `ImageBitmapLoader`（`fetch` 读 `blob:`）✗
  会被 CSP 挡 ⇒ 贴图全丢、看着像"模型没材质"。删掉它 ⇒ three 退回 `<img src=blob:>`（走 `img-src` ✓）。
- **读文件**走桥：`editor:ready` → `editor:init` → `editor:readFile` → `editor:fileContent`（base64）。
- ⚠️ **量长轴前必须清零所有变换**：否则读到的是"上一次摆好之后"的包围盒，长轴会在每次重算时来回翻。
- **开关顺序**：`root(缩放) → mirror(X 取反) → grip(归零) → flip(枪口 180°) → orient(front 旋转) → stage(原模型)`。
- 模式是 **`view`（只读）**：改数值请直接改 `config.json`（`edit` 模式属 P2，需要宿主放开跨文件只读）。
