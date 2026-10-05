import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { basename, join, relative, resolve } from "node:path";
import { runAsync } from "../lib/proc";
import { readState } from "../lib/game-paths";

/**
 * create_mod —— 从 repo 里的**模板 mod** 起一个可编译的 mod（一条调用，不用手抄文件、不用手改名字）。
 *
 * 为什么需要它：游戏要求每个 mod 提供 `<mod名>.ModBehaviour` 类型 → 目录名 / `info.ini` 的 name /
 * `.csproj` 的 `AssemblyName`+`RootNamespace` / `ModBehaviour.cs` 的 `namespace` **四处必须一致** ✗
 * —— 手工抄模板时极易漏一处，漏了就"mod 加载不了"且现象隐晦 ✗。这个工具把四处理一致 + 写 config + 编译。
 *
 * 模板目录：`reference/<template>/`（kind 决定用哪个；新能力就加一个模板 + 在这里登记 ✓）
 */

/** kind → { 模板目录, 默认文件名, label } */
const KINDS: Record<string, { template: string; dll?: string }> = {
  "replace-weapon-model": { template: "reference/weapon_model" },
};

export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "create_mod",
    label: "Create Mod (from template)",
    description:
      "Create a compilable mod inside your workspace by copying a repo template and fixing every place that must carry the mod name consistently (mod folder, info.ini name, AssemblyName/RootNamespace, the ModBehaviour namespace - the game looks for a <modName>.ModBehaviour type, so all of them must match). Writes config.json for the chosen kind and, unless build=false, compiles it with the recorded game directory. Use it instead of copying template files by hand.",
    promptSnippet: "Create a mod from a repo template (name + config + build)",
    promptGuidelines: [
      "Use create_mod instead of copying reference/ template files by hand: the mod name must appear in four places and mismatches make the game silently not load the mod.",
      "Pick kind for the capability you are building (e.g. 'replace-weapon-model'), give a simple name (letters/digits, no spaces), and pass the capability's config (target/typeIDs/model) so config.json is written for you.",
      "It writes only inside the workspace (default your_mods/<name>/) and refuses to overwrite an existing folder unless force= is set. After it returns PASS, use install_mod to put the mod into the game.",
    ],
    parameters: Type.Object({
      kind: Type.Union([Type.Literal("replace-weapon-model")], { description: "Which capability template to use." }),
      name: Type.String({ description: "Mod name, used as folder / info.ini name / assembly name / namespace. Letters+digits, no spaces (e.g. 'MyGun')." }),
      dir: Type.Optional(Type.String({ description: "Where to create it (default: your_mods/<name>)." })),
      target: Type.Optional(Type.String({ description: "For replace-weapon-model: the weapon to replace, e.g. 'MP5' (substring of the item name)." })),
      typeIDs: Type.Optional(Type.Array(Type.Number(), { description: "Optional: exact item typeIDs to match instead of (or in addition to) target." })),
      model: Type.Optional(Type.String({ description: "Model file name (a .glb that will live in the mod folder, e.g. 'gun.glb'). Default: model.glb." })),
      front: Type.Optional(Type.String({ description: "Only for models you got elsewhere (not from generate_model): which way the model faces. 'auto' (default) means it already faces +Z." })),
      file: Type.Optional(Type.String({ description: "For replace-weapon-model, the GLB file name inside the mod (default gun.glb)." })),
      build: Type.Optional(Type.Boolean({ description: "Compile after creating (default true). Needs the recorded game directory (see check_runtime / try_set_game_dir)." })),
      force: Type.Optional(Type.Boolean({ description: "Overwrite an existing mod folder (default false)." })),
    }),

    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const cwd = ctx.cwd;
      const spec = KINDS[params.kind];
      if (!spec) throw new Error(`unknown kind: ${params.kind}`);
      const templateDir = join(cwd, spec.template);
      if (!existsSync(templateDir)) throw new Error(`template not found: ${spec.template}`);

      const name = params.name;
      if (!/^[A-Za-z][A-Za-z0-9]*$/.test(name)) {
        return { content: [{ type: "text", text: `FAIL: name must be letters/digits starting with a letter (got "${name}"). It becomes the .NET namespace and assembly name.` }] };
      }
      const modDir = params.dir ? resolve(cwd, params.dir) : join(cwd, "your_mods", name);
      // 空目录放行：app 的写隔离要求先 create_mod_folder（建目录+绑定会话），之后这里才填骨架 ——
      // 那时目录已存在且为空，不该要求 force=true。只有目录里已经有东西时才要求 force（避免覆盖已有内容）。
      if (existsSync(modDir)) {
        const existing = readdirSync(modDir).filter((e) => e !== ".pi-mod.json");
        if (existing.length > 0 && !params.force) {
          return {
            content: [
              {
                type: "text",
                text: `FAIL: ${modDir} already exists and is not empty (${existing.slice(0, 5).join(", ")}). Pick another name, or pass force=true to overwrite it.`,
              },
            ],
          };
        }
      }

      // ① 拷模板（跳过构建产物）
      const copied: string[] = [];
      const copyTree = (from: string, to: string) => {
        mkdirSync(to, { recursive: true });
        for (const e of readdirSync(from)) {
          if (e === "bin" || e === "obj") continue;
          const f = join(from, e);
          const t = join(to, e);
          if (statSync(f).isDirectory()) copyTree(f, t);
          else {
            // csproj 也要跟着改名（否则目录里留着模板名字，且 build 目标是它）
            const target = e.endsWith(".csproj") ? join(to, `${name}.csproj`) : t;
            let data = readFileSync(f, "utf8");
            // ② 四处名字一起改：.csproj 的 AssemblyName/RootNamespace + ModBehaviour.cs 的 namespace
            //    （info.ini 的 name 见下；目录名 = modDir 的 basename ✓）
            if (e.endsWith(".csproj")) {
              data = data.replace(/<AssemblyName>[^<]*<\/AssemblyName>/, `<AssemblyName>${name}</AssemblyName>`);
              data = data.replace(/<RootNamespace>[^<]*<\/RootNamespace>/, `<RootNamespace>${name}</RootNamespace>`);
              // ⭐ 模板里指向 libs/mod-kit 的相对路径是按**模板目录**算的 ✗
              //    → 抄到别处（如 your_mods/<name>/）层数不同会断 ✗ → 按新位置重算 ✓
              const libDir = relative(modDir, join(cwd, "libs", "mod-kit")).split("\\").join("/");
              data = data.replace(/(<Compile Include=")[^"]*libs\/mod-kit\//g, `$1${libDir}/`);
            }
            if (e === "ModBehaviour.cs") {
              data = data.replace(/^namespace .*$/m, `namespace ${name}`);
              data = data.replace(/\[WeaponModel\]/g, `[${name}]`);
            }
            if (e === "info.ini") data = data.replace(/^name *=.*$/m, `name = ${name}`);
            writeFileSync(target, data);
            copied.push(target.slice(modDir.length + 1));
          }
        }
      };
      copyTree(templateDir, modDir);

      // ③ 写 config.json（按能力给默认字段）
      const modelFile = params.file ?? params.model ?? "gun.glb";
      const cfg: Record<string, unknown> = {};
      if (params.kind === "replace-weapon-model") {
        cfg.target = params.target ?? "";
        if (params.typeIDs?.length) cfg.typeIDs = params.typeIDs;
        if (params.front) cfg.front = params.front;
        cfg.model = modelFile;
      }
      writeFileSync(join(modDir, "config.json"), JSON.stringify(cfg, null, 2) + "\n");

      // ④ 编译（用已记录的游戏目录）
      let buildInfo = "";
      let buildOk = true;
      if (params.build !== false) {
        const state = readState(cwd) as { gameDir?: string | null; runtime?: { dotnet?: string } };
        const gameDir = state.gameDir;
        if (!gameDir) {
          buildOk = false;
          buildInfo = "SKIP build: no game directory recorded - run try_set_game_dir first, then `dotnet build` in the mod folder.";
        } else {
          const dotnet = state.runtime?.dotnet ?? "dotnet";
          try {
            await runAsync(dotnet, ["build", "-c", "Release", "-v", "q", "-nologo"], {
              cwd: modDir,
              env: { ...process.env, DUCKOV_DIR: gameDir },
            });
            buildInfo = "built OK";
          } catch (error) {
            buildOk = false;
            const e = error as { stdout?: string; stderr?: string; message?: string };
            buildInfo = `build FAILED:\n${(e.stderr || e.stdout || e.message || String(error)).trim().slice(-800)}`;
          }
        }
      }

      const prefix = buildOk ? "PASS" : "WARN";
      return {
        content: [
          {
            type: "text",
            text:
              `${prefix}: mod "${name}" created at ${modDir}\n` +
              `files: ${copied.join(", ")}, config.json\n` +
              `config: ${JSON.stringify(cfg)}\n` +
              `${buildInfo}\n` +
              `NEXT: put the model file (${modelFile}) in that folder - use generate_model to make one if you have none - then install_mod, then have the user draw the weapon in game to verify.`,
          },
        ],
      };
    },
  });
}
