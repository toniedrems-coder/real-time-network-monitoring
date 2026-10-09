import { useState } from "react";
import { addDiscoveredIps, scanLogsForIps } from "../../services/aiopsApi";
import Pagination from "../Pagination";

const PAGE_SIZE = 10;

function readError(error) {
  try {
    return JSON.parse(error.message).message ?? error.message;
  } catch {
    return error.message || "Request failed.";
  }
}

export default function LogDiscoveryPanel({ onEndpointsAdded }) {
  const [tail, setTail] = useState(500);
  const [scheme, setScheme] = useState("http");
  const [result, setResult] = useState(null);
  const [selected, setSelected] = useState(new Set());
  const [pageRequest, setPage] = useState(1);
  const [scanning, setScanning] = useState(false);
  const [adding, setAdding] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const addresses = result?.addresses ?? [];
  const totalPages = Math.max(1, Math.ceil(addresses.length / PAGE_SIZE));
  const page = Math.min(pageRequest, totalPages);
  const paged = addresses.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  async function scan() {
    setScanning(true);
    setError("");
    setNotice("");
    try {
      const data = await scanLogsForIps(tail);
      setResult(data);
      setSelected(new Set());
      setPage(1);
    } catch (scanError) {
      setError(readError(scanError));
    } finally {
      setScanning(false);
    }
  }

  function toggle(ip) {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(ip)) next.delete(ip);
      else next.add(ip);
      return next;
    });
  }

  async function addSelected() {
    setAdding(true);
    setError("");
    try {
      const outcome = await addDiscoveredIps([...selected].map((ip) => ({ ip, scheme })));
      setNotice(`Added ${outcome.added.length} endpoint(s)${outcome.skipped.length ? `, skipped ${outcome.skipped.length}` : ""}.`);
      setSelected(new Set());
      await scan().catch(() => {});
      setNotice(`Added ${outcome.added.length} endpoint(s)${outcome.skipped.length ? `, skipped ${outcome.skipped.length}` : ""}.`);
      onEndpointsAdded?.();
    } catch (addError) {
      setError(readError(addError));
    } finally {
      setAdding(false);
    }
  }

  return (
    <section className="panel operations-section">
      <div className="section-title">
        <div>
          <h2>Discover endpoints from logs</h2>
          <p className="muted">Scans recent Docker container logs for IPv4 addresses. Loopback, link-local and reserved addresses are ignored; nothing is monitored until you select and add it.</p>
        </div>
        <div className="inline-actions">
          <label className="inline-field">Lines per container
            <select value={tail} onChange={(event) => setTail(Number(event.target.value))}>
              <option value="100">100</option><option value="500">500</option><option value="1000">1000</option>
            </select>
          </label>
          <label className="inline-field">Probe with
            <select value={scheme} onChange={(event) => setScheme(event.target.value)}>
              <option value="http">HTTP</option><option value="https">HTTPS</option>
            </select>
          </label>
          <button type="button" className="button" onClick={scan} disabled={scanning}>{scanning ? "Scanning…" : "Scan logs"}</button>
        </div>
      </div>
      {error && <p className="error">{error}</p>}
      {notice && <p className="success">{notice}</p>}
      {result && (
        <>
          <p className="muted">Scanned {result.containersScanned} running container(s){result.containersFailed ? `, ${result.containersFailed} failed` : ""}; found {addresses.length} address(es).</p>
          {addresses.length > 0 && (
            <div className="operations-table-wrap">
              <table className="operations-table">
                <thead><tr><th></th><th>IP address</th><th>Type</th><th>Seen</th><th>Containers</th><th>Status</th></tr></thead>
                <tbody>
                  {paged.map((item) => (
                    <tr key={item.ip}>
                      <td><input type="checkbox" aria-label={`Select ${item.ip}`} checked={selected.has(item.ip)} disabled={item.alreadyMonitored} onChange={() => toggle(item.ip)} /></td>
                      <td>{item.ip}</td>
                      <td>{item.isPrivate ? "Private" : "Public"}</td>
                      <td>{item.occurrences}</td>
                      <td>{item.containers.join(", ")}</td>
                      <td>{item.alreadyMonitored ? "Monitored" : "New"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <Pagination page={page} totalPages={totalPages} total={addresses.length} label="addresses" onPageChange={setPage} />
            </div>
          )}
          <button type="button" className="button" onClick={addSelected} disabled={adding || selected.size === 0}>
            {adding ? "Adding…" : `Add ${selected.size} selected to monitoring`}
          </button>
        </>
      )}
    </section>
  );
}
