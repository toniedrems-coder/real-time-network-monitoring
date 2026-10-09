import { useCallback, useEffect, useState } from "react";
import { getContainerLogs, getContainers } from "../services/aiopsApi";
import ExecutionFlow from "../components/ExecutionFlow";
import Pagination from "../components/Pagination";

const PAGE_SIZE = 10;

export default function ContainersPage() {
  const [containers, setContainers] = useState([]);
  const [pageRequest, setPage] = useState(1);
  const totalPages = Math.max(1, Math.ceil(containers.length / PAGE_SIZE));
  const page = Math.min(pageRequest, totalPages);
  const pagedContainers = containers.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  const [selected, setSelected] = useState("");
  const [tail, setTail] = useState(100);
  const [logs, setLogs] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const refresh = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const data = await getContainers();
      setContainers(Array.isArray(data) ? data : []);
      setSelected((current) => data.some((container) => container.id === current) ? current : "");
    } catch (requestError) {
      setError(requestError.message || "Unable to connect to the configured Docker API.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { refresh(); }, [refresh]);

  async function loadLogs(id = selected) {
    if (!id) return;
    setBusy(true);
    setError("");
    try {
      const result = await getContainerLogs(id, tail);
      setLogs(result.content || "");
      setSelected(id);
    } catch (requestError) {
      setError(requestError.message || "Unable to load container logs.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="page operations-page">
      <div className="page-heading">
        <div><p className="eyebrow">Infrastructure monitoring</p><h1>Docker containers</h1><p className="muted">Read-only container inventory and bounded log-tail access through a configured Docker API endpoint.</p></div>
        <button className="button secondary" onClick={refresh} disabled={loading}>Refresh</button>
      </div>
      {error && <p className="operations-alert error" role="alert">{error}</p>}
      <ExecutionFlow
        title="Docker and log monitoring flow"
        steps={[
          { label: "Docker API", state: loading ? "active" : error && containers.length === 0 ? "failed" : "done", detail: loading ? "connecting" : error && containers.length === 0 ? "unreachable" : "connected" },
          { label: "Container inventory", state: containers.length ? "done" : "idle", detail: `${containers.length} containers` },
          { label: "Select container", state: selected ? "done" : "idle", detail: selected ? selected.slice(0, 12) : undefined },
          { label: "Fetch log tail", state: busy ? "active" : logs ? "done" : "idle", detail: logs ? `${tail} lines max` : undefined },
          { label: "Review output", state: logs ? "done" : "idle" },
        ]}
      />
      <section className="panel operations-section">
        <div className="section-title"><h2>Container inventory</h2><span className="status">{containers.length} containers</span></div>
        {loading ? <p className="muted">Loading container inventoryâ€¦</p> : containers.length === 0 ? (
          <p className="muted">No containers found. Ensure the authenticated Docker API proxy is configured.</p>
        ) : (
          <div className="operations-table-wrap">
            <table className="operations-table">
              <thead><tr><th>Name</th><th>Image</th><th>State</th><th>Status</th><th>Ports</th><th>Created</th><th>Logs</th></tr></thead>
              <tbody>{pagedContainers.map((container) => (
                <tr key={container.id}>
                  <td title={container.id}>{container.names || container.id.slice(0, 12)}</td><td>{container.image}</td><td>{container.state}</td><td>{container.status}</td><td>{container.ports?.join(", ") || "â€”"}</td><td>{container.createdAt ? new Date(container.createdAt).toLocaleString() : "â€”"}</td>
                  <td><button className="button secondary" onClick={() => loadLogs(container.id)} disabled={busy}>{busy && selected === container.id ? "Loadingâ€¦" : "View logs"}</button></td>
                </tr>
              ))}</tbody>
            </table>
            <Pagination page={page} totalPages={totalPages} total={containers.length} label="containers" onPageChange={setPage} />
          </div>
        )}
      </section>
      <section className="panel operations-section">
        <div className="section-title"><div><h2>Container logs</h2><p className="muted">{selected || "Select a container to view its recent output."}</p></div>
          <label className="inline-field">Tail lines<select value={tail} onChange={(event) => setTail(Number(event.target.value))}><option value="50">50</option><option value="100">100</option><option value="500">500</option><option value="1000">1000</option></select></label>
        </div>
        {selected && <button className="button secondary" onClick={() => loadLogs()} disabled={busy}>{busy ? "Loadingâ€¦" : "Refresh logs"}</button>}
        {logs && <pre className="container-logs">{logs}</pre>}
        {selected && !logs && <p className="muted">No log output loaded yet, or this container has no recent output.</p>}
      </section>
    </main>
  );
}
