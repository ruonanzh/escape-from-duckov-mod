# Duckov 配置编辑器（`editors/duckov_config_editor/`）

**玩家 UI**：在文件栏点任意 `config.json`，右侧栏打开**表单**，改完 **Ctrl+S** 保存。
契约见 [`docs/mod-repo-guide.md`](../../docs/mod-repo-guide.md) §10（在**设计仓** `desktop-gamer-agent-pi`）。

## 为什么单独做一个编辑器（不塞进 3D 编辑器）

契约 v1 有一条硬规则：**读/写都只针对「打开的那个文件」**。

- 3D 编辑器打开的是 `.glb`，它读写 `.glb`。
- 配置在 `config.json`，要读写它，就得**打开它**。

所以两件事天然落在两个文件上，**各做一个编辑器**。
这也让两边都能真正保存（同文件），不需要任何产品改动。

## 字段与 mod-kit 对齐

改字段前先看 `libs/mod-kit` 那边的说明。**判据一份**。

| 字段 | 默认 | 说明 |
| --- | --- | --- |
| `typeIDs` | — | 按 typeID（数组，**推荐**） |
| `targets` | — | 或按对象名**全等**（数组，不区分大小写） |
| `model` | — | 本 mod 目录下的 GLB |
| `front` | `auto` | 只给**自带模型**：`auto`/`+z`/`-z`/`+x`/`-x`/`up` |
| `size` | — | 目标**最长边**（米）。物品/装备不填；枪不填按游戏里的枪种尺寸 |
| `icon` | `icon.png` | 武器图标文件名 |
| `world` | **开** | 换世界里的模型 |
| `handheld` | 关 | 连拿在手里那条也换（**会改行为**） |
| `slots` | — | 只有**枪**用：`[x,y,z]`，单位**米**，在**我们模型的局部空间**里，可负（枪的这个空间经过**握把归零**，零点在**握把点**）。`x` 沿 X（左右）· `y` 沿 Y（上下）· `z` 沿 Z（枪口方向）。游戏挂点搬到我们模型上 |
| `slots.pivot` | — | 有它就用它当**对齐基准**（比包围盒映射准） |
| `debug` | 关 | 打详细日志 |

**写盘时会省略等于默认值的字段**（例如 `front: "auto"` 不写），**未知字段原样保留**。

## 条目模式（config 里有 `entries` 时）

一个 mod **可以混装多条**（换武器外观一条、换物品外观一条、新增武器一条），所以 `entries[]` 里每条自带一个 `action`。
编辑器看到 `entries` 就自动换成条目模式：一行一个动作，下面按动作换字段表。

| `action` | 编辑器支持吗 | 字段 |
| --- | --- | --- |
| `replace_weapon` | 支持 | 上表那套（缺省值，等于老写法）|
| `replace_item` | 支持 | 同上 |
| `new_weapon` | 支持 | `newTypeId` / `cloneFromItemId` / `displayName` / `description` / `model` / `icon` / `size` / `merchantID` / `maxStock` / `priceFactor` / `onSale` / `quality` / `displayQuality` / `stats` / `crafting` |
| `new_item` | **不支持**（还没实现）| 写进 config 会被 `validate_mod` 直接报错。编辑器把它当未知 action，原样保留不改动 |

**三条都能用 `slots`**（写法与上面一样）。

**两个写法的关系**：

- 没有 `entries` 的旧 config **照原样渲染**（就是上面那个老表单），行为一字不变。
- 上方的「**改成多条（entries）**」按钮把当前表单包成一条 `entries`，于是可以慢慢长出多条。
- 切 `action` 不会丢已填的值（切走再切回，`newTypeId` 那些还在）。

**两个字段的写法**（都是“每行一个”，好手写也好粘贴）：

```
stats     每行 `Damage=13.0`（键用游戏自己的名字，值用绝对值）
crafting  上面一行 money 与 tags，下面材料每行 `662=5`；三样都空就不写 crafting（写了空对象会被当成“要加配方”）
```

## 界面

- **扁平模式**（旧 config）：换哪件 / 用哪个模型 / 换哪一层 / 挂点 / 诊断
- **条目模式**（有 `entries`）：每条一张卡片（下拉选动作 + 字段表 + 挂点），可加条、可删条
- **挂点**：表（键 + `[x,y,z]`），可加行、可删行。只有枪用
- **诊断**：`debug` + 「其他字段」折叠区（未识别的键原样显示，需要手改 JSON 时看这里）
- 底栏：状态 · 重新加载 · 保存（Ctrl+S）
- `CONFLICT`（别处改过文件）会**拒绝保存并提示**，不会静默覆盖

## 沙箱约束（不得放宽）

- CSP `connect-src 'none'`，**不联网、不 fetch 任何东西**。
- 全部数据经宿主 `postMessage` 桥。
- 只读写**打开的那个文件**。不在 `your_mods/` 下的文件，宿主会回 `READ_ONLY`。
