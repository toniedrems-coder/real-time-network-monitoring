import { useEffect, useMemo, useState } from "react";

const PAGE_SIZE = 10;

export default function AnomalyList({ anomalies = [] }) {
  const [currentPage, setCurrentPage] = useState(1);

  const totalPages = Math.max(
    1,
    Math.ceil(anomalies.length / PAGE_SIZE)
  );

  // If the anomaly collection changes and the current page
  // no longer exists, move back to the last available page.
  useEffect(() => {
    if (currentPage > totalPages) {
      setCurrentPage(totalPages);
    }
  }, [currentPage, totalPages]);

  const paginatedAnomalies = useMemo(() => {
    const startIndex = (currentPage - 1) * PAGE_SIZE;

    return anomalies.slice(
      startIndex,
      startIndex + PAGE_SIZE
    );
  }, [anomalies, currentPage]);

  function goToPage(page) {
    if (page < 1 || page > totalPages) {
      return;
    }

    setCurrentPage(page);
  }

  return (
    <article className="panel">
      <div className="card-row">
        <h2>Recent anomalies</h2>

        <span className="muted">
          {anomalies.length} detected
        </span>
      </div>

      {anomalies.length === 0 ? (
        <p className="muted">
          No anomalies detected.
        </p>
      ) : (
        <>
          <div className="list">
            {paginatedAnomalies.map((anomaly) => (
              <div
                className="anomaly-card"
                key={anomaly.id}
              >
                <div className="card-row">
                  <strong>
                    {anomaly.type}
                  </strong>

                  <span
                    className={`status ${anomaly.severity?.toLowerCase()}`}
                  >
                    {anomaly.severity}
                  </span>
                </div>

                <p>
                  {anomaly.description}
                </p>

                <div className="card-row">
                  <span className="muted">
                    {new Date(
                      anomaly.detectedAt
                    ).toLocaleString()}
                  </span>

                  <span className="status">
                    {anomaly.detectionMethod ===
                    "MachineLearning"
                      ? "ML"
                      : "RULE-BASED"}
                  </span>
                </div>
              </div>
            ))}
          </div>

          <div className="pagination">
            <button
              type="button"
              className="pagination-button"
              disabled={currentPage === 1}
              onClick={() =>
                goToPage(currentPage - 1)
              }
            >
              Previous
            </button>

            <span className="pagination-info">
              Page {currentPage} of {totalPages}
            </span>

            <button
              type="button"
              className="pagination-button"
              disabled={currentPage === totalPages}
              onClick={() =>
                goToPage(currentPage + 1)
              }
            >
              Next
            </button>
          </div>
        </>
      )}
    </article>
  );
}