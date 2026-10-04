import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { runAsync } from "../lib/proc";
import { existsSync } from "node:fs";
import { join, isAbsolute, resolve } from "node:path";
import { readState } from "../lib/game-paths";
import { probeUpToDate } from "../lib/probe";

/**
 * generate_model —— 用 Tripo 从「一句话」或「一张图」生成 3D 模型（GLB），存到你正在做的 mod 目录里。
 *
 * 薄封装 `tools/tripo`（C# CLI）：建任务 → 轮询 → **立刻下载**（URL 5 分钟过期）→ 可选转换。
 * 为什么要这个工具：光有 API 文档，agent 没法"照做" —— 这一步必须可执行。
 *
 * API key 从 `.gamer-agent.local.json`（工作区本地、不进 git）的 `tripo.apiKey` 读，
 * 或环境变量 TRIPO_API_KEY。没有 key 就返回 FAIL + 告诉用户放哪。
 */
export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "generate_model",
    label: "Generate Model (Tripo)",
    description:
      "Generate a 3D model file (GLB) with Tripo from a text prompt or a reference image, and save it where you are building the mod. Use this to obtain the model for a model-mod (e.g. replacing a weapon's or an item's model). Also converts an existing Tripo task to a Unity-ready GLB (faces +Z) and reports the account balance. Requires a Tripo API key: put it in .gamer-agent.local.json as tripo.apiKey (or set TRIPO_API_KEY). The generated model is saved locally; the remote model URL expires after 5 minutes, so the tool downloads immediately.",
    promptSnippet: "Generate a 3D model (GLB) with Tripo from a prompt or image",
    promptGuidelines: [
      "Use generate_model to get the model file a model-mod needs (a weapon, an item, a character). For a model you already have as a file, do not call it - just use that file.",
      "Low-poly game assets come from the P series (the tool defaults to it, faceLimit ~3000). Pass a reference image with image= for image-to-model when the user has one (or when text alone misses the shape).",
      "The tool saves a GLB and prints PASS with the path. The model faces +Z after conversion (Unity forward), so no rotation is needed later. Keep requesting the same taskId with action=convert if you need another format/size - it does not re-generate or re-upload.",
      "This calls a paid API: it costs credits (a P-series model with texture is roughly 40, a conversion roughly 10). Check with action=balance first if the user cares, and never call it in a loop to 'try again' - change the prompt or ask the user.",
      "If it returns FAIL about a missing key, ask the user for their Tripo API key and explain it goes in .gamer-agent.local.json as tripo.apiKey (that file is local and never committed).",
    ],
    parameters: Type.Object({
      action: Type.Union(
        [Type.Literal("generate"), Type.Literal("convert"), Type.Literal("balance")],
        { description: "generate = prompt/image to a new model; convert = re-export an existing task; balance = credits left." },
      ),
      prompt: Type.Optional(
        Type.String({ description: 'Text description for the model, e.g. "PPSh-41 style submachine gun with drum magazine, game asset, side view".' }),
      ),
      image: Type.Optional(Type.String({ description: "Path to a reference image (png/jpg) for image-to-model." })),
      out: Type.Optional(Type.String({ description: "Where to save the GLB (path, usually inside your mod folder). Default: model.glb in the repo root." })),
      taskId: Type.Optional(Type.String({ description: "For action=convert: the task id returned by an earlier generate." })),
      faceLimit: Type.Optional(Type.Number({ description: "Max triangles (default 3000). Game assets: 1500-4000." })),
      static: Type.Optional(Type.Boolean({ description: "For action=convert: true = static (no skeleton/animation) - correct for props and weapons." })),
      noTexture: Type.Optional(Type.Boolean({ description: "For action=generate: skip texturing (cheaper/faster)." })),
    }),
    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const cwd = ctx.cwd;
      const st = readState(cwd);
      const tripoState = st.tripo as { apiKey?: string } | undefined;
      const key = tripoState?.apiKey ?? process.env.TRIPO_API_KEY;
      if (!key) {
        return {
          content: [
            {
              type: "text",
              text:
                "FAIL: no Tripo API key.\nNEXT: ask the user for their Tripo API key, then write it to .gamer-agent.local.json as {\"tripo\":{\"apiKey\":\"...\"}} (that file is local to this workspace and never committed), or set TRIPO_API_KEY.",
            },
          ],
        };
      }

      const dotnet = (st.runtime as { dotnet?: string } | undefined)?.dotnet ?? "dotnet";
      const toolDir = join(cwd, "tools", "tripo");
      const dll = join(toolDir, "bin", "Release", "net8.0", "tripo.dll");
      let buildWarning = "";
      if (!probeUpToDate(toolDir, dll)) {
        const r = await runAsync(dotnet, ["build", toolDir, "-c", "Release", "-v", "q", "-nologo"], { cwd });
        if (r.code !== 0) buildWarning = `WARN: tripo build failed (using the existing build if any): ${r.stderr.slice(0, 300)}\n`;
      }
      if (!existsSync(dll)) {
        throw new Error(`tools/tripo is not built and could not be built. Run: dotnet build tools/tripo -c Release`);
      }

      const args: string[] = [dll, params.action, "--key", key];
      if (params.action === "generate") {
        if (params.prompt) args.push("--prompt", params.prompt);
        if (params.image) args.push("--image", isAbsolute(params.image) ? params.image : resolve(cwd, params.image));
        if (!params.prompt && !params.image) throw new Error("generate needs either prompt= or image=");
        if (params.faceLimit != null) args.push("--face-limit", String(params.faceLimit));
        if (params.noTexture) args.push("--no-texture");
      }
      if (params.action === "convert") {
        if (!params.taskId) throw new Error("convert needs taskId= (from an earlier generate)");
        args.push("--task", params.taskId);
        if (params.static) args.push("--static");
      }
      const out = params.out
        ? isAbsolute(params.out)
          ? params.out
          : resolve(cwd, params.out)
        : undefined;
      if (out && params.action !== "balance") args.push("--out", out);
      // 默认导出成 Unity 就绪朝向（枪口/正面 +Z）；convert 不给 orientation 时 CLI 默认 -x

      const r = await runAsync(dotnet, args, { cwd });
      const text = ((buildWarning + r.stdout) + (r.stderr.trim() ? "\n" + r.stderr : "")).trim();
      if (r.code !== 0) {
        return {
          content: [{ type: "text", text: `FAIL: generate_model (${params.action}) exited ${r.code}\n${text.slice(0, 1500)}` }],
        };
      }
      const next =
        params.action === "generate"
          ? `\nNEXT: put this GLB in your mod folder and set it in config.json (e.g. {"target":"MP5","model":"${out ? out.split("/").pop() : "model.glb"}"}), then build and install the mod.`
          : "";
      return { content: [{ type: "text", text: text + next }] };
    },
  });
}
