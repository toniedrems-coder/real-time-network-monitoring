import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getWorkflow, retryWorkflow } from "../services/aiopsApi";

function dateLabel(value) {
  return value ? new Date(value).toLocaleString() : "—";
}

export default function WorkflowExecutionPage() {
  const { workflowId } = useParams();
  const [workflow, setWorkflow] = useState(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function refresh() {
    try {
      setError("");
      setWorkflow(await getWorkflow(workflowId));
    } catch (requestError) {
      setError(requestError.message || "Unable to load the workflow execution.");
    }
  }

  useEffect(() => {
    refresh();
  }, [workflowId]);

  async function retry() {
    setBusy(true);
    setError("");
    try {
      setWorkflow(await retryWorkflow(workflowId));
    } catch (requestError) {
      setError(requestError.message || "Workflow retry failed.");
    } finally {
      setBusy(false);
    }
  }

  if (error && !workflow) {
    return <main className="page"><p className="error" role="alert">{error}</p><Link to="/operations">Back to operations</Link></main>;
  }
  if (!workflow) return <main className="page"><p className="muted">Loading execution summary…</p></main>;

  return (
    <main className="page operations-page">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Execution summary</p>
          <h1>Incident workflow</h1>
          <p className="muted">{workflow.id}</p>
        </div>
        <div className="button-row">
          <Link className="button secondary" to="/operations">Back to operations</Link>
          {workflow.status === "Failed" && <button className="button" disabled={busy} onClick={retry}>{busy ? "Retrying…" : "Retry workflow"}</button>}
        </div>
      </div>
      {error && <p className="operations-alert error" role="alert">{error}</p>}
      <section className="kpi-grid">
        {[
          ["Status", workflow.status],
          ["Attempt", workflow.attemptCount],
          ["Email", workflow.emailNotificationStatus],
          ["SMS", workflow.smsNotificationStatus],
          ["Webhook", workflow.webhookNotificationStatus],
        ].map(([label, value]) => <article className="kpi-card" key={label}><span>{label}</span><strong>{value || "—"}</strong></article>)}
      </section>
      <section className="panel operations-section">
        <h2>Execution details</h2>
        <dl className="execution-details">
          <div><dt>Anomaly</dt><dd>{workflow.anomalyId}</dd></div>
          <div><dt>Incident</dt><dd>{workflow.incidentId ? <Link to={`/incidents/${workflow.incidentId}/rca`}>{workflow.incidentId}</Link> : "—"}</dd></div>
          <div><dt>Remediation</dt><dd>{workflow.remediationId || "—"}</dd></div>
          <div><dt>Started</dt><dd>{dateLabel(workflow.startedAt)}</dd></div>
          <div><dt>Completed</dt><dd>{dateLabel(workflow.completedAt)}</dd></div>
          <div><dt>Last updated</dt><dd>{dateLabel(workflow.updatedAt)}</dd></div>
        </dl>
        {workflow.error && <p className="error">{workflow.error}</p>}
        {workflow.notificationError && <p className="error">{workflow.notificationError}</p>}
      </section>
      <section className="panel operations-section">
        <h2>Stage history</h2>
        {workflow.stageHistory?.length ? (
          <ol className="stage-timeline">
            {workflow.stageHistory.map((stage, index) => (
              <li className={`stage-event ${stage.status?.toLowerCase()}`} key={`${stage.name}-${index}`}>
                <div className="card-row"><strong>{stage.name}</strong><span className="status">{stage.status}</span></div>
                <p className="muted">{dateLabel(stage.startedAt)}{stage.completedAt ? ` – ${dateLabel(stage.completedAt)}` : " – in progress"}</p>
                {stage.details && <p>{stage.details}</p>}
              </li>
            ))}
          </ol>
        ) : <p className="muted">This execution has no recorded stage history.</p>}
      </section>
    </main>
  );
}
