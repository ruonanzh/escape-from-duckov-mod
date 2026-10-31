import {
  cpSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const root = mkdtempSync(join(tmpdir(), "duckov-tripo-network-"));
const realFetch = globalThis.fetch;
const previousKey = process.env.TRIPO_API_KEY;

function copyWithTsExtensions(from, to) {
  cpSync(from, to, { recursive: true, preserveTimestamps: true });
  const walk = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const path = join(dir, entry.name);
      if (entry.isDirectory()) walk(path);
      else if (entry.name.endsWith(".ts")) {
        writeFileSync(
          path,
          readFileSync(path, "utf8").replace(/(from\s+")(\.\.?\/[^"]+?)(")/g, (match, start, spec, end) =>
            spec.endsWith(".ts") || spec.endsWith(".js") ? match : `${start}${spec}.ts${end}`,
          ),
        );
      }
    }
  };
  walk(to);
}

function writeStubs(dir) {
  const typebox = join(dir, "node_modules", "typebox");
  mkdirSync(typebox, { recursive: true });
  writeFileSync(join(typebox, "package.json"), JSON.stringify({ name: "typebox", type: "module", main: "index.js" }));
  writeFileSync(
    join(typebox, "index.js"),
    `const s = (kind) => (opts) => ({ kind, ...(opts || {}) });
export const Type = { Object: s("Object"), String: s("String"), Number: s("Number"), Boolean: s("Boolean"),
  Optional: (inner) => ({ kind: "Optional", inner }), Union: s("Union"), Literal: s("Literal") };
`,
  );
  const pi = join(dir, "node_modules", "@earendil-works", "pi-coding-agent");
  mkdirSync(pi, { recursive: true });
  writeFileSync(
    join(pi, "package.json"),
    JSON.stringify({ name: "@earendil-works/pi-coding-agent", type: "module", main: "index.js" }),
  );
  writeFileSync(join(pi, "index.js"), "export {};\n");
}

const textOf = (result) => result?.content?.map((item) => item.text).join("\n") ?? String(result);
let failed = 0;
const check = (name, condition, result) => {
  console.log(`${condition ? "PASS" : "FAIL"}  ${name}`);
  if (!condition) {
    failed += 1;
    if (result !== undefined) console.log(textOf(result));
  }
};
const errorOf = async (promise) => {
  try {
    await promise;
    return new Error("EXPECTED_REJECTION: operation passed");
  } catch (error) {
    return error;
  }
};
const json = (data, status = 200) => new Response(JSON.stringify({ code: 0, data }), {
  status,
  headers: { "Content-Type": "application/json" },
});
const taskId = (n) => `00000000-0000-4000-8000-${String(n).padStart(12, "0")}`;

try {
  copyWithTsExtensions(join(repoRoot, ".pi"), join(root, ".pi"));
  writeStubs(root);
  process.env.TRIPO_API_KEY = "test-key";
  const tools = new Map();
  const pi = { registerTool(definition) { tools.set(definition.name, definition); }, sendMessage() {} };
  const tripoModule = await import(pathToFileURL(join(root, ".pi", "extensions", "tripo.ts")).href);
  const register = tripoModule.default;
  register(pi);
  check(
    "model output already in .preview does not create a nested preview directory",
    tripoModule.previewDirFor(join(root, "mod", ".preview", "mimi.glb")) === join(root, "mod", ".preview"),
  );
  check(
    "model output in the mod root keeps previews in .preview",
    tripoModule.previewDirFor(join(root, "mod", "mimi.glb")) === join(root, "mod", ".preview"),
  );
  const image = (params, signal) => tools.get("generate_image").execute("test", params, signal, undefined, { cwd: root });
  const model = (params, signal) => tools.get("generate_model").execute("test", params, signal, undefined, { cwd: root });
  const http = await import(pathToFileURL(join(root, ".pi", "lib", "tripo-http.ts")).href);

  let active = 0;
  let maxActive = 0;
  let generationCalls = 0;
  globalThis.fetch = async (url) => {
    const value = String(url);
    if (value.includes("/generation/text-to-image")) {
      generationCalls += 1;
      active += 1;
      maxActive = Math.max(maxActive, active);
      await new Promise((resolveDelay) => setTimeout(resolveDelay, 25));
      active -= 1;
      return json({ task_id: taskId(generationCalls) });
    }
    if (value.includes("/tasks/")) return json({ status: "success", output: { rendered_image_url: "https://assets.test/image.png" } });
    if (value === "https://assets.test/image.png") return new Response(new Uint8Array([1, 2, 3]));
    throw new Error(`unexpected URL: ${value}`);
  };
  const concurrent = await Promise.all([
    image({ prompt: "first", out: join(root, "first.png") }),
    image({ prompt: "second", out: join(root, "second.png") }),
  ]);
  check(
    "two paid image generations use one global serial queue",
    maxActive === 1 && generationCalls === 2 && concurrent.every((result) => textOf(result).startsWith("PASS:")),
    { maxActive, generationCalls },
  );

  active = 0;
  maxActive = 0;
  let paidEndpoints = 0;
  globalThis.fetch = async (url) => {
    const value = String(url);
    if (value.includes("/generation/text-to-image") || value.includes("/models/convert")) {
      paidEndpoints += 1;
      active += 1;
      maxActive = Math.max(maxActive, active);
      await new Promise((resolveDelay) => setTimeout(resolveDelay, 25));
      active -= 1;
      return json({ task_id: taskId(30 + paidEndpoints) });
    }
    if (value.includes(`/tasks/${taskId(31)}`)) return json({ status: "success", output: { rendered_image_url: "https://assets.test/global.png" } });
    if (value.includes(`/tasks/${taskId(32)}`)) return json({ status: "success", output: { model_url: "https://assets.test/global.glb" } });
    if (value.includes("/tasks/")) return json({ status: "success", output: { rendered_image_url: "https://assets.test/global.png" } });
    if (value === "https://assets.test/global.png" || value === "https://assets.test/global.glb") return new Response(new Uint8Array([1]));
    throw new Error(`unexpected URL: ${value}`);
  };
  const globalPair = await Promise.all([
    image({ prompt: "global image", out: join(root, "global.png") }),
    model({ action: "convert", taskId: taskId(20), out: join(root, "global.glb") }),
  ]);
  check(
    "image and model paid actions share the same global queue",
    maxActive === 1 && globalPair.every((result) => textOf(result).startsWith("PASS:")),
    { maxActive },
  );

  let rateCalls = 0;
  globalThis.fetch = async () => {
    rateCalls += 1;
    return new Response(JSON.stringify({ code: 2000, message: "limited" }), { status: 429 });
  };
  const rateLimit = await errorOf(image({ prompt: "limited", out: join(root, "limited.png") }));
  check(
    "429 fails once without a retry loop",
    rateCalls === 1 && rateLimit.message.startsWith("TRIPO_RATE_LIMIT:"),
    rateLimit,
  );

  const controller = new AbortController();
  let polls = 0;
  globalThis.fetch = async (url) => {
    const value = String(url);
    if (value.includes("/generation/text-to-image")) return json({ task_id: taskId(10) });
    if (value.includes("/tasks/")) {
      polls += 1;
      controller.abort();
      return json({ status: "running" });
    }
    throw new Error(`unexpected URL: ${value}`);
  };
  const cancelled = await errorOf(image({ prompt: "cancel", out: join(root, "cancel.png") }, controller.signal));
  await new Promise((resolveDelay) => setTimeout(resolveDelay, 20));
  check(
    "caller cancellation stops polling and has a distinct error code",
    cancelled.message.startsWith("TRIPO_CANCELLED:") && polls === 1,
    cancelled,
  );

  const inFlightController = new AbortController();
  globalThis.fetch = (_url, init) => new Promise((_, reject) => {
    const rejectAbort = () => reject(new DOMException("aborted", "AbortError"));
    if (init.signal.aborted) rejectAbort();
    else init.signal.addEventListener("abort", rejectAbort, { once: true });
  });
  const inFlightPromise = image({ prompt: "in flight", out: join(root, "in-flight.png") }, inFlightController.signal);
  setTimeout(() => inFlightController.abort(), 10);
  const inFlight = await errorOf(inFlightPromise);
  check(
    "caller cancellation aborts an in-flight fetch",
    inFlight.message.startsWith("TRIPO_CANCELLED:"),
    inFlight,
  );

  globalThis.fetch = async () => { throw new TypeError("socket reset"); };
  const network = await errorOf(image({ prompt: "network", out: join(root, "network.png") }));
  check(
    "network failure is not reported as caller cancellation",
    network.message.startsWith("TRIPO_NETWORK_ERROR:") && !network.message.startsWith("TRIPO_CANCELLED:"),
    network,
  );

  const abortOnSignal = (signal) => new Promise((_, reject) => {
    signal.addEventListener("abort", () => reject(new DOMException("aborted", "AbortError")), { once: true });
  });
  const apiTimeout = await errorOf(http.tripoNetworkRequest(
    { timeoutMs: 10, timeoutCode: "TRIPO_API_TIMEOUT" },
    abortOnSignal,
  ));
  const downloadTimeout = await errorOf(http.tripoNetworkRequest(
    { timeoutMs: 10, timeoutCode: "TRIPO_DOWNLOAD_TIMEOUT" },
    abortOnSignal,
  ));
  check(
    "API and download timeouts keep distinct error codes",
    apiTimeout.message.startsWith("TRIPO_API_TIMEOUT:") && downloadTimeout.message.startsWith("TRIPO_DOWNLOAD_TIMEOUT:"),
    [apiTimeout, downloadTimeout],
  );
  check(
    "production timeout defaults are 30 seconds for API and 120 seconds for downloads",
    http.TRIPO_API_TIMEOUT_MS === 30_000 && http.TRIPO_DOWNLOAD_TIMEOUT_MS === 120_000,
  );

  let reusedGenerationCalls = 0;
  globalThis.fetch = async (url) => {
    const value = String(url);
    if (value.includes("/generation/")) reusedGenerationCalls += 1;
    if (value.includes("/tasks/")) return json({ status: "success", output: { rendered_image_url: "https://assets.test/reused.png" } });
    if (value === "https://assets.test/reused.png") return new Response(new Uint8Array([4, 5, 6]));
    throw new Error(`unexpected URL: ${value}`);
  };
  const reused = await image({ taskId: taskId(20), out: join(root, "reused.png") });
  check(
    "an existing task id reuses its render without another paid generation",
    reusedGenerationCalls === 0 && textOf(reused).startsWith("PASS:") && existsSync(join(root, "reused.png")),
    reused,
  );

  globalThis.fetch = async (url) => {
    const value = String(url);
    if (value.includes("/tasks/")) return json({ status: "success", output: { rendered_image_url: "https://assets.test/failed.png" } });
    if (value === "https://assets.test/failed.png") return new Response("failed", { status: 500 });
    throw new Error(`unexpected URL: ${value}`);
  };
  const failedDownload = await errorOf(image({ taskId: taskId(20), out: join(root, "download-failure.png") }));
  const failureRows = readFileSync(join(root, "asset_manifest.jsonl"), "utf8").trim().split("\n").map((line) => JSON.parse(line));
  check(
    "download failure records the paid task id in the manifest",
    failedDownload.message.startsWith("TRIPO_DOWNLOAD_FAILED:") &&
      failureRows.some((row) => row.request_id === taskId(20) && row.error?.includes("TRIPO_DOWNLOAD_FAILED") && row.files.length === 0),
    failedDownload,
  );

  globalThis.fetch = async (url) => {
    const value = String(url);
    if (value.includes("/tasks/")) return json({ status: "success", output: { rendered_image_url: "https://assets.test/reused.png" } });
    if (value === "https://assets.test/reused.png") return new Response(new Uint8Array([4, 5, 6]));
    throw new Error(`unexpected URL: ${value}`);
  };
  rmSync(join(root, "asset_manifest.jsonl"), { force: true });
  mkdirSync(join(root, "asset_manifest.jsonl"));
  const manifestWarn = await image({ taskId: taskId(20), out: join(root, "manifest-warning.png") });
  check(
    "manifest write failure only warns and keeps the downloaded artifact",
    textOf(manifestWarn).includes("WARN:") && existsSync(join(root, "manifest-warning.png")),
    manifestWarn,
  );
} finally {
  globalThis.fetch = realFetch;
  if (previousKey === undefined) delete process.env.TRIPO_API_KEY;
  else process.env.TRIPO_API_KEY = previousKey;
  rmSync(root, { recursive: true, force: true });
}

console.log(failed ? `FAIL: ${failed} Tripo network regression check(s)` : "PASS: all Tripo network regression checks");
process.exit(failed ? 1 : 0);
