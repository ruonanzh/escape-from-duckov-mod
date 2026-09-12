import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import {
  cpSync,
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  renameSync,
  rmSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { join, resolve, basename } from "node:path";

/**
 * install_mod — 把 your_mods/<ModName>/ 的产物装进游戏的 Mods 目录。
 *
 * 安装目标不自己探测：读 check_runtime 写的 .gamer-agent.local.json（单一事实源）。
 *
 * 同名冲突（契约见 desktop-gamer-agent-pi/docs/mod-repo-guide.md §4.1）：
 * 安装工具的职责是**装得进去**——目标目录被别的 mod 占用时，改名装到 `<name>_pimod`（再撞则 _pimod2…），
 * 并如实告知；**绝不覆盖别人的内容**。
 * 「两个 mod 在游戏里身份相同」是 mod 冲突问题，不属安装职责（本类型的身份 = info.ini 的 name =
 * C# 命名空间 = `<name>.dll`，编进产物，只有改 mod 名重新编译才能变）。
 *
 * 识别「是不是本 mod」靠目标目录里的 `.pi-mod.json`（记录 id 等）：
 * - 先扫 mod 根下所有 `.pi-mod.json`，按 id 找到本 mod 上次装在哪 → 就地更新（目录名不漂移）
 * - 目标目录里标记 id 相同 → 覆盖（重装/升级幂等）
 * - 目标目录没有标记、或 id 不同 → 改名安装 + 告知（不阻塞）
 */

/** dev-only 产物，不装进游戏（obj/bin 是编译中间物，.cs/.csproj 是源码） */
const EXCLUDED_DIRS = new Set(["obj", "bin", ".git", "node_modules"]);
const EXCLUDED_EXT = new Set([".cs", ".csproj", ".sln", ".pdb", ".user"]);

/** 读 mod 身份：info.ini 的 name（= 命名空间 = dll 名） */
export function readModIdentity(
  modDir: string,
): { name: string; displayName: string; version: string } | null {
  const iniPath = join(modDir, "info.ini");
  if (!existsSync(iniPath)) return null;
  const fields: Record<string, string> = {};
  for (const line of readFileSync(iniPath, "utf8").split(/\r?\n/)) {
    const m = /^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$/.exec(line);
    if (m) fields[m[1]] = m[2].trim();
  }
  const name = fields.name?.trim();
  if (!name) return null;
  return {
    name,
    displayName: fields.displayName?.trim() ?? name,
    version: fields.version?.trim() ?? "",
  };
}

/** 该目录下已安装标记（不是本工具装的 → null） */
export function readInstalledMarker(dir: string): { id?: string } | null {
  const markerPath = join(dir, ".pi-mod.json");
  if (!existsSync(markerPath)) return null;
  try {
    const parsed = JSON.parse(readFileSync(markerPath, "utf8")) as {
      id?: unknown;
    };
    return typeof parsed.id === "string" ? { id: parsed.id } : {};
  } catch {
    return {}; // 文件损坏：当作「不是我们装的」，交给冲突分支处理，不冒险覆盖
  }
}

/** 扫 mod 根，按 id 找本 mod 上次装到哪个目录（升级定位，避免目录名漂移） */
export function findInstalledDirByModId(
  modRoot: string,
  id: string,
): string | null {
  if (!existsSync(modRoot)) return null;
  for (const entry of readdirSync(modRoot, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;
    const dir = join(modRoot, entry.name);
    if (readInstalledMarker(dir)?.id === id) return dir;
  }
  return null;
}

/** 逐层复制 mod 产物，跳过源码/编译中间物 */
export function copyModProducts(from: string, to: string): string[] {
  const copied: string[] = [];
  const walk = (srcDir: string, destDir: string) => {
    mkdirSync(destDir, { recursive: true });
    for (const entry of readdirSync(srcDir, { withFileTypes: true })) {
      if (entry.isDirectory()) {
        if (EXCLUDED_DIRS.has(entry.name)) continue;
        walk(join(srcDir, entry.name), join(destDir, entry.name));
        continue;
      }
      const dot = entry.name.lastIndexOf(".");
      if (dot > 0 && EXCLUDED_EXT.has(entry.name.slice(dot).toLowerCase()))
        continue;
      if (entry.name === ".gitignore" || entry.name === ".DS_Store") continue;
      cpSync(join(srcDir, entry.name), join(destDir, entry.name));
      copied.push(entry.name);
    }
  };
  walk(from, to);
  return copied;
}

export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "install_mod",
    label: "Install Mod",
    description:
      "Copy a mod's runtime products from your_mods/<ModName>/ into the game's Mods directory (target discovered by check_runtime). Idempotent for the same mod: re-running updates the existing installation in place. It refuses to overwrite a different mod that already occupies the target directory. Does not compile, does not install SDKs, does not touch mod sources.",
    promptSnippet:
      "Install (or update) this mod into the game after validate_mod passes",
    promptGuidelines: [
      "Use install_mod after validate_mod passes, so the player can test the mod in-game. It copies products only; it never compiles.",
      "If install_mod reports a name conflict, do not retry blindly: tell the player which directory is occupied and decide together whether it is an older copy of this mod or a different mod; a real name clash cannot be fixed by renaming the install directory for this modType.",
    ],
    parameters: Type.Object({
      modDir: Type.String({
        minLength: 1,
        description:
          "Existing mod directory; absolute or relative to the workspace root, e.g. your_mods/MyMod.",
      }),
    }),
    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const repoRoot = ctx.cwd;
      if (!params.modDir.trim())
        throw new Error(
          "INVALID_MOD_DIR: Provide an existing mod directory, e.g. your_mods/MyMod.",
        );

      let cfg: { modType?: string };
      try {
        cfg = JSON.parse(readFileSync(join(repoRoot, "mod-repo.json"), "utf8"));
      } catch {
        throw new Error(
          "INVALID_WORKSPACE_CONFIG: Cannot read mod-repo.json. Reopen or update the game workspace; do not edit its maintainer configuration.",
        );
      }
      if (typeof cfg.modType !== "string" || !cfg.modType.trim()) {
        // mod 身份 = <modType>:<info.ini name>；缺 modType 会产出脏 id，宁可直接报配置错误
        throw new Error(
          "INVALID_WORKSPACE_CONFIG: mod-repo.json has no modType. Reopen or update the game workspace; do not edit its maintainer configuration.",
        );
      }

      const modDir = resolve(repoRoot, params.modDir);
      if (!statSync(modDir, { throwIfNoEntry: false })?.isDirectory()) {
        throw new Error(
          `INVALID_MOD_DIR: ${modDir} is not a directory. Check the session's bound path or build the mod first.`,
        );
      }

      // 目标目录来自 check_runtime 的发现结果（install_mod 不自己探测）
      const stateFile = join(repoRoot, ".gamer-agent.local.json");
      let modRoot: string | null = null;
      if (existsSync(stateFile)) {
        try {
          const state = JSON.parse(readFileSync(stateFile, "utf8")) as {
            modInstallDir?: unknown;
          };
          if (
            typeof state.modInstallDir === "string" &&
            state.modInstallDir.trim()
          )
            modRoot = state.modInstallDir;
        } catch {
          modRoot = null;
        }
      }
      if (
        !modRoot ||
        !statSync(modRoot, { throwIfNoEntry: false })?.isDirectory()
      ) {
        return {
          content: [
            {
              type: "text",
              text: "FAIL: install target not found (no usable modInstallDir in .gamer-agent.local.json).\nNEXT: run check_runtime first; if it cannot find the game, ask the player for the installed game directory.",
            },
          ],
          details: { ok: false, reason: "TARGET_NOT_FOUND" },
        };
      }

      const identity = readModIdentity(modDir);
      if (!identity) {
        return {
          content: [
            {
              type: "text",
              text: `FAIL: ${modDir} has no usable info.ini (name is required).\nNEXT: run validate_mod on this mod directory and fix the reported problems before installing.`,
            },
          ],
          details: { ok: false, reason: "MISSING_INFO_INI" },
        };
      }

      const dllPath = join(modDir, `${identity.name}.dll`);
      if (!existsSync(dllPath)) {
        return {
          content: [
            {
              type: "text",
              text: `FAIL: ${identity.name}.dll not found in ${modDir}.\nNEXT: run validate_mod to compile the mod, then install again.`,
            },
          ],
          details: { ok: false, reason: "MISSING_DLL", modName: identity.name },
        };
      }

      const id = `${cfg.modType}:${identity.name}`;

      // 升级定位：本 mod 之前装到哪就更新哪（目录名不因后来出现的冲突而漂移）
      const previousDir = findInstalledDirByModId(modRoot, id);
      const targetDir = previousDir ?? join(modRoot, identity.name);

      // 目标被别的 mod 占用 → 改名安装（不覆盖、不阻塞）。
      // 「两个 mod 在游戏里身份相同」是 mod 冲突问题，不是安装的职责：安装只保证装得进去、不毁别人的东西。
      let occupiedBy: string | null = null;
      let installDir = targetDir;
      if (existsSync(installDir) && !previousDir) {
        const marker = readInstalledMarker(installDir);
        if (marker?.id !== id) {
          occupiedBy = marker?.id ?? null;
          let suffix = 1;
          do {
            installDir = join(modRoot, `${identity.name}_pimod${suffix === 1 ? "" : suffix}`);
            suffix += 1;
          } while (existsSync(installDir) && readInstalledMarker(installDir)?.id !== id);
        }
      }

      // 先复制到临时目录再切换，避免中途失败留下半个安装
      const staging = `${installDir}.staging-${process.pid}`;
      rmSync(staging, { recursive: true, force: true });
      const copied = copyModProducts(modDir, staging);
      writeFileSync(
        join(staging, ".pi-mod.json"),
        `${JSON.stringify(
          {
            schemaVersion: 1,
            id,
            modType: cfg.modType,
            name: identity.name,
            displayName: identity.displayName,
            version: identity.version,
            source: `your_mods/${basename(modDir)}`,
            installedDir: basename(installDir),
            ...(installDir === targetDir ? {} : { renamedFrom: basename(targetDir) }),
            installedAt: new Date().toISOString(),
            installedBy: "pi-desktop",
          },
          null,
          2,
        )}\n`,
      );
      rmSync(installDir, { recursive: true, force: true });
      renameSync(staging, installDir);

      const renamed = installDir !== targetDir;
      const note = renamed
        ? `\nNOTE: ${basename(targetDir)} was already taken by ` +
          `${occupiedBy ? `another mod (${occupiedBy})` : "content this tool did not install"}, ` +
          `so it was installed to ${basename(installDir)} instead; that directory was left untouched.` +
          `\nNEXT: the mod keeps its in-game identity ${identity.name} (info.ini name = namespace = ${identity.name}.dll). ` +
          `If that clashes with the other mod in-game, change this mod's name (info.ini name + csproj AssemblyName/RootNamespace + namespaces), re-run validate_mod, then install_mod. Tell the player about the rename.`
        : `\nNEXT: ask the player to launch the game and confirm the mod loads; restart the game if it was already running.`;
      return {
        content: [
          {
            type: "text",
            text:
              `PASS: ${identity.name}${identity.version ? ` ${identity.version}` : ""} installed to ${installDir} (${copied.length} files).` +
              note,
          },
        ],
        details: {
          ok: true,
          id,
          modName: identity.name,
          targetDir: installDir,
          updated: Boolean(previousDir),
          renamed,
          occupiedBy,
          files: copied,
        },
      };
    },
  });
}
