/**
 * mod 身份的判据（**共享**：mod-lint 与 mod-install 都用这一份，避免两边各自实现后漂移）。
 *
 * 游戏硬规则：`info.ini` 的 `name` 必须是合法 C# 命名空间标识符 —— 它会拼进 `<name>.dll` 的读路径，
 * 名字非法时游戏读不到程序集。所以"name 非空"远远不够，必须过 `CODE_IDENTIFIER_RE`。
 */
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

/** C# 命名空间级标识符（游戏硬规则，见上） */
export const CODE_IDENTIFIER_RE = /^[A-Za-z_][A-Za-z0-9_]*$/;

export const isCodeIdentifier = (v: string): boolean => CODE_IDENTIFIER_RE.test(v);

export interface ModIdentity {
  name: string;
  version: string;
}

/**
 * 读 `info.ini` 的 name/version。
 * 返回 null 的条件：文件不存在、或没有可用的 `name`（空/缺失）。
 * 注意：**这里不做 C# 命名空间校验**（那是 `isCodeIdentifier` 的事）—— 调用方（validate/install）
 * 都应当在同一处用它，才能保证"validate 说能装、install 就真能装"。
 */
export function readModIdentity(modDir: string): ModIdentity | null {
  const iniPath = join(modDir, "info.ini");
  if (!existsSync(iniPath)) return null;
  const fields: Record<string, string> = {};
  for (const line of readFileSync(iniPath, "utf8").split(/\r?\n/)) {
    const m = /^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$/.exec(line);
    if (m) fields[m[1]] = m[2].trim();
  }
  const name = fields.name?.trim();
  return name ? { name, version: fields.version?.trim() ?? "" } : null;
}
