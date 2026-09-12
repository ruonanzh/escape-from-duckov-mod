---
name: mod-authoring
description: Escape From Duckov 的 C# mod 制作、修改、可行性/制作方法解释及编译排错。涉及 csproj、ModBehaviour、info.ini、DLL 或 validate_mod 时按需读取；咨询不要求创建文件，实际写入取决于 session 权限。
---

# 做 mod（Escape From Duckov）

一个 mod 是 `your_mods/<mod名>/` 目录，含：

- `<ModName>.csproj` — 编译配置
- `ModBehaviour.cs` — mod 入口类（继承 `Duckov.Modding.ModBehaviour`）
- `info.ini` — mod 元信息
- `preview.png` — 256×256 预览图（Workshop 上传用，可选）

编译成 DLL，用 `validate_mod` 工具校验。

## 使用方式与条件分支

本技能提供领域方法，不授予权限；Game Helper 可解释方法、通过获准工具校验已有 mod，不能因此创建目录或修改源码。以下路径均相对 workspace 根目录（不是技能目录）。

- **咨询/可行性**：按目标检索 `docs/api/`、`docs/data/` 与 `docs/mod-api.md`，确认 API/数据依据；只是讨论时不必建目录或安装环境。数据层版本见 `mod-repo.json` 的 `game.version`，不臆测最新补丁行为。
- **实际制作/修改**：写入仅限当前 session 绑定目录。无绑定且准备写入时才调 `create_mod_folder`，选择合法 C# 标识符（通常 PascalCase）；有绑定就复用，目录缺失先说明阻塞，不另建第二个绑定。
- **规范与实现**：复用 `reference/example_mod/ExampleMod.csproj` 与入口类。保持 info.ini 名称、AssemblyName、RootNamespace 和 DLL 名一致（validate_mod 校验）。
- **需要编译而环境未知/已变化**：用 `check_runtime` 核实 dotnet SDK 与游戏目录。有仍有效的结果无需每轮重复检查。缺 SDK 才取 `install_runtime` 安装指引；找不到游戏目录则确认安装位置，不用 SDK 安装解决。工具提供指引不等于已安装。
- **验证产物**：`validate_mod` 做字段检查，并在环境就绪时执行编译；不必再机械地重复同一次 `dotnet build`。若需手工排错，使用返回的运行时路径与游戏目录，不依赖偶然的 PATH。
- **装进游戏**：`install_mod` 把产物复制到游戏 `Mods/` 目录（目标来自 `check_runtime` 的发现结果，它不自己探测）。同一个 mod 重复安装是原地更新；目标目录若已被**别的 mod** 占用会直接 FAIL —— 本类型 mod 身份 = `info.ini` 的 `name` = C# 命名空间，编进 DLL，**改目录名解决不了冲突**，必须改 mod 名（info.ini + csproj AssemblyName/RootNamespace + 命名空间）后重新校验再装，且不要覆盖别人的内容。
- **失败处理**：按错误定位修复；环境缺失/网络阻塞或同类失败重复出现时先解决前置条件，不无限“直到 PASS”。

## info.ini

```ini
name = MyMod
displayName = 我的 Mod
description = 这个 mod 做了什么

tags = Quality of Life
version = 1.0
```

- `name`：mod 名，= 命名空间 = dll 文件名，合法 C# 标识符（validate_mod 校验）。
- `displayName`：显示名（validate_mod 校验非空）。
- `description`：描述（validate_mod 校验非空）。
- `tags`：Workshop 标签，逗号分隔，可选值见下。
- `version`：版本号。
- `publishedFileId`：Workshop 的 mod ID（上传后回填）。

### tags 可选值

Weapon / Equipment & Gear / Loot & Economy / Quality of Life / Cheats & Exploits / Visual Enhancements / Sound / Quest & Progression / Companion & NPC / Collectibles / Gameplay / Multiplayer & Co-op / Utility

## csproj 要点

- `TargetFramework = netstandard2.1`
- `AssemblyName` / `RootNamespace` = mod 名（和 info.ini 的 name 一致）
- 引用游戏 DLL（`TeamSoda.*`、`ItemStatsSystem.dll`、`Unity*`，`Private=false`）+ `libs/0Harmony.dll`（`Private=true`）
- 游戏目录用 `$(DUCKOV_DIR)` 环境变量注入（check_runtime 发现后写进状态文件，编译时导出）

## ModBehaviour

mod 入口类，继承 `Duckov.Modding.ModBehaviour`：

```csharp
using UnityEngine;

namespace MyMod
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        void Awake() { Debug.Log("MyMod loaded!"); }
    }
}
```

命名空间通常跟 mod 名一致，类名通常叫 `ModBehaviour`（游戏按 `<name>.ModBehaviour` 加载）。

## validate_mod 工具用法

调 `validate_mod` 工具，参数 `modDir = your_mods/<mod名>/`。

- `PASS: <名> is valid` 表示本工具所需检查通过；仍不代表游戏内加载/玩法已经验证。
- `FAIL` 表示校验失败；`PARTIAL` 表示有检查未执行（例如缺 SDK 跳过编译）。依据输出文本的 `PASS`/`FAIL`/`PARTIAL` 和 `NEXT:` 行说明实际完成范围，不把旧 DLL 的存在当作本次编译成功。
- 输出含 `FAIL` 时不能声称 mod 已全部验证；最终报告实现效果、实际验证和剩余步骤。

## 常见错误（对照修正）

- `FAIL: info.ini: name is missing` → info.ini 缺 name，补上。
- `FAIL: info.ini: name (...) is not a valid namespace` → name 含非法字符，改成合法 C# 标识符（PascalCase）。
- `FAIL: missing ModBehaviour.cs` → 缺入口类，创建继承 `Duckov.Modding.ModBehaviour` 的 `ModBehaviour` 类。
- `FAIL: dotnet build failed: ...` → 编译错误，读报错定位（多半是 API 用法/引用问题）。
- `FAIL: missing <ModName>.dll` → 编译没产出 dll，先跑 dotnet build。
- `WARN: dotnet not found` → 用 `check_runtime` 核实，按 `install_runtime` 返回的指引准备 SDK；再次检查确认后才能编译。工具本身不执行安装。
- `WARN: missing preview.png` → 缺预览图（不影响本地加载，Workshop 上传需要 256×256）。

## 参考

- 完整可编译样例：`reference/example_mod/`。
- API：`docs/mod-api.md`；物品：`docs/items.md`。
