import { useEffect, useState } from "react";
import { apiFetch } from "../../services/api";

export default function EndpointList() {
  const [endpoints, setEndpoints] = useState([]);
  const [error, setError] = useState("");

  async function load() {
    try {
      const response = await apiFetch("/endpoints");
      if (!response.ok) throw new Error();
      setEndpoints(await response.json());
    } catch {
      setError("Unable to load endpoints.");
    }
  }

  useEffect(() => { load(); }, []);

  async function remove(id) {
    const response = await apiFetch(`/endpoints/${id}`, { method: "DELETE" });
    if (response.ok) setEndpoints((current) => current.filter((endpoint) => endpoint.id !== id));
  }

  return (
    <article className="panel">
      <h2>Endpoint list</h2>
      {error && <p className="error">{error}</p>}
      {!error && endpoints.length === 0 && <p className="muted">No endpoints added yet.</p>}
      <div className="list">
        {endpoints.map((endpoint) => (
          <div className="list-item" key={endpoint.id}>
            <div><strong>{endpoint.name}</strong><span className="muted">{endpoint.url}</span></div>
            <button className="button danger" onClick={() => remove(endpoint.id)}>Remove</button>
          </div>
        ))}
      </div>
    </article>
  );
}