# 第三方组件与许可

本仓入库（git 跟踪）的第三方二进制与单文件库。**全部为 MIT**。游戏自带的 DLL 不在本表：那些是编译期引用（`<Private>false</Private>`），既不随仓也不随 mod 分发。

## 清单一览

| 文件 | 组件 | 版本 | 许可 | 用途 | 随 mod 分发 |
|---|---|---|---|---|---|
| `libs/0Harmony.dll` | [Harmony](https://github.com/pardeike/Harmony)（0Harmony） | **2.4.1.0** | MIT | `ItemHooks` / `CharacterSwap` 打补丁（postfix `ItemGraphicInfo.Setup`、`ItemAgent.Initialize`） | **是**（`<Private>true</Private>` + `CopyHarmonyToModRoot` 拷到 mod 根；玩家不用自己装） |
| `tools/data-probe/lib/AssetsTools.NET.dll` | [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) | **3.0.0.0** | MIT | 离线读 Unity `.assets`：类、字段、序列化对象 | 否（维护者工具） |
| `tools/data-probe/lib/AssetsTools.NET.MonoCecil.dll` | AssetsTools.NET.MonoCecil | **1.0.0.0** | MIT | 上面的 Mono 脚本支持 | 否 |
| `tools/data-probe/lib/Mono.Cecil.dll` | [Mono.Cecil](https://github.com/jbevain/cecil) | **0.11.4.0** | MIT | IL / 程序集解析 | 否 |
| `tools/data-probe/lib/Mono.Cecil.Rocks.dll` | Mono.Cecil.Rocks | **0.11.4.0** | MIT | 同上（Rocks 扩展） | 否 |
| `tools/data-probe/lib/classdata.tpk` | AssetsTools 类型数据库 | 与 AssetsTools 3.0 配套 | 随组件 | 解析 Unity 类型所需的 `.tpk` | 否 |
| `tools/api-probe/lib/ICSharpCode.Decompiler.dll` | [ICSharpCode.Decompiler](https://github.com/icsharpcode/ILSpy) | **11.1.0.9782** | MIT | 反编译 C#（`docs/api` 快照、`ItemGraphicInfo`/`ItemAgent_Gun` 的实证来源） | 否 |
| `tools/api-probe/lib/System.Reflection.Metadata.dll` | System.Reflection.Metadata | **9.0.0.0** | MIT | 上者的依赖 | 否 |
| `tools/api-probe/lib/System.Collections.Immutable.dll` | System.Collections.Immutable | **9.0.0.0** | MIT | 上者的依赖 | 否 |
| `editors/duckov_3d_editor/three.bundle.js` | [three.js](https://threejs.org) | **r169** | MIT | 3D 编辑器渲染（esbuild 打成单文件 IIFE） | 否（只给编辑器） |

编辑器那份的许可正文单独在 [`editors/duckov_3d_editor/THIRD_PARTY_LICENSES.md`](editors/duckov_3d_editor/THIRD_PARTY_LICENSES.md)。

## 升级与核对

- 升级任何一个 DLL 时**连版本一起改本表**。版本读法（不执行被读的代码）：

```sh
# 先建一个临时 console 工程，然后：
dotnet run -c Release -- libs/0Harmony.dll tools/data-probe/lib/AssetsTools.NET.dll
# Program.cs 里用 AssemblyName.GetAssemblyName(path) + FileVersionInfo.GetVersionInfo(path)
```

- `classdata.tpk` 跟 AssetsTools 大版本绑定：换 `AssetsTools.NET` 大版本时要一起换，否则新类型读不出来。
- `AssetsTools.NET.MonoCecil` 的版本号是上游自己写的 `1.0.0.0`，不代表与 `AssetsTools.NET` 同版本。

## 已知注意

- 本仓**没有自己的 LICENSE 文件**（默认保留所有权利）。第三方许可只覆盖上表那些文件本身。
- 不要把 LGPL/其它传染性许可的库直接编进来：各 mod 把 `libs/mod-kit/*.cs` **编译进每个 mod 的 DLL**，一旦混入，整个 mod DLL 都要按该许可分发。

## MIT 许可全文

以下适用于上表中所有 MIT 组件（各自的版权行见组件仓库；仓内二进制里可读到的是
`0Harmony` = `Copyright © 2016`、`Mono.Cecil` = `Copyright © 2008 - 2018 Jb Evain`、
`ICSharpCode.Decompiler` = `Copyright 2011-2026 AlphaSierraPapa`、`AssetsTools.NET` = `Written by nes`、
`System.*` = `© Microsoft Corporation`）。

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```
