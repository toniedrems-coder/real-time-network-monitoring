import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  approveRemediation,
  createRemediation,
  executeRemediation,
  getIncidents,
  getRemediations,
  getWorkflows,
  rejectRemediation,
  retryWorkflow,
  verifyRemediation,
} from "../services/aiopsApi";
import useAiOpsSignalR from "../hooks/useAiOpsSignalR";
import ExecutionFlow from "../components/ExecutionFlow";
import Pagination from "../components/Pagination";

const WORKFLOW_PAGE_SIZE = 10;
import { kubernetesFlowSteps, workflowFlowSteps } from "../utils/flows";

const remediationStatuses = [
  "Proposed",
  "AwaitingApproval",
  "Approved",
  "Executing",
  "Verifying",
  "Succeeded",
  "Failed",
  "Rejected",
  "Escalated",
];

const remediationActions = [
  "RecheckEndpoint",
  "RestartService",
  "RestartPod",
  "RestartDeployment",
  "ScaleDeployment",
  "ClearCache",
  "ExecuteRunbook",
];

function statusName(status) {
  return typeof status === "number"
    ? remediationStatuses[status] ?? `Status ${status}`
    : status;
}

function actionName(action) {
  return typeof action === "number"
    ? remediationActions[action] ?? `Action ${action}`
    : action;
}

function dateLabel(value) {
  return value ? new Date(value).toLocaleString() : "—";
}

export default function OperationsPage() {
  const [workflows, setWorkflows] = useState([]);
  const [workflowPageRequest, setWorkflowPage] = useState(1);
  const workflowPages = Math.max(1, Math.ceil(workflows.length / WORKFLOW_PAGE_SIZE));
  const workflowPage = Math.min(workflowPageRequest, workflowPages);
  const pagedWorkflows = workflows.slice(
    (workflowPage - 1) * WORKFLOW_PAGE_SIZE,
    workflowPage * WORKFLOW_PAGE_SIZE,
  );
  const [remediations, setRemediations] = useState([]);
  const [remediationPageRequest, setRemediationPage] = useState(1);
  const remediationPages = Math.max(1, Math.ceil(remediations.length / WORKFLOW_PAGE_SIZE));
  const remediationPage = Math.min(remediationPageRequest, remediationPages);
  const pagedRemediations = remediations.slice(
    (remediationPage - 1) * WORKFLOW_PAGE_SIZE,
    remediationPage * WORKFLOW_PAGE_SIZE,
  );
  const [incidents, setIncidents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [workingId, setWorkingId] = useState(null);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [selectedIncident, setSelectedIncident] = useState("");
  const [selectedAction, setSelectedAction] = useState("2");
  const [kubernetesTarget, setKubernetesTarget] = useState("");

  const refresh = useCallback(async () => {
    setError("");
    try {
      const [workflowData, remediationData, incidentData] = await Promise.all([
        getWorkflows(),
        getRemediations(),
        getIncidents(),
      ]);
      setWorkflows(Array.isArray(workflowData) ? workflowData : []);
      setRemediations(Array.isArray(remediationData) ? remediationData : []);
      setIncidents(Array.isArray(incidentData) ? incidentData : []);
    } catch (requestError) {
      setError(requestError.message || "Unable to load operations data.");
    } finally {
      setLoading(false);
    }
  }, []);

  const signalRHandlers = useMemo(
    () => ({
      WorkflowUpdated: () => refresh(),
      RemediationUpdated: () => refresh(),
    }),
    [refresh],
  );

  useAiOpsSignalR(signalRHandlers);

  useEffect(() => {
    refresh();
  }, [refresh]);

  async function act(id, action, label) {
    setWorkingId(id);
    setError("");
    setNotice("");
    try {
      await action();
      setNotice(label);
      await refresh();
    } catch (requestError) {
      setError(requestError.message || `${label} failed.`);
    } finally {
      setWorkingId(null);
    }
  }

  async function reject(id) {
    const reason = window.prompt("Why is this remediation being rejected?");
    if (reason === null) return;
    if (!reason.trim()) {
      setError("A rejection reason is required.");
      return;
    }
    await act(id, () => rejectRemediation(id, reason), "Remediation rejected.");
  }

  const kubernetesAction = ["2", "3", "4"].includes(selectedAction);

  async function proposeRemediation(event) {
    event.preventDefault();
    if (!selectedIncident) {
      setError("Select an incident before proposing a remediation.");
      return;
    }
    await act("new-remediation", () => createRemediation({
      incidentId: selectedIncident,
      actionType: Number(selectedAction),
      ...(kubernetesAction ? { target: kubernetesTarget.trim() } : {}),
      requestedBy: "Operations",
    }), "Remediation recommendation created.");
  }

  return (
    <main className="page operations-page">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Agentic AIOps</p>
          <h1>Operations</h1>
          <p className="muted">Review automated workflows and control guarded remediation.</p>
        </div>
        <button className="button secondary" onClick={refresh} disabled={loading}>
          Refresh
        </button>
      </div>

      {error && <p className="operations-alert error" role="alert">{error}</p>}
      {notice && <p className="operations-alert success" role="status">{notice}</p>}

      <ExecutionFlow title="Incident workflow flow" steps={workflowFlowSteps(workflows)} />
      <ExecutionFlow title="Kubernetes remediation flow" steps={kubernetesFlowSteps(remediations, remediationStatuses)} />

      <section className="panel operations-section">
        <h2>Propose a guarded remediation</h2>
        <p className="muted">Kubernetes targets use namespace/resource-name; higher-risk actions remain escalated by policy.</p>
        <form className="remediation-form" onSubmit={proposeRemediation}>
          <label>
            Incident
            <select value={selectedIncident} required onChange={(event) => setSelectedIncident(event.target.value)}>
              <option value="">Choose an incident</option>
              {incidents.map((incident) => (
                <option key={incident.id} value={incident.id}>
                  {incident.incidentNumber} — {incident.title}
                </option>
              ))}
            </select>
          </label>
          <label>
            Action
            <select value={selectedAction} onChange={(event) => setSelectedAction(event.target.value)}>
              <option value="0">Recheck endpoint</option>
              <option value="2">Restart pod</option>
              <option value="3">Restart deployment</option>
              <option value="4">Scale deployment up</option>
            </select>
          </label>
          {kubernetesAction && (
            <label>
              Kubernetes target
              <input value={kubernetesTarget} required pattern="[a-z0-9]([-.a-z0-9]*[a-z0-9])?/[a-z0-9]([-a-z0-9.]*[a-z0-9])?"
                onChange={(event) => setKubernetesTarget(event.target.value)}
                placeholder="namespace/resource-name" />
            </label>
          )}
          <button className="button" type="submit" disabled={!selectedIncident || workingId === "new-remediation"}>
            {workingId === "new-remediation" ? "Creating…" : "Propose action"}
          </button>
        </form>
      </section>

      <section className="panel operations-section">
        <div className="section-title">
          <div>
            <h2>Incident workflows</h2>
            <p className="muted">Anomaly, incident, investigation and remediation recommendation lifecycle.</p>
          </div>
          <span className="status">{workflows.length} recent</span>
        </div>
        {loading ? <p className="muted">Loading workflows…</p> : workflows.length === 0 ? (
          <p className="muted">No automated workflows have run yet.</p>
        ) : (
          <div className="operations-table-wrap">
            <table className="operations-table">
              <thead><tr><th>Status</th><th>Stage</th><th>Incident</th><th>Attempts</th><th>Updated</th><th>Details</th></tr></thead>
              <tbody>
                {pagedWorkflows.map((workflow) => (
                  <tr key={workflow.id}>
                    <td><span className={`status ${workflow.status === "Completed" ? "healthy" : "warning"}`}>{workflow.status}</span></td>
                    <td>{workflow.currentStage || "—"}</td>
                    <td>{workflow.incidentId || "—"}</td>
                    <td>{workflow.attemptCount}</td>
                    <td>{dateLabel(workflow.updatedAt)}</td>
                    <td>
                      {workflow.error && <span className="operations-error-detail">{workflow.error}</span>}
                      <Link className="details-link" to={`/operations/workflows/${workflow.id}`}>Execution summary</Link>
                      {(workflow.status === "Failed" ||
                        (workflow.status === "Running" &&
                          Date.now() - new Date(workflow.updatedAt).getTime() > 5 * 60 * 1000)) && (
                        <button className="button secondary" disabled={workingId === workflow.id}
                          onClick={() => act(workflow.id, () => retryWorkflow(workflow.id), "Workflow retry completed.")}>
                          Retry
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination page={workflowPage} totalPages={workflowPages} total={workflows.length} label="workflows" onPageChange={setWorkflowPage} />
          </div>
        )}
      </section>

      <section className="panel operations-section">
        <div className="section-title">
          <div>
            <h2>Remediation actions</h2>
            <p className="muted">Approval remains policy-gated; high-risk actions require escalation.</p>
          </div>
          <span className="status">{remediations.length} actions</span>
        </div>
        {loading ? <p className="muted">Loading remediations…</p> : remediations.length === 0 ? (
          <p className="muted">No remediation actions are pending.</p>
        ) : (
          <div className="operations-table-wrap">
            <table className="operations-table">
              <thead><tr><th>Action</th><th>Target</th><th>Status</th><th>Approval</th><th>Reason</th><th>Controls</th></tr></thead>
              <tbody>
                {pagedRemediations.map((item) => {
                  const status = statusName(item.status);
                  const pending = status === "AwaitingApproval" || status === "Proposed";
                  const completed = status === "Succeeded" || status === "Failed";
                  return (
                    <tr key={item.id}>
                      <td>{actionName(item.actionType)}</td>
                      <td>{item.target}</td>
                      <td><span className="status">{status}</span></td>
                      <td>{String(item.approvalStatus)}</td>
                      <td>{item.reason || item.failureReason || "—"}</td>
                      <td>
                        <div className="button-row">
                          {pending && item.approvalStatus !== "Approved" && item.approvalStatus !== 2 && (
                            <button className="button secondary" disabled={workingId === item.id}
                              onClick={() => act(item.id, () => approveRemediation(item.id), "Remediation approval recorded.")}>
                              Approve
                            </button>
                          )}
                          {pending && (
                            <button className="button danger" disabled={workingId === item.id} onClick={() => reject(item.id)}>
                              Reject
                            </button>
                          )}
                          {(status === "Proposed" || status === "Approved") && (
                            <button className="button" disabled={workingId === item.id}
                              onClick={() => act(item.id, () => executeRemediation(item.id), "Remediation execution finished.")}>
                              Execute
                            </button>
                          )}
                          {completed && item.verificationSuccessful == null && (
                            <button className="button secondary" disabled={workingId === item.id}
                              onClick={() => act(item.id, () => verifyRemediation(item.id), "Verification finished.")}>
                              Verify
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            <Pagination page={remediationPage} totalPages={remediationPages} total={remediations.length} label="actions" onPageChange={setRemediationPage} />
          </div>
        )}
      </section>
    </main>
  );
}
