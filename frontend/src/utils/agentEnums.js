export const agentStatus = {
  0: {
    label: "Stopped",
    className: "agent-stopped",
  },
  1: {
    label: "Starting",
    className: "agent-starting",
  },
  2: {
    label: "Running",
    className: "agent-running",
  },
  3: {
    label: "Investigating",
    className: "agent-investigating",
  },
  4: {
    label: "Executing",
    className: "agent-executing",
  },
  5: {
    label: "Failed",
    className: "agent-failed",
  },
  6: {
    label: "Disabled",
    className: "agent-disabled",
  },
};

export function getAgentStatus(value) {
  // Future-proof this for when the API
  // starts returning string enums.
  if (typeof value === "string") {
    return {
      label: value,
      className:
        `agent-${value.toLowerCase()}`,
    };
  }

  return (
    agentStatus[value] ?? {
      label: `Unknown (${value})`,
      className: "agent-unknown",
    }
  );
}