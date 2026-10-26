export const TRIPO_API_TIMEOUT_MS = 30_000;
export const TRIPO_DOWNLOAD_TIMEOUT_MS = 120_000;

export type TripoTimeoutCode = "TRIPO_API_TIMEOUT" | "TRIPO_DOWNLOAD_TIMEOUT";

export interface TripoNetworkOptions {
  timeoutMs: number;
  timeoutCode: TripoTimeoutCode;
  signal?: AbortSignal;
}

export function tripoCancelled(): Error {
  return new Error(
    "TRIPO_CANCELLED: the call was cancelled. Reuse an existing task_id when available; do not start a duplicate paid task.",
  );
}

export function throwIfTripoCancelled(signal?: AbortSignal): void {
  if (signal?.aborted) throw tripoCancelled();
}

function timeoutError(code: TripoTimeoutCode): Error {
  return new Error(
    code === "TRIPO_API_TIMEOUT"
      ? "TRIPO_API_TIMEOUT: the API call exceeded 30 seconds. Check the network before retrying; do not retry in a loop."
      : "TRIPO_DOWNLOAD_TIMEOUT: the download exceeded 120 seconds. Reuse the existing task_id and retry the download; do not generate the asset again.",
  );
}

/** Run one complete HTTP operation, including reading its response body, under caller cancellation and a timeout. */
export async function tripoNetworkRequest<T>(
  options: TripoNetworkOptions,
  operation: (signal: AbortSignal) => Promise<T>,
): Promise<T> {
  throwIfTripoCancelled(options.signal);
  const controller = new AbortController();
  const onAbort = () => controller.abort(options.signal?.reason);
  options.signal?.addEventListener("abort", onAbort, { once: true });
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, options.timeoutMs);
  try {
    return await operation(controller.signal);
  } catch (error) {
    if (options.signal?.aborted) throw tripoCancelled();
    if (timedOut) throw timeoutError(options.timeoutCode);
    if (error instanceof Error && error.message.startsWith("TRIPO_")) throw error;
    const message = error instanceof Error ? error.message : String(error);
    throw new Error(
      `TRIPO_NETWORK_ERROR: ${message} Check the network before retrying; reuse an existing task_id when available.`,
    );
  } finally {
    clearTimeout(timer);
    options.signal?.removeEventListener("abort", onAbort);
  }
}

/** Sleep between polls without leaving a timer behind after cancellation. */
export function tripoSleep(ms: number, signal?: AbortSignal): Promise<void> {
  throwIfTripoCancelled(signal);
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      signal?.removeEventListener("abort", onAbort);
      resolve();
    }, ms);
    const onAbort = () => {
      clearTimeout(timer);
      signal?.removeEventListener("abort", onAbort);
      reject(tripoCancelled());
    };
    signal?.addEventListener("abort", onAbort, { once: true });
  });
}
