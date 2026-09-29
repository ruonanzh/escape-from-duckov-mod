/**
 * 探针构建的共享 helper（`inspect_game_api` / `inspect_game_data` 共用）。
 *
 * 两个工具都是"薄封装一个 vendored 的 C# 探针"：真正读 DLL / 读数据的是 `tools/*` 里的探针。
 * 这里只负责一件事：**判断要不要重新构建探针**（增量构建虽快，但每次 `dotnet build` 仍有
 * MSBuild 开销，没必要）。
 */
import { existsSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";

/** 探针 DLL 已存在、且比它的源码/依赖都新 → 无需重建。 */
export function probeUpToDate(probeDir: string, probeDll: string): boolean {
  if (!existsSync(probeDll)) return false;
  const dllTime = statSync(probeDll).mtimeMs;
  const watched: string[] = [];
  try {
    for (const e of readdirSync(probeDir)) {
      if (e.endsWith(".cs") || e.endsWith(".csproj")) watched.push(join(probeDir, e));
    }
  } catch {
    return false;
  }
  const libDir = join(probeDir, "lib");
  if (existsSync(libDir)) {
    try {
      for (const e of readdirSync(libDir)) watched.push(join(libDir, e));
    } catch {
      // ignore: 读不到 lib/ 目录就不看它，仍按其余源文件判断
    }
  }
  return watched.every((p) => {
    try {
      return statSync(p).mtimeMs <= dllTime;
    } catch {
      return false;
    }
  });
}
