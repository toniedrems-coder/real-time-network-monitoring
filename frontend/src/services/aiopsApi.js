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

export function getWorkflows() {
  return request("/api/workflows");
}

export function getWorkflow(workflowId) {
  return request(`/api/workflows/${workflowId}`);
}

export function retryWorkflow(workflowId) {
  return request(`/api/workflows/${workflowId}/retry`, {
    method: "POST",
  });
}

export function getNotificationStatus() {
  return request("/api/notifications/status");
}

export function sendNotificationTest(channel) {
  return request(`/api/notifications/test/${encodeURIComponent(channel)}`, {
    method: "POST",
  });
}

export function getContainers() {
  return request("/api/containers");
}

export function getContainerLogs(id, tail = 100) {
  return request(`/api/containers/${encodeURIComponent(id)}/logs?tail=${encodeURIComponent(tail)}`);
}

export function getEndpoints() {
  return request("/endpoints");
}

export function getRemediations() {
  return request("/api/remediations");
}

export function createRemediation(payload) {
  return request("/api/remediations", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export function approveRemediation(id) {
  return request(`/api/remediations/${id}/approve`, {
    method: "POST",
    body: JSON.stringify({}),
  });
}

export function rejectRemediation(id, reason) {
  return request(`/api/remediations/${id}/reject`, {
    method: "POST",
    body: JSON.stringify({ reason }),
  });
}

export function executeRemediation(id) {
  return request(`/api/remediations/${id}/execute`, {
    method: "POST",
  });
}

export function verifyRemediation(id) {
  return request(`/api/remediations/${id}/verify`, {
    method: "POST",
  });
}
export function scanLogsForIps(tail = 500) {
  return request(`/api/log-discovery/scan?tail=${encodeURIComponent(tail)}`);
}

export function addDiscoveredIps(items) {
  return request("/api/log-discovery/add", {
    method: "POST",
    body: JSON.stringify(items),
  });
}

export function getAlertTransport() {
  return request("/api/alerts/transport");
}

export function getAlertContacts() {
  return request("/api/alerts/contacts");
}

export function saveAlertContact(contact) {
  return request(contact.id ? `/api/alerts/contacts/${contact.id}` : "/api/alerts/contacts", {
    method: contact.id ? "PUT" : "POST",
    body: JSON.stringify(contact),
  });
}

export function deleteAlertContact(id) {
  return request(`/api/alerts/contacts/${id}`, { method: "DELETE" });
}

export function getAlertRules() {
  return request("/api/alerts/rules");
}

export function saveAlertRule(rule) {
  return request(rule.id ? `/api/alerts/rules/${rule.id}` : "/api/alerts/rules", {
    method: rule.id ? "PUT" : "POST",
    body: JSON.stringify(rule),
  });
}

export function deleteAlertRule(id) {
  return request(`/api/alerts/rules/${id}`, { method: "DELETE" });
}

export function getAlertEvents(limit = 200) {
  return request(`/api/alerts/events?limit=${encodeURIComponent(limit)}`);
}
