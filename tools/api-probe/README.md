# api-probe —— 只读检查托管 DLL（给 agent 用）

当 `docs/api/`（维护者反射 dump 的**公开**签名）不够时，用本探针读游戏托管 DLL 的
**私有成员 / 反编译 C# / IL / 字符串字面量**。

**只读**：用 ILSpy 的 `ICSharpCode.Decompiler` **读取**程序集文件，**不加载、不执行**目标 DLL。

## 用法

```bash
dotnet run --project tools/api-probe -- \
  --managed "<game>/.../Managed" --dll "<name[,name]|path|*>" --action <search|members|decompile|il|strings> \
  [--target <type-or-substring>] [--member <method>] [--limit N] [--offset N]
```

| action | 作用 |
|---|---|
| `search` | 按名字（子串，忽略大小写）找**类型/成员**（每次最多 500 条，`--offset` 翻页）|
| `members` | 某类型的**全部成员（含 private）+ 基类** |
| `decompile` | 反编译**类型**（或 `--member` 指定方法/字段）为 **C#** |
| `il` | 反编译类型/方法的 **IL** |
| `strings` | 扫程序集里的**字符串字面量**（每次最多 500 条，`--offset` 翻页）|

- `--dll`：**必填**。给 DLL 名（不带 `.dll`）、逗号分隔多个、**通配**（`TeamSoda.*`、`Unity*.dll`）、绝对路径，
  或 `*`（`--managed` 下所有**非框架** DLL）。
  本探针**不自带默认值**：它不得知道任何具体游戏的程序集名（本文件会被复制到别的游戏仓）。
  `agent` 侧的 `inspect_game_api` 默认按本仓配置的 `compile.references` 拼出这串（会排除 `Unity*.dll`）。
- **`*` 很宽**：它是"非框架"，会连 DOTween / Odin / UniTask 这些插件一起扫（实测 duckov 的 Managed 下 70 个）。
  想只扫游戏自己的程序集，用通配（`--dll 'TeamSoda.*,ItemStatsSystem'`）。

**`--managed` 是程序集解析根，不只是"去哪找 dll"**：它的作用是 `UniversalAssemblyResolver` 的搜索根，
基类与依赖类型都从这里解析。若目标程序集**不在**该目录（例如来自 NuGet 缓存），正确写法是

```
--managed <游戏数据目录>/Managed     ← 永远指向游戏目录
--dll     <目标 dll 的绝对路径>       ← Path.Combine 遇绝对路径原样返回
```

反例：把 NuGet 包目录当 `--managed`，游戏程序集就解析不到，基类链被**静默截断**
（`class base` 退化成 `TypeKind.Unknown` 并被过滤掉，只剩接口），看起来像"类型没有基类"。
- `--target`：类型名（`Duckov.BlackMarkets.BlackMarket` 或短名 `BlackMarket`）。
- `--member`：方法/字段名（配合 `decompile`/`il`）。
- `--offset`：跳过前 N 条，给 `search`/`strings` 翻页用（截断行会提示下一页的 `--offset`）。

## 出口码与静默边界（2026-10-17）

| 出口码 | 含义 |
|---|---|
| 0 | 成功（含合法空结果）|
| 1 | 目标不存在（类型/成员查不到）或某个 DLL 解析失败 |
| 2 | 调用方式错（未知 flag、flag 缺值、没给 `--dll`、`members`/`decompile`/`il` 没给 `--target`）|

- 找不到时不再只说 `no result`：会列出**搜过哪些 DLL**、提示换 `--dll <name>` / `--dll '*'`，并给 3 个近邻类型名。
- 一个类型可能住在多个 DLL 里；`--target` 查不到时先换 `--dll`（或 `--dll '*'`）再下结论。
- 单个 DLL 解析异常 → 输出里 `# ERROR: …` 且 **exit 1**（以前混在正文里、exit 0）。
- `search` / `strings` 没有命中 → **exit 0** + `# search: 0 match(es) … this is a legal empty result`（空结果是合法结果；`members`/`decompile`/`il` 无输出仍是 exit 1）。
- `--dll` 写了不存在的名字 → 解析出的 DLL 列表不含它时打 `# WARNING: --dll not found: …`（不再静默当成「没找到类型」）。名字里带点的（`TeamSoda.Duckov.Core`）按完整名比对，不会被当成扩展名切掉。

## 依赖（vendored，免联网）

`lib/` 下随附 ILSpy 的 `ICSharpCode.Decompiler`（MIT, v11.1.0.9782）及其两个 BCL 依赖
（`System.Reflection.Metadata` / `System.Collections.Immutable`），针对 net8.0。
许可全文与版本见仓根 `THIRD_PARTY_LICENSES.md`。
**无需 NuGet 下载** —— `dotnet run` 时直接引用 `lib/`。

> 为什么不用 `ilspycmd`（CLI）：它没有"引用/调用点"等能力，且不便被工具调用；我们要的是
> 可组合的统一接口。本探针只实现 V1 的 5 个 action；`refs`（调用图）与 Unity 资产是后续。
