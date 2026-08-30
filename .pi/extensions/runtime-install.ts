import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";
import os from "node:os";

/**
 * install_runtime — 运行时契约（docs/mod-repo-guide.md §4）
 * 引导安装 .NET SDK（用户级 ~/.dotnet 优先，无权限；系统级备选）。
 * 只管引导装，不管删。
 */
export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "install_runtime",
    label: "Install Runtime",
    description: "Guide installation of the mod runtime (.NET SDK 8.0)",
    promptSnippet: "Install mod runtime",
    promptGuidelines: ["Use install_runtime when check_runtime reports a missing dotnet SDK."],
    parameters: Type.Object({}),
    async execute(_toolCallId, _params, _signal, _onUpdate, _ctx) {
      const v = findDotnet();
      if (v) {
        const major = parseInt(v.split(".")[0], 10);
        if (major >= 8) {
          return { content: [{ type: "text", text: `SKIP: dotnet SDK ${v} already present` }], details: { ok: true } };
        }
      }

      const lines = ["Install .NET SDK 8.0 — user-level install (no admin/UAC, recommended):"];
      if (process.platform === "win32") {
        lines.push(
          "  PowerShell:",
          '  iex "& { $(irm https://dot.net/v1/dotnet-install.ps1) } -Channel 8.0"',
          "  -> installs to %USERPROFILE%\\.dotnet, no admin required",
          "",
          "Alternate (system-level, needs admin/UAC):",
          "  winget install Microsoft.DotNet.SDK.8",
        );
      } else if (process.platform === "darwin") {
        lines.push(
          "  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0",
          "  -> installs to ~/.dotnet, no password required",
          "",
          "Alternate (system-level, needs password):",
          "  brew install --cask dotnet-sdk",
        );
      } else {
        lines.push("  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0");
      }
      lines.push(
        "",
        "Note: we only guide the install; uninstalling is up to you",
        "(delete ~/.dotnet, or `brew uninstall --cask dotnet-sdk` / `winget uninstall Microsoft.DotNet.SDK.8`).",
        "After installing, call check_runtime again.",
      );

      return { content: [{ type: "text", text: lines.join("\n") }], details: { ok: true } };
    },
  });
}

function findDotnet() {
  const bin = process.platform === "win32" ? "dotnet.exe" : "dotnet";
  const tryRun = (p: string) => {
    try {
      return execFileSync(p, ["--version"], { encoding: "utf8" }).trim();
    } catch {
      return null;
    }
  };
  const fromPath = tryRun("dotnet");
  if (fromPath) return fromPath;
  const userLevel = join(os.homedir(), ".dotnet", bin);
  if (existsSync(userLevel)) return tryRun(userLevel);
  return null;
}
