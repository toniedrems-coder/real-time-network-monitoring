// export const apiBase = import.meta.env.VITE_API_BASE_URL || "http://localhost:5011";
// export const accessToken = import.meta.env.VITE_API_ACCESS_TOKEN || "";

// export function apiFetch(path, options = {}) {
//   const headers = new Headers(options.headers);
//   if (accessToken) headers.set("Authorization", `Bearer ${accessToken}`);
//   return fetch(`${apiBase}${path}`, { ...options, headers });
// }

// export function signalRAccessTokenFactory() {
//   return accessToken;
// }



// export const apiBase =
//   import.meta.env.VITE_API_BASE_URL || "http://localhost:5011";

// export const accessToken =
//   import.meta.env.VITE_API_ACCESS_TOKEN || "";

// export function getAccessToken() {
//   return localStorage.getItem("accessToken") || accessToken;
// }

// export function apiFetch(path, options = {}) {
//   const headers = new Headers(options.headers);
//   const token = getAccessToken();

//   if (token) {
//     headers.set("Authorization", `Bearer ${token}`);
//   }

//   return fetch(`${apiBase}${path}`, {
//     ...options,
//     headers,
//   });
// }

// export function signalRAccessTokenFactory() {
//   return getAccessToken();
// }



//-----------------------------------------------------------------

// export const apiBase =
//   import.meta.env.VITE_API_BASE_URL || "http://localhost:5011";

// const environmentToken =
//   import.meta.env.VITE_API_ACCESS_TOKEN || "";

// export function getAccessToken() {
//   return (
//     localStorage.getItem("accessToken") ||
//     environmentToken
//   );
// }

// export function setAccessToken(token) {
//   if (!token) {
//     throw new Error("No access token was supplied.");
//   }

//   localStorage.setItem("accessToken", token);
// }

// export function clearAccessToken() {
//   localStorage.removeItem("accessToken");
// }

// export function apiFetch(path, options = {}) {
//   const headers = new Headers(options.headers);
//   const token = getAccessToken();

//   if (token) {
//     headers.set("Authorization", `Bearer ${token}`);
//   }

//   if (options.body && !(options.body instanceof FormData)) {
//     headers.set("Content-Type", "application/json");
//   }

//   return fetch(`${apiBase}${path}`, {
//     ...options,
//     headers,
//   });
// }

// export function signalRAccessTokenFactory() {
//   return getAccessToken();
// }

//=======================================================

// export const apiBase =
//   import.meta.env.VITE_API_BASE_URL || "http://localhost:5011";

// const configuredToken =
//   import.meta.env.VITE_API_ACCESS_TOKEN || "";

// export function getAccessToken() {
//   return (
//     localStorage.getItem("accessToken") ||
//     configuredToken
//   );
// }

// export function setAccessToken(token) {
//   if (!token) {
//     throw new Error("No access token was returned.");
//   }

//   localStorage.setItem("accessToken", token);
// }

// export function clearAccessToken() {
//   localStorage.removeItem("accessToken");
// }

// export async function requestDevAccessToken() {
//   const response = await fetch(`${apiBase}/connect/token`, {
//     method: "POST",
//     headers: {
//       "Content-Type": "application/x-www-form-urlencoded",
//     },
//     body: new URLSearchParams({
//       grant_type: "client_credentials",
//       client_id:
//         import.meta.env.VITE_AUTH_CLIENT_ID ||
//         "dev-client",
//       client_secret:
//         import.meta.env.VITE_AUTH_CLIENT_SECRET ||
//         "dev-secret",
//       audience: "network-monitoring-api",
//     }),
//   });

//   const result = await response.json();

//   if (!response.ok) {
//     throw new Error(
//       result.error_description ||
//       result.error ||
//       `Token request failed: ${response.status}`
//     );
//   }

//   // OAuth token responses normally use access_token.
//   const token =
//     result.access_token ||
//     result.accessToken ||
//     result.token;

//   if (!token) {
//     console.error("Token response:", result);
//     throw new Error(
//       "The token endpoint did not return an access token."
//     );
//   }

//   setAccessToken(token);

//   return token;
// }

// export async function apiFetch(path, options = {}) {
//   let token = getAccessToken();

//   // Obtain a development token when none is available.
//   if (!token) {
//     token = await requestDevAccessToken();
//   }

//   const headers = new Headers(options.headers);

//   headers.set("Authorization", `Bearer ${token}`);
//   headers.set("Accept", "application/json");

//   if (options.body && !(options.body instanceof FormData)) {
//     headers.set("Content-Type", "application/json");
//   }

//   let response = await fetch(`${apiBase}${path}`, {
//     ...options,
//     headers,
//   });

//   // The stored token might have expired.
//   if (response.status === 401) {
//     clearAccessToken();

//     token = await requestDevAccessToken();
//     headers.set("Authorization", `Bearer ${token}`);

//     response = await fetch(`${apiBase}${path}`, {
//       ...options,
//       headers,
//     });
//   }

//   return response;
// }

// export function signalRAccessTokenFactory() {
//   return getAccessToken();
// }





export const apiBase =
  import.meta.env.VITE_API_BASE_URL ||
  "http://localhost:5011";

const clientId =
  import.meta.env.VITE_AUTH_CLIENT_ID ||
  "dev-client";

const clientSecret =
  import.meta.env.VITE_AUTH_CLIENT_SECRET ||
  "dev-secret";

const TOKEN_KEY = "accessToken";
const TOKEN_EXPIRY_KEY = "accessTokenExpiresAt";

let tokenRequestPromise = null;

export function getAccessToken() {
  const token = localStorage.getItem(TOKEN_KEY);
  const expiresAt = Number(
    localStorage.getItem(TOKEN_EXPIRY_KEY) || 0
  );

  // Treat the token as expired 30 seconds early.
  if (!token || Date.now() >= expiresAt - 30_000) {
    return null;
  }

  return token;
}

export function clearAccessToken() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(TOKEN_EXPIRY_KEY);
}

function storeAccessToken(token, expiresIn) {
  const expiresAt =
    Date.now() + Number(expiresIn || 3600) * 1000;

  localStorage.setItem(TOKEN_KEY, token);
  localStorage.setItem(
    TOKEN_EXPIRY_KEY,
    expiresAt.toString()
  );
}

export async function requestAccessToken() {
  const response = await fetch(
    `${apiBase}/connect/token`,
    {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type":
          "application/x-www-form-urlencoded",
      },
      body: new URLSearchParams({
        grant_type: "client_credentials",
        client_id: clientId,
        client_secret: clientSecret,
      }),
    }
  );

  const result = await response.json();

  if (!response.ok) {
    throw new Error(
      result.error ||
      `Token request failed with status ${response.status}`
    );
  }

  // Your controller returns access_token.
  if (!result.access_token) {
    throw new Error(
      "The token response did not contain access_token."
    );
  }

  storeAccessToken(
    result.access_token,
    result.expires_in
  );

  return result.access_token;
}

export async function ensureAccessToken() {
  const existingToken = getAccessToken();

  if (existingToken) {
    return existingToken;
  }

  // Prevent several dashboard requests from obtaining
  // separate tokens at the same time.
  if (!tokenRequestPromise) {
    tokenRequestPromise = requestAccessToken().finally(() => {
      tokenRequestPromise = null;
    });
  }

  return tokenRequestPromise;
}

async function sendRequest(path, options, token) {
  const headers = new Headers(options.headers);

  headers.set("Authorization", `Bearer ${token}`);
  headers.set("Accept", "application/json");

  if (
    options.body &&
    !(options.body instanceof FormData)
  ) {
    headers.set("Content-Type", "application/json");
  }

  const normalizedPath = path.startsWith("/")
    ? path
    : `/${path}`;

  return fetch(
    `${apiBase.replace(/\/$/, "")}${normalizedPath}`,
    {
      ...options,
      headers,
    }
  );
}

export async function apiFetch(path, options = {}) {
  let token = await ensureAccessToken();
  let response = await sendRequest(
    path,
    options,
    token
  );

  // Token may have been invalidated or expired.
  if (response.status === 401) {
    clearAccessToken();

    token = await ensureAccessToken();
    response = await sendRequest(
      path,
      options,
      token
    );
  }

  return response;
}

export async function signalRAccessTokenFactory() {
  return ensureAccessToken();
}