import { useEffect, useState } from "react";
import { apiFetch } from "../../services/api";

export default function EndpointList({ refreshKey }) {
  const [endpoints, setEndpoints] = useState([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [removingId, setRemovingId] = useState(null);

  async function load() {
    try {
      setLoading(true);
      setError("");

      const response = await apiFetch("/endpoints");

      if (!response.ok) {
        throw new Error("Failed to load endpoints.");
      }

      const data = await response.json();

      setEndpoints(data);
    } catch (error) {
      console.error("Unable to load endpoints:", error);

      setError("Unable to load endpoints.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, [refreshKey]);

  async function remove(id) {
    try {
      setRemovingId(id);
      setError("");

      const response = await apiFetch(
        `/endpoints/${id}`,
        {
          method: "DELETE",
        }
      );

      if (!response.ok) {
        throw new Error("Failed to remove endpoint.");
      }

      // Remove immediately from the UI after successful deletion.
      setEndpoints((current) =>
        current.filter(
          (endpoint) => endpoint.id !== id
        )
      );
    } catch (error) {
      console.error("Unable to remove endpoint:", error);

      setError("Unable to remove endpoint.");
    } finally {
      setRemovingId(null);
    }
  }

  return (
    <article className="panel">
      <h2>Endpoint list</h2>

      {error && (
        <p className="error">
          {error}
        </p>
      )}

      {loading && (
        <p className="muted">
          Loading endpoints...
        </p>
      )}

      {!loading &&
        !error &&
        endpoints.length === 0 && (
          <p className="muted">
            No endpoints added yet.
          </p>
        )}

      {!loading && endpoints.length > 0 && (
        <div className="list">
          {endpoints.map((endpoint) => (
            <div
              className="list-item"
              key={endpoint.id}
            >
              <div>
                <strong>
                  {endpoint.name}
                </strong>

                <span className="muted">
                  {endpoint.url}
                </span>
              </div>

              <button
                className="button danger"
                type="button"
                disabled={removingId === endpoint.id}
                onClick={() => remove(endpoint.id)}
              >
                {removingId === endpoint.id
                  ? "Removing..."
                  : "Remove"}
              </button>
            </div>
          ))}
        </div>
      )}
    </article>
  );
}