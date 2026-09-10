export const apiBase = import.meta.env.VITE_API_BASE_URL || "http://localhost:5000";
export const accessToken = import.meta.env.VITE_API_ACCESS_TOKEN || "";

export function apiFetch(path, options = {}) {
  const headers = new Headers(options.headers);
  if (accessToken) headers.set("Authorization", `Bearer ${accessToken}`);
  return fetch(`${apiBase}${path}`, { ...options, headers });
}

export function signalRAccessTokenFactory() {
  return accessToken;
}