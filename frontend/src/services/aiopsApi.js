const API_BASE =
  import.meta.env.VITE_API_BASE_URL ??
  "http://localhost:5011";

async function request(path, options = {}) {
  const token = localStorage.getItem("accessToken");

  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(token
        ? { Authorization: `Bearer ${token}` }
        : {}),
      ...options.headers,
    },
  });

  if (!response.ok) {
    const text = await response.text();

    throw new Error(
      text ||
        `Request failed with status ${response.status}`
    );
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

export function getIncidents() {
  return request("/api/incidents");
}

export function getIncident(id) {
  return request(`/api/incidents/${id}`);
}

export function runRca(incidentId) {
  return request(
    `/api/rca/${incidentId}/analyze`,
    {
      method: "POST",
    }
  );
}

export function getAgents() {
  return request("/api/agents");
}

export function executeAgent(
  agentId,
  context = {}
) {
  return request(
    `/api/agents/${agentId}/execute`,
    {
      method: "POST",
      body: JSON.stringify(context),
    }
  );
}

export function getRca(incidentId) {
  return request(
    `/api/rca/${incidentId}`
  );
}

// export function runRca(incidentId) {
//   return request(
//     `/api/rca/${incidentId}/analyze`,
//     {
//       method: "POST",
//     }
//   );
// }

export function runInvestigation(incidentId) {
  return request(
    `/api/investigations/${incidentId}/execute`,
    {
      method: "POST",
    }
  );
}