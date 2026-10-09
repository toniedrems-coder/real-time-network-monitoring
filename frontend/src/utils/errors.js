export function readError(error, fallback = "Request failed.") {
  const text = error?.message ?? "";
  try {
    const body = JSON.parse(text);
    if (body.errors) return Object.values(body.errors).flat().join(" ");
    if (body.message) return body.message;
  } catch {
    // not JSON
  }
  return text || fallback;
}
