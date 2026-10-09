import { useMemo, useState } from "react";
import { deleteAlertRule, saveAlertRule } from "../../services/aiopsApi";
import { readError } from "../../utils/errors";
import Pagination from "../Pagination";

const PAGE_SIZE = 10;
const ENDPOINT_METRICS = [
  { value: "Latency", label: "Latency above (ms)", unit: "ms" },
  { value: "ErrorRate", label: "Error rate above (%)", unit: "%" },
  { value: "Availability", label: "Availability below (%)", unit: "%" },
  { value: "Unreachable", label: "Endpoint unreachable" },
];
const EMPTY = {
  name: "", targetType: "Endpoint", targetId: "", metric: "Latency", threshold: 800,
  durationMinutes: 5, severity: "High", notifyEmail: true, notifySms: false,
  contactIds: [], cooldownMinutes: 60, enabled: true,
};

function describe(rule) {
  if (rule.metric === "ContainerNotRunning") return "Container not running";
  if (rule.metric === "Unreachable") return "Endpoint unreachable";
  const m = ENDPOINT_METRICS.find((x) => x.value === rule.metric);
  return `${m?.label.replace(/ \(.*\)/, "")} ${rule.threshold}${m?.unit ?? ""}`;
}

export default function RulesTab({ rules, contacts, endpoints, containers, onChanged }) {
  const [form, setForm] = useState(EMPTY);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [pageRequest, setPage] = useState(1);

  const endpointName = useMemo(() => Object.fromEntries(endpoints.map((e) => [e.id, e.name])), [endpoints]);
  const contactName = useMemo(() => Object.fromEntries(contacts.map((c) => [c.id, c.name])), [contacts]);
  const totalPages = Math.max(1, Math.ceil(rules.length / PAGE_SIZE));
  const page = Math.min(pageRequest, totalPages);
  const paged = rules.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  const needsThreshold = ["Latency", "ErrorRate", "Availability"].includes(form.metric);

  function setTargetType(targetType) {
    setForm({ ...form, targetType, targetId: "", metric: targetType === "Container" ? "ContainerNotRunning" : "Latency" });
  }

  function toggleContact(id) {
    setForm((current) => ({
      ...current,
      contactIds: current.contactIds.includes(id) ? current.contactIds.filter((x) => x !== id) : [...current.contactIds, id],
    }));
  }

  async function submit(event) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      await saveAlertRule({ ...form, threshold: Number(form.threshold) || 0, durationMinutes: Number(form.durationMinutes) || 0, cooldownMinutes: Number(form.cooldownMinutes) || 0 });
      setForm(EMPTY);
      onChanged();
    } catch (requestError) {
      setError(readError(requestError));
    } finally {
      setBusy(false);
    }
  }

  async function toggleEnabled(rule) {
    try {
      await saveAlertRule({ ...rule, enabled: !rule.enabled });
      onChanged();
    } catch (requestError) {
      setError(readError(requestError));
    }
  }

  async function remove(rule) {
    if (!window.confirm(`Delete rule "${rule.name}"?`)) return;
    try {
      await deleteAlertRule(rule.id);
      onChanged();
    } catch (requestError) {
      setError(readError(requestError));
    }
  }

  return (
    <div className="alert-tab">
      <section className="panel operations-section">
        <h2>{form.id ? "Edit alert rule" : "New alert rule"}</h2>
        <form className="alert-form" onSubmit={submit}>
          <label>Rule name<input value={form.name} maxLength={100} required placeholder="Payments API slow" onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
          <label>Target type
            <select value={form.targetType} onChange={(e) => setTargetType(e.target.value)}>
              <option value="Endpoint">Endpoint</option><option value="Container">Docker container (service / DB)</option>
            </select>
          </label>
          {form.targetType === "Endpoint" ? (
            <label>Endpoint
              <select value={form.targetId} required onChange={(e) => setForm({ ...form, targetId: e.target.value })}>
                <option value="">Select endpoint…</option>
                {endpoints.map((e) => <option key={e.id} value={e.id}>{e.name} — {e.url}</option>)}
              </select>
            </label>
          ) : (
            <label>Container name
              <input list="alert-containers" value={form.targetId} required maxLength={200} placeholder="postgres" onChange={(e) => setForm({ ...form, targetId: e.target.value })} />
              <datalist id="alert-containers">{containers.map((name) => <option key={name} value={name} />)}</datalist>
            </label>
          )}
          {form.targetType === "Endpoint" ? (
            <label>Condition
              <select value={form.metric} onChange={(e) => setForm({ ...form, metric: e.target.value })}>
                {ENDPOINT_METRICS.map((m) => <option key={m.value} value={m.value}>{m.label}</option>)}
              </select>
            </label>
          ) : <label>Condition<input value="Container not running" disabled /></label>}
          {needsThreshold && <label>Threshold<input type="number" min="0" step="any" value={form.threshold} onChange={(e) => setForm({ ...form, threshold: e.target.value })} /></label>}
          <label>For at least (minutes)<input type="number" min="0" max="1440" value={form.durationMinutes} onChange={(e) => setForm({ ...form, durationMinutes: e.target.value })} /></label>
          <label>Severity
            <select value={form.severity} onChange={(e) => setForm({ ...form, severity: e.target.value })}>
              {["Low", "Medium", "High", "Critical"].map((s) => <option key={s}>{s}</option>)}
            </select>
          </label>
          <label>Repeat reminder every (minutes, 0 = once)<input type="number" min="0" max="10080" value={form.cooldownMinutes} onChange={(e) => setForm({ ...form, cooldownMinutes: e.target.value })} /></label>
          <fieldset className="alert-channels">
            <legend>Notify via</legend>
            <label className="checkbox-field"><input type="checkbox" checked={form.notifyEmail} onChange={(e) => setForm({ ...form, notifyEmail: e.target.checked })} /> Email</label>
            <label className="checkbox-field"><input type="checkbox" checked={form.notifySms} onChange={(e) => setForm({ ...form, notifySms: e.target.checked })} /> SMS</label>
          </fieldset>
          <fieldset className="alert-channels">
            <legend>Who gets alerted</legend>
            {contacts.length === 0 && <span className="muted">Add support contacts first.</span>}
            {contacts.map((c) => (
              <label className="checkbox-field" key={c.id}><input type="checkbox" checked={form.contactIds.includes(c.id)} onChange={() => toggleContact(c.id)} /> {c.name}</label>
            ))}
          </fieldset>
          <label className="checkbox-field"><input type="checkbox" checked={form.enabled} onChange={(e) => setForm({ ...form, enabled: e.target.checked })} /> Enabled</label>
          <div className="inline-actions">
            <button className="button" disabled={busy}>{busy ? "Saving…" : form.id ? "Save changes" : "Create rule"}</button>
            {form.id && <button type="button" className="button secondary" onClick={() => setForm(EMPTY)}>Cancel</button>}
          </div>
        </form>
        {error && <p className="error" role="alert">{error}</p>}
      </section>

      <section className="panel operations-section">
        <h2>Alert rules</h2>
        {rules.length === 0 ? <p className="muted">No rules yet. Create one above to alert support when a service crosses a threshold.</p> : (
          <div className="operations-table-wrap">
            <table className="operations-table">
              <thead><tr><th>Rule</th><th>Target</th><th>Condition</th><th>Severity</th><th>Notify</th><th>State</th><th></th></tr></thead>
              <tbody>{paged.map((r) => (
                <tr key={r.id}>
                  <td>{r.name}</td>
                  <td>{r.targetType === "Endpoint" ? (endpointName[r.targetId] ?? "Deleted endpoint") : r.targetId}</td>
                  <td>{describe(r)}{r.durationMinutes > 0 ? ` for ${r.durationMinutes} min` : ""}</td>
                  <td>{r.severity}</td>
                  <td title={r.contactIds.map((id) => contactName[id] ?? "removed").join(", ")}>{[r.notifyEmail && "Email", r.notifySms && "SMS"].filter(Boolean).join(" + ")} · {r.contactIds.length} contact(s)</td>
                  <td><span className={`status ${!r.enabled ? "warning" : r.firing ? "critical" : "healthy"}`}>{!r.enabled ? "Disabled" : r.firing ? "Alerting" : r.breachStartedAt ? "Breaching" : "OK"}</span></td>
                  <td className="inline-actions">
                    <button className="button secondary" onClick={() => setForm({ ...EMPTY, ...r })}>Edit</button>
                    <button className="button secondary" onClick={() => toggleEnabled(r)}>{r.enabled ? "Disable" : "Enable"}</button>
                    <button className="button secondary" onClick={() => remove(r)}>Delete</button>
                  </td>
                </tr>
              ))}</tbody>
            </table>
            <Pagination page={page} totalPages={totalPages} total={rules.length} label="rules" onPageChange={setPage} />
          </div>
        )}
      </section>
    </div>
  );
}
