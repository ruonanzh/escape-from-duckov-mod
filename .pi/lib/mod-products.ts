import {
  closeSync,
  existsSync,
  openSync,
  readdirSync,
  readSync,
  renameSync,
  rmSync,
  statSync,
} from "node:fs";
import { join, resolve } from "node:path";

export interface PngSize {
  width: number;
  height: number;
}

/** Read only the PNG signature and IHDR dimensions. Invalid or truncated input returns null. */
export function readPngSize(path: string): PngSize | null {
  try {
    const fd = openSync(path, "r");
    const bytes = Buffer.alloc(24);
    let count: number;
    try {
      count = readSync(fd, bytes, 0, bytes.length, 0);
    } finally {
      closeSync(fd);
    }
    const signature = [137, 80, 78, 71, 13, 10, 26, 10];
    if (
      count < bytes.length ||
      !signature.every((byte, index) => bytes[index] === byte) ||
      bytes.toString("ascii", 12, 16) !== "IHDR"
    )
      return null;
    const width = bytes.readUInt32BE(16);
    const height = bytes.readUInt32BE(20);
    return width > 0 && height > 0 ? { width, height } : null;
  } catch {
    return null;
  }
}

/** Replace an installed directory transactionally; restore the old copy if staging cannot be moved in. */
export function replaceInstallDir(installDir: string, staging: string, previous: string): void {
  const hadPrevious = existsSync(installDir);
  if (hadPrevious) renameSync(installDir, previous);
  try {
    renameSync(staging, installDir);
  } catch (error) {
    if (hadPrevious) renameSync(previous, installDir);
    throw error;
  }
  if (hadPrevious) rmSync(previous, { recursive: true, force: true });
}

/** 产物是否比它的**编译输入**旧。
 *
 * 为什么要查：`install_mod` 只复制已有的产物，不编译。
 * 改过 kit 之后旧产物照样装得进去。游戏里跑的是旧行为。
 * 实测踩过：旧 DLL 按旧顺序读 `slots`，玩家怎么调都不对。
 *
 * 判据：DLL 的 mtime 是否早于下面两组源码。
 *
 * 1. 本 mod 自己的 `.cs` 与 `.csproj`。
 * 2. `libs/mod-kit/**`。csproj 用通配把整个 kit 编进来。
 *
 * **不算 `reference/**`**。那是抄来的模板，不参与编译。
 * 模板漂移要用「重新抄一遍」解决，重编解决不了。
 * 所以这里不得把 rebuild 当模板漂移的补救。
 *
 * 只比 mtime。同一秒内不算，因此留 1 秒余量。
 *
 * @param dllPath 已找到的产物路径。
 * @param modDir `your_mods/` 下的 mod 目录。
 * @returns 源码比产物新时返回 true。
 */
export function sourcesNewerThan(dllPath: string, modDir: string): boolean {
  let newest = 0;
  const walk = (dir: string): void => {
    let entries;
    try {
      entries = readdirSync(dir, { withFileTypes: true });
    } catch {
      return;
    }
    for (const entry of entries) {
      if (entry.name === "bin" || entry.name === "obj" || entry.name === ".git") continue;
      const p = join(dir, entry.name);
      if (entry.isDirectory()) walk(p);
      else if (entry.name.endsWith(".cs") || entry.name.endsWith(".csproj")) {
        try {
          const mtime = statSync(p).mtimeMs;
          if (mtime > newest) newest = mtime;
        } catch {
          /* 读不到就不算 */
        }
      }
    }
  };
  walk(modDir);
  const workRoot = resolve(modDir, "..", ".."); // your_mods/<name> -> 工作区根
  walk(join(workRoot, "libs", "mod-kit"));
  let dllMtime = 0;
  try {
    dllMtime = statSync(dllPath).mtimeMs;
  } catch {
    return false;
  }
  return newest > dllMtime + 1000;
}
