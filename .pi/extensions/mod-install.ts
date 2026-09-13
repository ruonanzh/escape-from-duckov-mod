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
import { basename, isAbsolute, join, resolve } from "node:path";
import { checkModInstallDir } from "../lib/game-paths";
import { isCodeIdentifier, readModIdentity } from "../lib/mod-identity";

/**
 * install_mod — 把 your_mods/<ModName>/ 的产物装进游戏的 Mods 目录。
 *
 * 安装目标不自己探测：读 check_runtime 写的 .gamer-agent.local.json（单一事实源）。
 *
 * 同名冲突（契约见 desktop-gamer-agent-pi/docs/mod-repo-guide.md §4.1）：
 * 目标目录被**别的 mod** 占用时**改名安装**到 `<mod名>_pimod`（再撞顺延 _pimod2…）——
 * **绝不覆盖别人的内容**，也不阻塞安装。
 * 「两个 mod 在游戏里身份相同」是 mod 冲突问题，不属安装职责：本类型的身份 = info.ini 的 name
 * = C# 命名空间 = `<name>.dll`，编进编译产物，安装阶段不改。
 *
 * 目标目录里的 `.pi-mod.json`（只记 name）用来识别「这是我上次装的」：
 * 命中 → 就地更新（重装幂等）；不在 → 换一个不冲突的目录名。
 */

/** dev-only 产物，不装进游戏（obj/bin 是编译中间物，.cs/.csproj 是源码） */
const EXCLUDED_DIRS = new Set(["obj", "bin", ".git", "node_modules"]);
const EXCLUDED_EXT = new Set([".cs", ".csproj", ".sln", ".pdb", ".user"]);

/** 读 mod 身份：info.ini 的 name（= 命名空间 = dll 名） */

/** 这个目录是不是本 mod 上次装的 */
export function readMarkerName(dir: string): string | null {
  const markerPath = join(dir, ".pi-mod.json");
  if (!existsSync(markerPath)) return null;
  try {
    const parsed = JSON.parse(readFileSync(markerPath, "utf8")) as {
      name?: unknown;
    };
    return typeof parsed.name === "string" ? parsed.name : null;
  } catch {
    return null; // 坏文件当作「不是我们的」，宁可另装一个目录也不覆盖
  }
}

/**
 * 选落地目录：优先 <mod 名>；被别的 mod 占了就顺延 <mod 名>_pimod、_pimod2…
 * 同一个 mod 重装时命中自己的标记 → 原地更新（不漂移）。
 */
export function pickInstallDir(
  modRoot: string,
  modName: string,
  modIdentity: string = modName,
): {
  dir: string;
  renamed: boolean;
  occupiedBy: string | null;
  reused: boolean;
} {
  const primary = join(modRoot, modName);
  const free = (dir: string) => {
    if (!existsSync(dir)) return true;
    return readMarkerName(dir) === modIdentity;
  };
  if (free(primary))
    return {
      dir: primary,
      renamed: false,
      occupiedBy: null,
      reused: existsSync(primary),
    };

  const occupiedBy = readMarkerName(primary);
  for (let suffix = 1; ; suffix += 1) {
    const candidate = join(
      modRoot,
      `${modName}_pimod${suffix === 1 ? "" : suffix}`,
    );
    if (free(candidate))
      return {
        dir: candidate,
        renamed: true,
        occupiedBy,
        reused: existsSync(candidate),
      };
  }
}

/** 复制 mod 产物，跳过源码/编译中间物 */
export function copyModProducts(from: string, to: string): number {
  let count = 0;
  const walk = (srcDir: string, destDir: string) => {
    mkdirSync(destDir, { recursive: true });
    for (const entry of readdirSync(srcDir, { withFileTypes: true })) {
      if (entry.isDirectory()) {
        if (EXCLUDED_DIRS.has(entry.name)) continue;
        walk(join(srcDir, entry.name), join(destDir, entry.name));
        continue;
      }
      if (
        entry.name === ".gitignore" ||
        entry.name === ".DS_Store" ||
        entry.name === ".pi-mod.json"
      )
        continue;
      const dot = entry.name.lastIndexOf(".");
      if (dot > 0 && EXCLUDED_EXT.has(entry.name.slice(dot).toLowerCase()))
        continue;
      cpSync(join(srcDir, entry.name), join(destDir, entry.name));
      count += 1;
    }
  };
  walk(from, to);
  return count;
}

export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "install_mod",
    label: "Install Mod",
    description:
      "Copy a mod's runtime products from your_mods/<ModName>/ into the game's Mods directory (target discovered by check_runtime). Re-installing the same mod updates it in place. If the target directory is taken by another mod it installs as <ModName>_pimod instead of overwriting it. Does not compile, does not install SDKs, does not touch mod sources.",
    promptSnippet:
      "Install (or update) this mod into the game after validate_mod passes",
    promptGuidelines: [
      "Use install_mod after validate_mod passes, so the player can test the mod in-game. It copies products only; it never compiles.",
      "If install_mod reports the mod was installed as <ModName>_pimod, tell the player: the other mod in that directory was left untouched, and this mod still carries its own in-game name (info.ini name).",
    ],
    parameters: Type.Object({
      modDir: Type.String({
        minLength: 1,
        description:
          "Existing mod directory; absolute or relative to the workspace root, e.g. your_mods/MyMod.",
      }),
    }),
    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      if (!params.modDir?.trim())
        throw new Error(
          "INVALID_MOD_DIR: Provide an existing mod directory, e.g. your_mods/MyMod.",
        );
      const modDir = resolve(ctx.cwd, params.modDir);
      if (!statSync(modDir, { throwIfNoEntry: false })?.isDirectory()) {
        throw new Error(
          `INVALID_MOD_DIR: ${modDir} is not a directory. Check the session's bound path or build the mod first.`,
        );
      }

      // 目标目录来自 check_runtime 的发现结果（install_mod 不自己探测）
      let modRoot: string | null = null;
      try {
        const state = JSON.parse(
          readFileSync(join(ctx.cwd, ".gamer-agent.local.json"), "utf8"),
        ) as {
          modInstallDir?: unknown;
        };
        if (
          typeof state.modInstallDir === "string" &&
          state.modInstallDir.trim()
        )
          modRoot = state.modInstallDir.trim();
      } catch {
        modRoot = null;
      }
      if (!modRoot) {
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
      // 目标目录「不存在」是全新机器的正常状态（游戏从没跑过、Duckov_Data/Mods 还没被创建）→ **不是错误**：
      // 继续往下走，由下面的 mkdirSync(destDir, { recursive: true }) 连缺失的父级一起创建。
      // 旧行为要求「必须已存在」：check_runtime 报可安装 → install 因目标不存在失败 → 再 check 仍不创建 → 死循环（B18）。
      // 仍然拒绝的只有两种：路径不是绝对路径 / 路径存在但不是目录（否则继续只会在 cp/rename 阶段抛出更难懂的错）。
      const targetStat = statSync(modRoot, { throwIfNoEntry: false });
      if (!isAbsolute(modRoot) || (targetStat && !targetStat.isDirectory())) {
        return {
          content: [
            {
              type: "text",
              text:
                `FAIL: TARGET_INVALID: ${modRoot} ` +
                (targetStat
                  ? "exists but is not a directory."
                  : "is not an absolute path.") +
                "\nNEXT: run check_runtime again to re-derive the game's Mods directory; if it still looks wrong, ask the player where the game is installed.",
            },
          ],
          details: { ok: false, reason: "TARGET_INVALID" },
        };
      }
      const targetWasMissing = !targetStat;

      // 这个目标目录**在游戏目录里面**（mod-repo.json: modInstall.relativeTo = gameDir），所以先确认那条路径
      // 确实长在一个真的 Duckov 目录里：向上找**兄弟目录**里的游戏自带程序集（哨兵，与 Managed 同级。
      // Windows: <gameDir>/Duckov_Data/{Mods,Managed}；macOS: <...>/Data/{Mods,Managed}）。
      // 为什么不能只验"目录存在"：游戏换盘/卸载后旧路径可能还"存在"（残留目录）→ 我们会 mkdirSync(recursive)
      // 造出一条假路径、把 mod 装到游戏永远不读的地方，而且看起来还成功。
      // 不自己探测 Steam 库（那是 check_runtime 的职责）→ 只报告这份缓存已失效，让 agent 去重跑它。
      // 判据与 check_game_paths / try_set_game_paths 共用一份（lib/game-paths）：
      // 目标目录在游戏目录里（relativeTo=gameDir）→ 向上一步看兄弟目录 Managed/ 里的游戏哨兵。
      // 游戏换盘/卸载后旧路径可能"还存在"（残留目录），只验"目录在不在"会造出假路径并报成功。
      const targetVerdict = checkModInstallDir(modRoot);
      if (!targetVerdict.ok) {
        return {
          content: [
            {
              type: "text",
              text:
                `FAIL: GAME_DIRECTORY_NOT_FOUND: ${targetVerdict.reason}. ` +
                "The game may have been moved or uninstalled since check_runtime last ran." +
                `\nNEXT: ${targetVerdict.next}`,
            },
          ],
          details: { ok: false, reason: "GAME_DIRECTORY_NOT_FOUND" },
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
      // 身份会被拼进路径（下面的 <name>.dll 与安装标记）。它由 info.ini 决定，不由目录名决定，
      // 所以这里必须复用 validate_mod 的同一套规则 —— 否则 `../x` 这类名字能读到 mod 目录之外。
      if (!isCodeIdentifier(identity.name)) {
        return {
          content: [
            {
              type: "text",
              text: `FAIL: info.ini name ${JSON.stringify(identity.name)} is not a valid C# namespace.\nNEXT: fix info.ini (name = namespace = <name>.dll), rebuild, then run validate_mod again.`,
            },
          ],
          details: { ok: false, reason: "INVALID_IDENTITY" },
        };
      }
      if (!existsSync(join(modDir, `${identity.name}.dll`))) {
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

      // 安装目录名 = your_mods 下的**目录名**（不是 info.ini 的 name）：
      // · basename 一定是单层名字 → 构造上不可能越界（旧版用 info.ini 的 name 拼路径，带 ../ 就能写出去）
      // · 目录名在 create_mod_folder 时已按规则校验过
      // · 玩家在游戏 Mods 目录里看到的与工作区目录名一致，便于对号入座
      const modName = basename(modDir);

      // 上次替换在「旧版本挪开、新版本换入」之间崩溃 → 目标缺失但旁边留着 .previous-*：先恢复
      if (existsSync(modRoot))
        for (const entry of readdirSync(modRoot)) {
          if (entry.startsWith(`${modName}.previous-`) && !existsSync(join(modRoot, modName))) {
            renameSync(join(modRoot, entry), join(modRoot, modName));
          }
        }

      const { dir: installDir, renamed, occupiedBy, reused } = pickInstallDir(modRoot, modName, identity.name);

      // 同一个 mod（marker 里的身份相同）曾以别的目录名装过 → 只提示，不删它
      // 只在「身份 ≠ 目录名」时才需要扫：两者相同时，上次安装用的目录名必然就是本次的目标目录名，
      // 重装/冲突都由 pickInstallDir 在目标目录上解决 → 不可能出现"同一个 mod 装在两个目录名"。
      // （这样常见路径上没有 O(n) 扫描，而"身份≠目录名"的类型仍能拿到那条提示。）
      let duplicateDir: string | null = null;
      if (identity.name !== modName) {
        const installedName = basename(installDir);
        for (const entry of existsSync(modRoot) ? readdirSync(modRoot, { withFileTypes: true }) : []) {
          if (!entry.isDirectory() || entry.name === installedName) continue;
          if (readMarkerName(join(modRoot, entry.name)) === identity.name) duplicateDir = entry.name;
        }
      }

      // 先复制到临时目录再切换，避免中途失败留下半个安装
      const staging = `${installDir}.staging-${process.pid}`;
      const previous = `${installDir}.previous-${process.pid}`;
      rmSync(staging, { recursive: true, force: true });
      rmSync(previous, { recursive: true, force: true });
      let files = 0;
      try {
        files = copyModProducts(modDir, staging);
        // `.pi-mod.json` 的来龙去脉（避免后来人误判它的用途）：
        // · 最初的设计用意：一份**安装指南**，放在 mod 自己的工作区目录里（your_mods/<mod>/.pi-mod.json），
        //   描述"这个 mod 该怎么装"（当时没沟通清楚，没实现成那个形态）。
        // · 现在实际承担的角色：**归属标记**，写在**安装副本**里，内容只记 `{ "name": <mod 身份> }`,
        //   用来回答"这个目标目录是不是我上次装的" → 命中就地更新，没命中则改名装 `<目录名>_pimod`。
        // · 关键约束：**存什么就拿什么比**（这里存身份、判断也用身份）。若改成存目录名，就等于放弃
        //   "同一 mod 换目录名后仍能识别为同一份安装"的能力（Duckov 这类身份≠目录名的类型会静默装成两份）。
        // · 若将来真的要实现"工作区侧的安装指南"，需另定文件名或明确优先级，不要复用这个文件。
        writeFileSync(
          join(staging, ".pi-mod.json"),
          `${JSON.stringify({ name: identity.name }, null, 2)}\n`,
        );
        // 事务化替换：旧版本先 rename 到旁边（不删）→ 换入新版本 → 成功后才删旧的；
        // 换入失败就把旧版本挪回。旧版是「先 rmSync 再 rename」，中间失败会让新旧两份都丢。
        const hadPrevious = existsSync(installDir);
        if (hadPrevious) renameSync(installDir, previous);
        try {
          renameSync(staging, installDir);
        } catch (error) {
          if (hadPrevious) renameSync(previous, installDir);
          throw error;
        }
        if (hadPrevious) rmSync(previous, { recursive: true, force: true });
      } catch (error) {
        rmSync(staging, { recursive: true, force: true });
        return {
          content: [
            {
              type: "text",
              text: `FAIL: copy failed: ${error instanceof Error ? error.message : String(error)}\nNEXT: check write permission on ${modRoot}. The previously installed copy (if any) was left in place.`,
            },
          ],
          details: { ok: false, reason: "COPY_FAILED" },
        };
      }

      const mismatched = identity.name !== modName;
      const notes: string[] = [];
      if (targetWasMissing) {
        notes.push(
          `\nNOTE: the mod directory ${modRoot} did not exist and was created (normal on a first install - the game had not created it yet). If the player expected mods somewhere else, tell them where this mod actually landed: ${installDir}.`,
        );
      }
      if (mismatched) {
        notes.push(
          `\nNOTE: the installed folder is named ${modName} (the your_mods directory name), while the mod declares name ${identity.name} - the game loads <name>.dll by the namespaces inside, so keep info.ini, csproj and the dll name consistent.`,
        );
      }
      if (duplicateDir) {
        notes.push(
          `\nNOTE: the same mod (name ${identity.name}) is also installed as ${duplicateDir}; this tool did not touch it - tell the player which one to enable, and remove the other manually if it is stale.`,
        );
      }
      const extraNotes = notes.join("");
      const note = renamed
        ? `\nNOTE: ${identity.name} was already taken by ${occupiedBy ? `another mod (${occupiedBy})` : "content this tool did not install"}, ` +
          `so it went to ${basename(installDir)} instead; that directory was left untouched.` +
          `\nNEXT: tell the player it installs as ${basename(installDir)} (its in-game name is still ${identity.name}), then let them launch the game.`
        : `\nNEXT: ask the player to launch the game and confirm the mod loads; restart the game if it was already running.`;

      return {
        content: [
          {
            type: "text",
            text: `PASS: ${identity.name}${identity.version ? ` ${identity.version}` : ""} installed to ${installDir} (${files} files).${extraNotes}${note}`,
          },
        ],
        details: {
          ok: true,
          modName: identity.name,
          targetDir: installDir,
          updated: reused,
          renamed,
          occupiedBy,
          files,
          targetCreated: targetWasMissing,
        },
      };
    },
  });
}
