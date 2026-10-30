# 维护工具（maintainer-only）

它们只读 `docs/api`（API 签名快照）与现查的 `inspect_game_data` 结果，从不跑这些脚本。

## 自检脚本（判据的机器检查）

这些跑起来不花积分，也不装任何东西。改判据或改文档前后各跑一次。
完整门禁使用 `lefthook run pre-commit --all-files`。decompile 测试需要 Bun。
未安装 Bun 时，decompile 检查失败，不得记为通过。

```bash
node --experimental-strip-types scripts/check-asset-manifest.mjs          # 素材清单判据
node --experimental-strip-types scripts/check-mod-install-staleness.mjs   # 产物过期判据
node --experimental-strip-types scripts/check-mod-config-data.mjs         # config 数据层判据
node scripts/check-probe-tool-parity.mjs                                  # 工具面一致性
node scripts/check-prompt-standard-zh.mjs                                 # 中文提示词规范（情态词、箭头、术语）
node scripts/check-character-rig.mjs                                      # 角色替换的骨骼映射表
node scripts/check-character-swap.mjs                                     # 角色替换的搬运算法与接线
node scripts/check-doc-refs.mjs                                           # 文档引用（链接与仓库根路径）
node scripts/check-mod-kit-api.mjs                                        # 台账里的 `类型.成员` 在代码里真的存在
bun test --cwd .pi/lib decompile.test.mjs                                 # dump 读取判据
bun scripts/test-inspect-code-incomplete.mjs                               # 不完整 dump 的 5 个 action
node --experimental-strip-types scripts/test-mod-install.mjs                # 安装事务、validate verdict、构建串行与 PNG 边界
node --experimental-strip-types scripts/test-tripo-network.mjs              # Tripo 超时、取消与付费串行
node --experimental-strip-types scripts/test-reference-builds.mjs            # 4 个 reference 的新 DLL 与负向构建夹具
```

它们都导入 `.pi/lib/` 里的**同一份**函数，扩展也导入那份。于是判据只有一份，不会漂。

## 工具清单

| 工具 | 运行时 | 产出 | 用途 |
|---|---|---|---|
| `inspect_game/` | dotnet SDK | `docs/api/*.md` | 反射 dump 游戏自有 DLL 的 API 签名（过滤第三方库） |
| `extract_data.py` | python3 + UnityPy | （不再生成）| **已废弃**：数据表与本地化快照。条目少得多（Items 892 ↔ 游戏自带 2944），已被「现查」取代（`docs/cookbook.md` 第 13、14 节）|
| `extract_resources.py` | python3 + UnityPy | （不再生成）| **已废弃**：资源清单快照。改用 `inspect_game_data` 的 `classes` / `search` / `export --class X` 现查 |

## 一键刷新（游戏 patch 后）

```bash
python3 scripts/refresh.py [游戏目录]
```

刷新 `docs/api`（API 签名快照）。游戏目录缺省按此顺序解析：

1. 命令行参数；
2. `.gamer-agent.local.json` 的 `gameDir`（check_runtime 缓存）；
3. 环境变量 `DUCKOV_DIR`。

managedDir 由 `mod-repo.json` 的 `compile.managedDir` 按平台推导（mac `Duckov.app/Contents/Resources/Data/Managed`、win `Duckov_Data/Managed`），dataDir = managedDir 的父目录。

## 依赖（维护者自装）

- **dotnet SDK >= 8.0**（`inspect_game` 用；玩家机器上由 `install_runtime` 引导装到 `~/.dotnet`）。
- **python3 + UnityPy**（两个 extract 脚本用）：
  ```bash
  pip install UnityPy
  ```

> 国内网络装依赖：
> - dotnet SDK：微软官方或国内镜像；
> - UnityPy：`pip install -i https://pypi.tuna.tsinghua.edu.cn/simple UnityPy`。

## 注意事项

1. **maintainer-only**：脚本读的是维护者本机的游戏目录。玩家与 agent 不得运行这些脚本；产品不负责这里的运行时。
2. **游戏 patch 后重跑**：`docs/api` 是「某游戏版本的快照」。游戏更新后必须重跑 refresh。
3. **review 后提交**：`refresh.py` 只生成，不自动 commit。确认 `git diff docs/api mod-repo.json` 无误后手动提交。
4. **跨平台路径**：managedDir/dataDir 按 `mod-repo.json` + 平台推导，不得在脚本里写死路径。
5. **编译/字节码产物不进 git**：`.gitignore` 已排 `**/bin/`、`**/obj/`、`__pycache__/`、`*.pyc`——`dotnet run` 和 python 脚本都会生成这些，不得提交。
6. **Windows 终端编码**：脚本输出含中文 + emoji（✅/❌），Windows 必须用 UTF-8 终端（Windows Terminal / Git Bash / VSCode 集成终端），GBK 下会乱码。

## `check-character-rig.mjs` —— 角色替换的骨骼映射表检查

角色替换的成败全压在 `libs/mod-kit/CharacterRig.cs` 那张表上。表错一条，游戏里就是**某个部位不动**或**整块歪掉**，
而且不报错 —— 又一次「静默无效果」。所以拿实测值机器查。

```bash
node scripts/check-character-rig.mjs     # 0 = 表自洽；1 = 有问题
```

查四类：① `DuckBones` 与**运行时实测名单逐项一致（含顺序）**；② 22 个 Mixamo 关节一个不漏；
③ 多出来的 7 根骨都在名单里、都挂到了名单内的骨头、且没被映射表重复指到；
④ 24 根实骨全部被覆盖。另外还查**解析到的条数**（表被改坏/解析失败时要显式红，不能靠空数组假通过）。

实测名单的唯一来源是设计仓 `docs/unity-3d-assets/10-character-replacement-research.md` §4.5。
游戏更新后先重跑探针，再改这里与 `CharacterRig.cs`。

## `measure-skeleton-gap.mjs` —— 量模型骨架与游戏骨架的差距

角色替换现在保留源骨架，并在运行时重定向游戏骨骼的旋转增量。这个脚本用于比较两副骨架的
参考姿势与比例。差距**随模型变**，不是常数。

```bash
node scripts/measure-skeleton-gap.mjs /path/to/model.glb /tmp/duckbody.txt
```

输出每根骨的**位移**（米，越大越说明「搬到角色骨上」会破坏形状）与**朝向偏差**（度，
大而整齐说明骨轴约定不同）。缺文件或缺地标时退出码 1。

## `check-character-swap.mjs` —— 角色替换的数学性质与运行时接线

`libs/mod-kit/CharacterSwap.cs` 创建源骨架，`CharacterRetarget.cs` 在运行时重定向旋转。
这个脚本防止已知的数据流与坐标转换回退，不执行 C#，也不证明游戏内外观正确。

```bash
node scripts/check-character-swap.mjs     # 0 = 符合预期；1 = 有问题
```

查两类：① 用 JavaScript 复算身体对齐、世界旋转增量与父子坐标转换；
② 对 C# 原文做关键字断言，检查源节点层级、源权重写回、坐标共轭转换、`LateUpdate`、
`Reassert`、骨骼与材质还原。脚本通过不等于已完成游戏内验收。

## `check-prompt-standard-zh.mjs` —— 中文提示词规范的检查

写文档的规则在**设计仓**的 `docs/duckov-harness/prompt-standard.md`。这份脚本把其中**能机器判**的三条查一遍：

1. **情态词**：全文只用 必须 / 不得 / 可以（「需要」表示缺少某物时可以用）。`不要`、`别X`、`请X` 都拦下。
2. **箭头与符号**：正文里不得用 `→` `←` `⇒` `⇐` `⇔`，也不得用 `✓` `✗` `⚠️` `⭐`。命令输出可以留。
3. **一词一义**：术语表里能机械判的那些（`用户`、`游戏安装目录`、`本仓(库)`、`卡片图`、`原图`、`素材`、
   `游戏档位`、`内容数据`、`脚手架`、`名称/身份`、`选中`、`匹配`）。

```bash
node scripts/check-prompt-standard-zh.mjs     # 0 = 干净；1 = 有违规（逐条打印 文件:行）
```

范围按标准的 §1：`AGENTS.md`、根 `README.md`、`.pi/skills/*/SKILL.md`、`docs/**/*.md`（`docs/api/` 除外）、
`reference/*/README.md`。**代码块与行内代码先被剥掉**，于是命令输出里的箭头与符号不会误报。

**故意不查的**（术语表按**概念**禁，grep 分不出来，留给人工 review）：`贴图`、`模板`、`数据`、
`API`、`实现`，以及 `gameDir`、`repo`（它们在本仓是真实标识符）。

## `check-mod-install-staleness.mjs` —— 产物过期判据的检查

```bash
node --experimental-strip-types scripts/check-mod-install-staleness.mjs   # 0 = 符合预期；1 = 有不符合
```

**为什么需要**：`install_mod` 会拒绝安装**比编译输入旧的**产物。
判太松：装进游戏的是旧产物，游戏里跑旧行为。
判太严：工作区更新一次就要求所有 mod 重编，白干活。
两种都踩过。

**它查什么**：调用 `.pi/lib/mod-products.ts` 里的 `sourcesNewerThan`。
那是**同一个函数**，扩展也导入它。
五个用例：产物比源码新 · 本 mod 的源码比产物新 · kit 比产物新 · **样例 mod 比产物新**（不得判成过期）· 同一时刻。

## `check-probe-tool-parity.mjs` —— 工具面一致性检查

```bash
node scripts/check-probe-tool-parity.mjs     # 0 = 对齐；1 = 有缺口
```

**为什么需要**：`data-probe`（CLI）认的参数写在 `tools/data-probe/*.cs`，而 agent 能传的参数写在
`.pi/extensions/inspect-game-data.ts` —— **两处各写各的**，没有任何东西保证一致。这个错**犯过两次**：
`9b26ae5`（CLI 加了 `export` + `match/field/rows`，扩展没跟上）· 后来（CLI 加了
`--mesh/--exact/--has/refs --by`，扩展又没跟上，于是 agent 只能绕道 bash 跑 CLI）。

**它查什么**：CLI 的 `Opt(o,"x")` / `o.ContainsKey("x")` / `AllOpts(args,"x")` + `Usage` 里的
`--action <a|b|c>` ⟷ 扩展的 `params.x` + `Type.Union` 里的 `Type.Literal("x")`。
白名单只有 `data` / `managed`（扩展自己算的游戏路径）。

已接到 `lefthook.yml` 的 `pre-commit`（装了 lefthook 才自动跑；否则手跑）。

## `check-mod-config-data.mjs` —— config 数据层判据的检查

`validate_mod` 用它判「新增武器 / 新增物品」的 `config.json`。判据本体在 `.pi/lib/mod-config-data.ts`。

它覆盖：action 名、必填字段（`cloneFromItemId`、`displayName`）、`newTypeId` 是否重复或已被游戏占用、
`quality` 与 `displayQuality` 的取值、`stats` 的键值类型、`merchantID` 是否认识、`crafting.materials` 的 id 与数量、
以及 `//` 注释的处理（字符串里的 `//` 不得被吃掉）。

```bash
node --experimental-strip-types scripts/check-mod-config-data.mjs   # 0 = 符合预期；1 = 有不符合
```

**已踩的坑**（写在这里免得再犯）：拿游戏 typeID 清单时用 `data-probe --action list`，一页是 500 个**资产**，
而里面有些行不是物品（`typeID=-1`）。于是「这一页少于 500 个命中」**不能**当末页判据，
否则第一页就停、后面的号全看不到（662 就这样被误报成不存在）。

## 两个探针（`tools/data-probe`、`tools/api-probe`）

它们不在这里，是 `tools/` 下的 C# 命令行探针：**agent 通过 `inspect_game_data` / `inspect_game_api` 调**，
维护者也可以直接跑（输出格式与扩展返回一致）。

```bash
dotnet build tools/data-probe -c Release && dotnet build tools/api-probe -c Release

dotnet tools/data-probe/bin/Release/net8.0/data-probe.dll \
  --managed "<游戏>/.../Managed" --data "<游戏>/.../Data" --action classes

dotnet tools/api-probe/bin/Release/net8.0/api-probe.dll \
  --managed "<游戏>/.../Managed" --dll '<程序集>.*' --action search --target <类型名>
```

两条要点：

- **`--managed` 是程序集解析根**，不只是「去哪找 dll」。目标程序集不在该目录时（例如来自 NuGet 缓存），
  让 `--managed` 继续指游戏目录，把绝对路径给 `--dll`；否则基类链会被**静默截断**，看起来像「类型没有基类」。
- **`--dll` 必填**（探针不得知道任何具体游戏的程序集名）。可用通配 `MyGame.*`；`*` 是「非框架全扫」，
  会把 Managed 下的第三方插件一起算进去。

出口码 0 / 1 / 2 的判据见两个 README 的「出口码与静默边界」。

