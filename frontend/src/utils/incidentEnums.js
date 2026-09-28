export const incidentStatus = {
  0: {
    label: "Open",
    className: "status-open",
  },
  1: {
    label: "Investigating",
    className: "status-investigating",
  },
  2: {
    label: "Remediating",
    className: "status-remediating",
  },
  3: {
    label: "Resolved",
    className: "status-resolved",
  },
  4: {
    label: "Escalated",
    className: "status-escalated",
  },
};

export const incidentSeverity = {
  0: {
    label: "P1",
    className: "severity-p1",
  },
  1: {
    label: "P2",
    className: "severity-p2",
  },
  2: {
    label: "P3",
    className: "severity-p3",
  },
  3: {
    label: "P4",
    className: "severity-p4",
  },
};

export const failureType = {
  0: "Unknown",
  1: "Timeout",
  2: "HttpError",
  3: "Unavailable",
  4: "Anomaly",
};

export function getStatus(value) {
  return (
    incidentStatus[value] ?? {
      label: `Unknown (${value})`,
      className: "status-unknown",
    }
  );
}

export function getSeverity(value) {
  return (
    incidentSeverity[value] ?? {
      label: `Unknown (${value})`,
      className: "severity-unknown",
    }
  );
}

export function getFailureType(value) {
  return failureType[value] ?? `Unknown (${value})`;
}