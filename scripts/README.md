# 维护工具（maintainer-only）

本目录的工具是 **repo 作者（维护者）** 用的，**玩家/agent 从不运行**——它们只 `grep` 生成的 `docs/api` 和 `docs/data`，从不跑这些脚本。

## 工具清单

| 工具 | 运行时 | 产出 | 用途 |
|---|---|---|---|
| `inspect_game/` | dotnet SDK | `docs/api/*.md` | 反射 dump 游戏自有 DLL 的 API 签名（过滤第三方库） |
| `extract_data.py` | python3 + UnityPy | `docs/data/*.csv` | 解包游戏数据表 + 本地化（每表一个 CSV，取最丰富语言变体） |
| `extract_resources.py` | python3 + UnityPy | `docs/data/resources.csv` | 列资源清单（纹理/精灵/网格/音频/材质等，供做资源替换 mod） |

## 一键刷新（游戏 patch 后）

```bash
python3 scripts/refresh.py [游戏目录]
```

刷新 `docs/api` + `docs/data`。游戏目录缺省按此顺序解析：

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

1. **maintainer-only**：脚本读的是维护者本机的游戏安装目录；玩家/agent 不要跑，产品不负责这里的运行时。
2. **游戏 patch 后重跑**：`docs/api` + `docs/data` 是「某游戏版本的快照」，游戏更新后要重跑 refresh 刷新（对应 R2 数据层刷新机制）。
3. **review 后提交**：`refresh.py` 只生成、**不自动 commit**；确认 `git diff docs/api docs/data` 无误后手动提交。
4. **跨平台路径**：managedDir/dataDir 按 `mod-repo.json` + 平台推导，不要在脚本里写死路径。
5. **编译/字节码产物不进 git**：`.gitignore` 已排 `**/bin/`、`**/obj/`、`__pycache__/`、`*.pyc`——`dotnet run` 和 python 脚本都会生成这些，不要提交。
6. **Windows 终端编码**：脚本输出含中文 + emoji（✅/❌），Windows 请用 UTF-8 终端（Windows Terminal / Git Bash / VSCode 集成终端），GBK 下会乱码。
