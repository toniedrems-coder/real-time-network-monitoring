const stageDone = (workflows, name) =>
  workflows.some((w) => w.stageHistory?.some((s) => s.name === name && s.status === "Completed"));
const stageFailed = (workflows, name) =>
  workflows.some((w) => w.stageHistory?.some((s) => s.name === name && s.status === "Failed"));
const stageRunning = (workflows, name) =>
  workflows.some((w) => w.currentStage === name);

function stageState(workflows, name) {
  if (stageFailed(workflows, name)) return "failed";
  if (stageRunning(workflows, name)) return "active";
  return stageDone(workflows, name) ? "done" : "idle";
}

export function workflowFlowSteps(workflows, endpointCount = null) {
  const detail = (name) => {
    const count = workflows.filter((w) =>
      w.stageHistory?.some((s) => s.name === name && s.status === "Completed")).length;
    return count ? `${count} completed` : undefined;
  };
  const steps = [
    { label: "Endpoint probe", state: endpointCount === null ? "idle" : endpointCount > 0 ? "done" : "idle", detail: endpointCount === null ? undefined : `${endpointCount} monitored` },
    { label: "Anomaly detected", state: workflows.length ? "done" : "idle", detail: workflows.length ? `${workflows.length} workflows` : undefined },
    { label: "Incident", state: stageState(workflows, "Create or update incident"), detail: detail("Create or update incident") },
    { label: "RCA + knowledge", state: stageState(workflows, "RCA and knowledge search"), detail: detail("RCA and knowledge search") },
    { label: "Remediation", state: stageState(workflows, "Create guarded remediation recommendation"), detail: detail("Create guarded remediation recommendation") },
    { label: "Notify", state: stageState(workflows, "Send email, SMS, and webhook notifications"), detail: detail("Send email, SMS, and webhook notifications") },
  ];
  return steps;
}

const kubernetesActions = new Set(["RestartPod", "RestartDeployment", "ScaleDeployment", 2, 3, 4]);
const statusOf = (r) => r.status;

export function kubernetesFlowSteps(remediations, names) {
  const items = remediations.filter((r) => kubernetesActions.has(r.actionType));
  const name = (r) => (typeof statusOf(r) === "number" ? names[statusOf(r)] : statusOf(r));
  const count = (...statuses) => items.filter((r) => statuses.includes(name(r))).length;
  const fmt = (n) => (n ? `${n}` : undefined);
  return [
    { label: "Proposed", state: items.length ? "done" : "idle", detail: fmt(items.length) },
    { label: "Awaiting approval", state: count("AwaitingApproval", "Proposed") ? "active" : items.length ? "done" : "idle", detail: fmt(count("AwaitingApproval", "Proposed")) },
    { label: "Approved", state: count("Approved") ? "active" : count("Executing", "Verifying", "Succeeded", "Failed") ? "done" : "idle", detail: fmt(count("Approved")) },
    { label: "Execute on cluster", state: count("Failed") ? "failed" : count("Executing") ? "active" : count("Verifying", "Succeeded") ? "done" : "idle", detail: fmt(count("Executing", "Succeeded", "Failed")) },
    { label: "Verify", state: count("Verifying") ? "active" : items.some((r) => r.verificationSuccessful === true) ? "done" : items.some((r) => r.verificationSuccessful === false) ? "failed" : "idle", detail: fmt(items.filter((r) => r.verificationSuccessful != null).length) },
  ];
}
