# tripo —— 生成模型（AI 3D），给"做 mod"用

薄封装 Tripo 的 REST API：**文本/图片 → 3D 模型（GLB）**，并导出成 **Unity 就绪朝向**（枪口/正面 = +Z）。
agent 通过 π 工具 **`generate_model`** 调它（`.pi/extensions/tripo.ts`）；也可以直接跑 CLI。

## 用法

```bash
dotnet build tools/tripo -c Release
export TRIPO_API_KEY=<你的 key>          # 或 --key
dotnet tools/tripo/bin/Release/net8.0/tripo.dll balance
dotnet tools/tripo/bin/Release/net8.0/tripo.dll generate --prompt "PPSh-41 样式的冲锋枪，游戏资产，侧视" --out gun.glb --face-limit 3000
dotnet tools/tripo/bin/Release/net8.0/tripo.dll generate --image ref.png --out gun.glb
dotnet tools/tripo/bin/Release/net8.0/tripo.dll convert --task <task_id> --out gun.glb --static
```

| 参数 | 说明 |
|---|---|
| `--prompt` / `--image` | 文本 或 单图（二选一）|
| `--out` | 保存路径（**任务成功立刻下载** —— 远端 URL 5 分钟过期 ✗）|
| `--face-limit` | 最大三角面（默认 3000；游戏资产 1500~4000）|
| `--no-texture` / `--pbr` | 不要贴图 / 要 PBR 贴图 |
| `--static`（convert）| 静态（不带骨骼/动画）—— 道具/武器用 ✓ |
| `--export-orientation`（convert）| 默认 `-x` = **Unity 前向**（实测：`-x` 让枪口落在 +Z）|
| `--global` | 用全球站 `.ai`（默认中国站 `.com`）|

## 实测坑（都踩过）

1. ⚠️ **本机网络会 DPI 重置 .NET 的 TLS 握手**（报 `unexpected EOF`）→ 工具里已强制 **TLS 1.2 + 不走代理** 才通 ✓
   （同样的端点用 curl / Python 是通的 ✓ —— 所以别以为"网络坏了" ✗）
2. 模型 URL **5 分钟后过期** → 任务成功必须**立刻下载** ✓（CLI 已自动做 ✓）
3. 轮询 **2 秒一次**（别更密 ✓ 官方限流）
4. `task_id` **可复用**：`convert` / 绑骨 / 贴图 / 减面都只传 `task_id` ✓ 不重新生成也不重新上传 ✓
5. P 系列（`P1-*`）**面数精确生效**（要 3000 就得 ~2900 ✓）；对**冷门型号**识别不准（PPD-34/PPSh 会出成 AK ✗）→ 换提示词或先用文生图 ✓
6. 需要 key：放 `.gamer-agent.local.json` 的 `tripo.apiKey`（工作区本地 ✓ 不进 git ✓）
