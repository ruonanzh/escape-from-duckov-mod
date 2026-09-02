#!/usr/bin/env python3
"""Duckov 维护工具一键刷新（maintainer-only）。

重跑三个数据提取工具，刷新 docs/api + docs/data：
  1. inspect_game（dotnet）：反射 dump 游戏自有 DLL 的 API 签名 → docs/api/*.md
  2. extract_data.py（python + UnityPy）：解包数据表 + 本地化 → docs/data/*.csv
  3. extract_resources.py（python + UnityPy）：列资源清单 → docs/data/resources.csv

用法：
  python3 scripts/refresh.py [游戏目录]

游戏目录解析顺序：命令行参数 → .gamer-agent.local.json 的 gameDir（check_runtime 缓存）
→ 环境变量 DUCKOV_DIR。

依赖（维护者自装，见 scripts/README.md）：dotnet SDK >= 8.0、python3 + UnityPy。
"""
import json
import os
import shlex
import shutil
import subprocess
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STATE_FILE = os.path.join(REPO_ROOT, ".gamer-agent.local.json")
MOD_REPO_FILE = os.path.join(REPO_ROOT, "mod-repo.json")


def fail(msg):
    print(f"❌ {msg}", file=sys.stderr)
    sys.exit(1)


def info(msg):
    print(msg)


def run(cmd, cwd=REPO_ROOT):
    info(f"\n$ {shlex.join(cmd)}")
    result = subprocess.run(cmd, cwd=cwd)
    if result.returncode != 0:
        fail(f"命令失败（exit {result.returncode}）：{shlex.join(cmd)}")


def load_json(path):
    try:
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, json.JSONDecodeError) as e:
        fail(f"读取 {path} 失败：{e}")


def resolve_game_dir():
    if len(sys.argv) > 1:
        return sys.argv[1]
    if os.path.exists(STATE_FILE):
        state = load_json(STATE_FILE)
        if state.get("gameDir"):
            return state["gameDir"]
    env = os.environ.get("DUCKOV_DIR")
    if env:
        return env
    fail(
        "未找到游戏目录。用法：python3 scripts/refresh.py <游戏目录>；"
        "或先跑 check_runtime 生成 .gamer-agent.local.json；或设环境变量 DUCKOV_DIR。"
    )


def platform_key():
    if sys.platform == "win32":
        return "windows"
    if sys.platform == "darwin":
        return "mac"
    return "linux"


def resolve_managed_dir(game_dir):
    cfg = load_json(MOD_REPO_FILE)
    managed = cfg["compile"]["managedDir"]
    key = platform_key()
    if key not in managed:
        fail(f"该游戏不支持当前平台（{key}）。managedDir 仅支持：{', '.join(sorted(managed))}。")
    return os.path.join(game_dir, managed[key])


def resolve_dotnet():
    # 优先用状态文件里 check_runtime 缓存的 dotnet 绝对路径（用户级 ~/.dotnet 不在 PATH）
    if os.path.exists(STATE_FILE):
        state = load_json(STATE_FILE)
        dotnet = state.get("runtime", {}).get("dotnet")
        if dotnet and os.path.isfile(dotnet):
            return dotnet
    # 回退到 PATH；找不到时 shutil.which 返回 None，给清晰报错而非 FileNotFoundError
    path = shutil.which("dotnet")
    if not path:
        fail("未找到 dotnet SDK（需 >= 8.0）。请先装 dotnet 或跑 install_runtime。")
    return path


def main():
    game_dir = resolve_game_dir()
    if not os.path.isdir(game_dir):
        fail(f"游戏目录不存在：{game_dir}")

    managed_dir = resolve_managed_dir(game_dir)
    data_dir = os.path.dirname(managed_dir)  # managedDir 的父目录 = Data 目录
    api_dir = os.path.join(REPO_ROOT, "docs", "api")
    data_out_dir = os.path.join(REPO_ROOT, "docs", "data")

    if not os.path.isdir(managed_dir):
        fail(f"managedDir 不存在：{managed_dir}（请确认游戏已安装、路径正确）")

    info(f"gameDir    = {game_dir}")
    info(f"managedDir = {managed_dir}")
    info(f"dataDir    = {data_dir}")
    info(f"outDir     = docs/api + docs/data")

    # 依赖检查
    dotnet = resolve_dotnet()
    if subprocess.run([dotnet, "--version"], capture_output=True).returncode != 0:
        fail("未找到 dotnet SDK（需 >= 8.0）。请先装 dotnet 或跑 install_runtime。")
    try:
        import UnityPy  # noqa: F401
    except ImportError:
        fail("未找到 UnityPy。请 `pip install UnityPy`。")

    os.makedirs(api_dir, exist_ok=True)
    os.makedirs(data_out_dir, exist_ok=True)

    # 1. API 签名（dotnet）
    run([dotnet, "run", "--project", "scripts/inspect_game", "--", managed_dir, api_dir])
    # 2. 数据表 + 本地化（python）
    run([sys.executable, "scripts/extract_data.py", data_dir, data_out_dir])
    # 3. 资源清单（python）
    run([sys.executable, "scripts/extract_resources.py", data_dir, data_out_dir])

    info("\n" + "=" * 60)
    info("✅ 三个工具全部跑完。下一步（review 后手动提交，refresh 不自动 commit）：")
    info("  git status")
    info("  git diff docs/api docs/data")
    info('  确认无误后：git add docs/api docs/data && git commit -m "data: refresh game data layer"')
    info("=" * 60)


if __name__ == "__main__":
    main()
