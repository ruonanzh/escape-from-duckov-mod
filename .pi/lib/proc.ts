/**
 * 异步跑子进程（**不要用 `execFileSync`**）。
 *
 * 为什么：扩展的 `execute` 跑在 agent-host 的 Node event loop 上。`execFileSync` 会**阻塞**
 * 这个 event loop；而主进程每 15s ping 一次、10s 没响应就判定 host 死掉并重启（见
 * pi-desktop `host-manager.ts` 的 PING_INTERVAL_MS / PING_TIMEOUT_MS）。于是「编译 / 探针构建」
 * 这类 >10s 的同步子进程 = 触发 `agent-host ping timeout — killing`，**那次工具调用直接丢失**。
 *
 * 这里统一用异步 `execFile`（Promise 化），并在出错时把 stdout/stderr 挂回 error 上，
 * 方便调用方按需展示（与原 `execFileSync` 的 catch 用法兼容）。
 */
import { execFile } from "node:child_process";

export interface RunResult {
  stdout: string;
  stderr: string;
}

export interface RunOptions {
  cwd?: string;
  env?: NodeJS.ProcessEnv;
  /** 超时（毫秒），默认 5 分钟。 */
  timeoutMs?: number;
  /** 输出上限，默认 64MB。 */
  maxBuffer?: number;
}

export function runAsync(file: string, args: string[], options: RunOptions = {}): Promise<RunResult> {
  return new Promise((resolve, reject) => {
    execFile(
      file,
      args,
      {
        cwd: options.cwd,
        env: options.env,
        encoding: "utf8",
        timeout: options.timeoutMs ?? 300_000,
        maxBuffer: options.maxBuffer ?? 64 * 1024 * 1024,
      },
      (error, stdout, stderr) => {
        if (error) {
          const e = error as Error & { stdout?: string; stderr?: string };
          e.stdout = stdout;
          e.stderr = stderr;
          reject(e);
          return;
        }
        resolve({ stdout, stderr });
      },
    );
  });
}
