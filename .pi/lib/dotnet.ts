/**
 * .NET SDK 探测（**共享**：check_runtime 与 install_runtime 都用这一份，避免两边各写一套后漂移）。
 *
 * 判据：能在本机跑起来、且 `dotnet --version` 有输出的 dotnet 可执行文件。
 * 候选覆盖「PATH → 用户级 ~/.dotnet → 系统级」——用户级是 install_runtime 推荐的装法，
 * 默认**不在 PATH 上**；只看 PATH 会导致「按指引装完仍被判缺 SDK」，所以必须都探。
 *
 * 多个候选都在时选**主版本最高**的：用户级的新 SDK 不该被 PATH 上的旧版本顶掉。
 */
import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";
import os from "node:os";

export interface DotnetProbe {
  found: boolean;
  /** 例如 "8.0.404" */
  version: string | null;
  major: number | null;
  /** 实际可执行文件路径；来自 PATH 时为 "dotnet" */
  path: string | null;
}

/** 所有可能要探的 dotnet 位置（顺序仅用于展示，最终按版本选优） */
export function dotnetCandidates(): string[] {
  const bin = process.platform === "win32" ? "dotnet.exe" : "dotnet";
  const out = ["dotnet"]; // PATH
  out.push(join(os.homedir(), ".dotnet", bin));
  if (process.platform === "win32") {
    out.push("C:\\Program Files\\dotnet\\dotnet.exe");
    if (process.env.LOCALAPPDATA)
      out.push(join(process.env.LOCALAPPDATA, "Microsoft", "dotnet", "dotnet.exe"));
  } else if (process.platform === "darwin") {
    out.push("/usr/local/share/dotnet/dotnet");
  }
  return [...new Set(out)];
}

function runVersion(p: string): string | null {
  try {
    return execFileSync(p, ["--version"], { encoding: "utf8" }).trim() || null;
  } catch {
    return null;
  }
}

const parts = (v: string): number[] => v.split(".").map((n) => parseInt(n, 10) || 0);

/** 版本号比较（a - b 的符号）；用于从多个 dotnet 里选最高的那个 */
function cmpVersion(a: string, b: string): number {
  const pa = parts(a);
  const pb = parts(b);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const d = (pa[i] ?? 0) - (pb[i] ?? 0);
    if (d !== 0) return d;
  }
  return 0;
}

/** 探一次 .NET SDK：所有候选各跑一次 `--version`，选版本最高的那个。 */
export function probeDotnet(): DotnetProbe {
  const found: { path: string; version: string }[] = [];
  for (const c of dotnetCandidates()) {
    if (c !== "dotnet" && !existsSync(c)) continue;
    const v = runVersion(c);
    if (v) found.push({ path: c, version: v });
  }
  if (found.length === 0) return { found: false, version: null, major: null, path: null };
  found.sort((a, b) => cmpVersion(b.version, a.version));
  const best = found[0];
  return { found: true, version: best.version, major: parts(best.version)[0] ?? null, path: best.path };
}
