# api-probe —— 只读检查托管 DLL（给 agent 用）

当 `docs/api/`（维护者反射 dump 的**公开**签名）不够时，用本探针读游戏托管 DLL 的
**私有成员 / 反编译 C# / IL / 字符串字面量**。

**只读**：用 ILSpy 的 `ICSharpCode.Decompiler` **读取**程序集文件，**不加载、不执行**目标 DLL。

## 用法

```bash
dotnet run --project tools/api-probe -- \
  --managed "<games>/.../Managed" --action <search|members|decompile|il|strings> \
  [--dll TeamSoda.Duckov.Core] [--target <type-or-substring>] [--member <method>] [--limit N] [--offset N]
```

| action | 作用 |
|---|---|
| `search` | 按名字（子串，忽略大小写）找**类型/成员**（每次最多 500 条，`--offset` 翻页）|
| `members` | 某类型的**全部成员（含 private）+ 基类** |
| `decompile` | 反编译**类型**（或 `--member` 指定方法/字段）为 **C#** |
| `il` | 反编译类型/方法的 **IL** |
| `strings` | 扫程序集里的**字符串字面量**（每次最多 500 条，`--offset` 翻页）|

- `--dll`：默认 `TeamSoda.Duckov.Core`；可逗号分隔多个，或 `*`（Managed 下全部）。
- `--target`：类型名（`Duckov.BlackMarkets.BlackMarket` 或短名 `BlackMarket`）。
- `--member`：方法/字段名（配合 `decompile`/`il`）。
- `--offset`：跳过前 N 条，给 `search`/`strings` 翻页用（截断行会提示下一页的 `--offset`）。

## 依赖（vendored，免联网）

`lib/` 下随附 ILSpy 的 `ICSharpCode.Decompiler`（MIT, v11.1.0.9782）及其两个 BCL 依赖
（`System.Reflection.Metadata` / `System.Collections.Immutable`），针对 net8.0。
**无需 NuGet 下载** —— `dotnet run` 时直接引用 `lib/`。

> 为什么不用 `ilspycmd`（CLI）：它没有"引用/调用点"等能力，且不便被工具调用；我们要的是
> 可组合的统一接口。本探针只实现 V1 的 5 个 action；`refs`（调用图）与 Unity 资产是后续。
