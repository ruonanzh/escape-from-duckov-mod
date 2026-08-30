---
name: mod-authoring
description: 在 Escape From Duckov 的 modding 环境里创建、编译、校验 C# mod（your_mods/<mod名>/ 下的 csproj + ModBehaviour.cs + info.ini）。当任务是在本环境做 mod、编译 DLL、跑 lint 校验 mod 时使用。
---

# 做 mod（Escape From Duckov）

在 `your_mods/<mod名>/` 下做一个 C# mod，编译成 DLL，用 `lint/check_mod.mjs` 校验。

## 唯一可写目录

`your_mods/<mod名>/` 是唯一可写目录。环境的 `docs/`、`specs/`、`lint/`、`tools/`、`reference/`、`libs/` 一律只读，别把中间文件写回去。

## 步骤

1. 先调 `check_runtime` 工具确认环境就绪（dotnet SDK + 游戏目录）。缺失则调 `install_runtime` 工具。
2. 读 `specs/mod-spec.md`（info.ini schema + 命名）和 `docs/mod-api.md`（可用 API）。
3. 在 `your_mods/<mod名>/` 下创建 `info.ini`。
4. 创建 `<ModName>.csproj`（参考 `reference/example_mod/ExampleMod.csproj`，改 `AssemblyName`/`RootNamespace` 为你的 mod 名）。
5. 创建 `ModBehaviour.cs`（继承 `Duckov.Modding.ModBehaviour`，命名空间 = mod 名）。
6. 编译：`dotnet build <ModName>.csproj -c Release`（需先设 `DUCKOV_DIR` 环境变量为游戏目录；或直接跑 lint，它自动编译）。
7. 校验：`node lint/check_mod.mjs your_mods/<mod名>/`，直到输出 `PASS`（exit 0）。

## info.ini

```ini
name = MyMod
displayName = 我的 Mod
description = 这个 mod 做了什么

tags = Quality of Life
version = 1.0
```

- `name`：必填，= 命名空间 = dll 文件名，合法 C# 标识符（PascalCase）。
- `displayName`、`description`：必填。
- `tags`、`version`、`publishedFileId`：可选。

## csproj 要点

- `TargetFramework = netstandard2.1`
- `AssemblyName` / `RootNamespace` = mod 名（和 info.ini 的 name 一致）
- 引用游戏 DLL（`TeamSoda.*`、`ItemStatsSystem.dll`、`Unity*`，`Private=false`）+ `libs/0Harmony.dll`（`Private=true`）
- 游戏目录用 `$(DUCKOV_DIR)` 环境变量注入（check_runtime 发现后写进状态文件，编译时导出）

## ModBehaviour

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

## lint 用法

```bash
node lint/check_mod.mjs your_mods/<mod名>/
```

- exit `0`：通过，打印 `PASS: <名> is valid`
- exit `1`：失败，逐条打印 `FAIL: <原因>`（英文），直接定位缺失/非法字段或编译错误
- exit `2`：用法错误（参数个数不对）

## 常见错误（对照修正）

- `FAIL: info.ini: name is missing` → info.ini 缺 name，补上。
- `FAIL: info.ini: name (...) is not a valid namespace` → name 含非法字符，改成合法 C# 标识符（PascalCase）。
- `FAIL: missing ModBehaviour.cs` → 缺入口类，创建继承 `Duckov.Modding.ModBehaviour` 的 `ModBehaviour` 类。
- `FAIL: dotnet build failed: ...` → 编译错误，读报错定位（多半是 API 用法/引用问题）。
- `FAIL: missing <ModName>.dll` → 编译没产出 dll，先跑 dotnet build。
- `WARN: dotnet not found — skip compile check` → 调 `check_runtime` / `install_runtime` 工具装 dotnet。
- `WARN: missing preview.png` → 缺预览图（不影响本地加载，Workshop 上传需要 256×256）。

## 参考

- 完整可编译样例：`reference/example_mod/`。
- 字段定义：`specs/mod-spec.md`；API：`docs/mod-api.md`；物品：`docs/items.md`。
