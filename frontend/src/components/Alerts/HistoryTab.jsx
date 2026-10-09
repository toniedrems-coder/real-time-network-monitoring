import { useState } from "react";
import Pagination from "../Pagination";

const PAGE_SIZE = 10;

export default function HistoryTab({ events }) {
  const [pageRequest, setPage] = useState(1);
  const totalPages = Math.max(1, Math.ceil(events.length / PAGE_SIZE));
  const page = Math.min(pageRequest, totalPages);
  const paged = events.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  return (
    <section className="panel operations-section">
      <h2>Alert history</h2>
      {events.length === 0 ? <p className="muted">No alerts have fired yet.</p> : (
        <div className="operations-table-wrap">
          <table className="operations-table">
            <thead><tr><th>When</th><th>Type</th><th>Rule</th><th>Target</th><th>Severity</th><th>Message</th><th>Delivery</th></tr></thead>
            <tbody>{paged.map((e) => (
              <tr key={e.id}>
                <td>{new Date(e.createdAt).toLocaleString()}</td>
                <td><span className={`status ${e.kind === "Resolved" ? "healthy" : "critical"}`}>{e.kind}</span></td>
                <td>{e.ruleName}</td><td>{e.target}</td><td>{e.severity}</td><td>{e.message}</td>
                <td>{e.deliveries.length === 0 ? "No recipients" : e.deliveries.map((d, i) => (
                  <div key={i} title={d.error ?? ""}>{d.contact} · {d.channel}: <strong>{d.status}</strong></div>
                ))}</td>
              </tr>
            ))}</tbody>
          </table>
          <Pagination page={page} totalPages={totalPages} total={events.length} label="alerts" onPageChange={setPage} />
        </div>
      )}
    </section>
  );
}
