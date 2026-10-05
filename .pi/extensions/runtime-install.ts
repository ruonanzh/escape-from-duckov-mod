import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { probeDotnet } from "../lib/dotnet";

/**
 * install_runtime — 运行时契约（docs/mod-repo-guide.md §4）
 * 引导安装 .NET SDK（用户级 ~/.dotnet 优先，无权限；系统级备选）。
 * 只管引导装，不管删。（dotnet 探测与 check_runtime 共用 .pi/lib/dotnet.ts）
 * 返回指引不等于已装：details.ok 表示「SDK 已就绪」这个前提是否满足，与 check_runtime 一致。
 */
export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "install_runtime",
    label: "Install Runtime",
    description: "Return platform-specific instructions for installing .NET SDK >= 8, or report that it is already present. Read-only probing only: this tool does NOT run an installer, download software, or locate the game.",
    promptSnippet: "Get SDK installation instructions when an SDK prerequisite is missing",
    promptGuidelines: ["Use install_runtime when check_runtime reports a missing dotnet SDK."],
    parameters: Type.Object({}),
    async execute(_toolCallId, _params, _signal, _onUpdate, _ctx) {
      const dotnet = probeDotnet();
      if (dotnet.found && (dotnet.major ?? 0) >= 8) {
        return { content: [{ type: "text", text: `SKIP: dotnet SDK ${dotnet.version} already present; no installation performed.` }], details: { ok: true } };
      }

      const lines = ["INSTRUCTIONS PROVIDED: No installation has been performed.", "Install .NET SDK 8.0 - user-level install (no admin/UAC, recommended):"];
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
        "NEXT: after installing, call check_runtime again.",
      );

      return {
        content: [{ type: "text", text: lines.join("\n") }],
        // ok = the SDK prerequisite is satisfied. Guidance alone does not satisfy it -> false
        // (same semantics as check_runtime, which returns ok:false when the SDK is missing).
        details: { ok: false },
      };
    },
  });
}
