import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from "react";

import { useNavigate } from "react-router-dom";

import useAiOpsSignalR from "../hooks/useAiOpsSignalR";

import {
  AlertTriangle,
  BrainCircuit,
  Clock3,
  Eye,
  RefreshCw,
  Search,
  ServerCrash,
  ShieldAlert,
} from "lucide-react";

import {
  getIncidents,
  runInvestigation,
} from "../services/aiopsApi";

import {
  getFailureType,
  getSeverity,
  getStatus,
} from "../utils/incidentEnums";

import "./IncidentsPage.css";

function IncidentsPage() {
  const navigate = useNavigate();

  // =========================================================
  // State
  // =========================================================

  const [incidents, setIncidents] =
    useState([]);

  const [loading, setLoading] =
    useState(true);

  const [error, setError] =
    useState("");

  const [search, setSearch] =
    useState("");

  const [
    statusFilter,
    setStatusFilter,
  ] = useState("all");

  const [
    severityFilter,
    setSeverityFilter,
  ] = useState("all");

  const [
    runningIncidentId,
    setRunningIncidentId,
  ] = useState(null);

  // =========================================================
  // Load incidents
  // =========================================================

  const loadIncidents =
    useCallback(async () => {
      try {
        setError("");

        const data =
          await getIncidents();

        setIncidents(
          Array.isArray(data)
            ? data
            : []
        );
      } catch (err) {
        console.error(
          "Unable to load incidents:",
          err
        );

        setError(
          err.message ||
            "Unable to load incidents."
        );
      } finally {
        setLoading(false);
      }
    }, []);

  // =========================================================
  // Initial load
  // =========================================================

  useEffect(() => {
    loadIncidents();
  }, [loadIncidents]);

  // =========================================================
  // Filtering
  // =========================================================

  const filteredIncidents =
    useMemo(() => {
      const normalizedSearch =
        search
          .trim()
          .toLowerCase();

      return incidents.filter(
        (incident) => {
          const matchesSearch =
            !normalizedSearch ||
            incident.incidentNumber
              ?.toLowerCase()
              .includes(
                normalizedSearch
              ) ||
            incident.target
              ?.toLowerCase()
              .includes(
                normalizedSearch
              ) ||
            incident.title
              ?.toLowerCase()
              .includes(
                normalizedSearch
              );

          const matchesStatus =
            statusFilter === "all" ||
            String(
              incident.status
            ) === statusFilter;

          const matchesSeverity =
            severityFilter === "all" ||
            String(
              incident.severity
            ) === severityFilter;

          return (
            matchesSearch &&
            matchesStatus &&
            matchesSeverity
          );
        }
      );
    }, [
      incidents,
      search,
      statusFilter,
      severityFilter,
    ]);

  // =========================================================
  // Dashboard statistics
  // =========================================================

  const statistics =
    useMemo(() => {
      return {
        total:
          incidents.length,

        open:
          incidents.filter(
            (incident) =>
              incident.status === 0
          ).length,

        investigating:
          incidents.filter(
            (incident) =>
              incident.status === 1
          ).length,

        critical:
          incidents.filter(
            (incident) =>
              incident.severity === 0
          ).length,

        escalated:
          incidents.filter(
            (incident) =>
              incident.status === 4
          ).length,
      };
    }, [incidents]);

  // =========================================================
  // SignalR
  // =========================================================

  const signalRHandlers =
    useMemo(
      () => ({
        IncidentCreated: () => {
          loadIncidents();
        },

        IncidentUpdated: () => {
          loadIncidents();
        },

        IncidentResolved: () => {
          loadIncidents();
        },
      }),
      [loadIncidents]
    );

  useAiOpsSignalR(
    signalRHandlers
  );

  // =========================================================
  // V2.0.4C
  // Full Investigation
  //
  // RCA Agent
  //     ↓
  // Knowledge Agent
  // =========================================================

  async function handleInvestigate(
    incident
  ) {
    try {
      setRunningIncidentId(
        incident.id
      );

      setError("");

      const result =
        await runInvestigation(
          incident.id
        );

      console.log(
        "Investigation completed:",
        result
      );

      // Reload because RCA may have
      // updated the incident.
      await loadIncidents();

      // Take operator to the
      // investigation/RCA detail page.
      navigate(
        `/incidents/${incident.id}/rca`,
        {
          state: {
            incident,
            investigationResult:
              result,
          },
        }
      );
    } catch (err) {
      console.error(
        "Investigation failed:",
        err
      );

      setError(
        err.message ||
          "Unable to execute investigation."
      );
    } finally {
      setRunningIncidentId(
        null
      );
    }
  }

  // =========================================================
  // View existing RCA
  // =========================================================

  function handleViewRca(
    incident
  ) {
    navigate(
      `/incidents/${incident.id}/rca`,
      {
        state: {
          incident,
        },
      }
    );
  }

  // =========================================================
  // Formatting
  // =========================================================

  function formatDate(value) {
    if (!value) {
      return "—";
    }

    const date =
      new Date(value);

    if (
      Number.isNaN(
        date.getTime()
      )
    ) {
      return "—";
    }

    return new Intl.DateTimeFormat(
      undefined,
      {
        dateStyle: "medium",
        timeStyle: "medium",
      }
    ).format(date);
  }

  function formatLatency(value) {
    if (
      value === null ||
      value === undefined
    ) {
      return "—";
    }

    return `${Number(
      value
    ).toFixed(2)} ms`;
  }

  // =========================================================
  // Render
  // =========================================================

  return (
    <div className="incidents-page">

      {/* =====================================================
          Header
      ====================================================== */}

      <div className="incidents-header">
        <div>
          <p className="page-eyebrow">
            Agentic AIOps
          </p>

          <h1>Incidents</h1>

          <p className="page-description">
            Monitor operational incidents,
            investigate failures, perform
            automated root cause analysis,
            and search previous operational
            knowledge.
          </p>
        </div>

        <button
          className="refresh-button"
          onClick={
            loadIncidents
          }
          disabled={loading}
        >
          <RefreshCw
            size={17}
            className={
              loading
                ? "spin"
                : ""
            }
          />

          Refresh
        </button>
      </div>

      {/* =====================================================
          Error
      ====================================================== */}

      {error && (
        <div className="incident-error">
          <AlertTriangle
            size={18}
          />

          <span>
            {error}
          </span>
        </div>
      )}

      {/* =====================================================
          Statistics
      ====================================================== */}

      <div className="incident-stat-grid">

        <StatCard
          title="Total Incidents"
          value={
            statistics.total
          }
          icon={
            <ServerCrash
              size={20}
            />
          }
        />

        <StatCard
          title="Open"
          value={
            statistics.open
          }
          icon={
            <Clock3
              size={20}
            />
          }
        />

        <StatCard
          title="Investigating"
          value={
            statistics.investigating
          }
          icon={
            <BrainCircuit
              size={20}
            />
          }
        />

        <StatCard
          title="P1 Critical"
          value={
            statistics.critical
          }
          icon={
            <ShieldAlert
              size={20}
            />
          }
        />

        <StatCard
          title="Escalated"
          value={
            statistics.escalated
          }
          icon={
            <AlertTriangle
              size={20}
            />
          }
        />

      </div>

      {/* =====================================================
          Incident Panel
      ====================================================== */}

      <div className="incident-panel">

        {/* ===================================================
            Toolbar
        ==================================================== */}

        <div className="incident-toolbar">

          <div className="incident-search">

            <Search
              size={17}
            />

            <input
              type="text"
              placeholder="Search incident, target or title..."
              value={search}
              onChange={(
                event
              ) =>
                setSearch(
                  event.target
                    .value
                )
              }
            />

          </div>

          {/* Status filter */}

          <select
            value={
              statusFilter
            }
            onChange={(
              event
            ) =>
              setStatusFilter(
                event.target
                  .value
              )
            }
          >

            <option value="all">
              All statuses
            </option>

            <option value="0">
              Open
            </option>

            <option value="1">
              Investigating
            </option>

            <option value="2">
              Remediating
            </option>

            <option value="3">
              Resolved
            </option>

            <option value="4">
              Escalated
            </option>

          </select>

          {/* Severity filter */}

          <select
            value={
              severityFilter
            }
            onChange={(
              event
            ) =>
              setSeverityFilter(
                event.target
                  .value
              )
            }
          >

            <option value="all">
              All severities
            </option>

            <option value="0">
              P1
            </option>

            <option value="1">
              P2
            </option>

            <option value="2">
              P3
            </option>

            <option value="3">
              P4
            </option>

          </select>

        </div>

        {/* ===================================================
            Loading
        ==================================================== */}

        {loading ? (

          <div className="incident-state">

            <RefreshCw
              size={24}
              className="spin"
            />

            <p>
              Loading incidents...
            </p>

          </div>

        ) : filteredIncidents.length ===
          0 ? (

          /* =================================================
             Empty
          ================================================== */

          <div className="incident-state">

            <ServerCrash
              size={32}
            />

            <h3>
              No incidents found
            </h3>

            <p>
              No incidents match
              the current filters.
            </p>

          </div>

        ) : (

          /* =================================================
             Table
          ================================================== */

          <div className="incident-table-wrapper">

            <table className="incident-table">

              <thead>
                <tr>
                  <th>
                    Incident
                  </th>

                  <th>
                    Target
                  </th>

                  <th>
                    Failure
                  </th>

                  <th>
                    Severity
                  </th>

                  <th>
                    Status
                  </th>

                  <th>
                    Latency
                  </th>

                  <th>
                    Detected
                  </th>

                  <th>
                    Agent
                  </th>

                  <th>
                    Actions
                  </th>
                </tr>
              </thead>

              <tbody>

                {filteredIncidents.map(
                  (incident) => {

                    const severity =
                      getSeverity(
                        incident.severity
                      );

                    const status =
                      getStatus(
                        incident.status
                      );

                    const investigating =
                      runningIncidentId ===
                      incident.id;

                    return (
                      <tr
                        key={
                          incident.id
                        }
                      >

                        {/* Incident */}

                        <td>
                          <div className="incident-primary">

                            <strong>
                              {
                                incident.incidentNumber
                              }
                            </strong>

                            <span>
                              {
                                incident.title
                              }
                            </span>

                          </div>
                        </td>

                        {/* Target */}

                        <td>
                          <span className="target-text">
                            {
                              incident.target
                            }
                          </span>
                        </td>

                        {/* Failure */}

                        <td>
                          <span className="failure-badge">
                            {getFailureType(
                              incident.failureType
                            )}
                          </span>
                        </td>

                        {/* Severity */}

                        <td>
                          <span
                            className={
                              `badge ${severity.className}`
                            }
                          >
                            {
                              severity.label
                            }
                          </span>
                        </td>

                        {/* Status */}

                        <td>
                          <span
                            className={
                              `badge ${status.className}`
                            }
                          >
                            {
                              status.label
                            }
                          </span>
                        </td>

                        {/* Latency */}

                        <td>
                          {formatLatency(
                            incident.latencyMs
                          )}
                        </td>

                        {/* Detected */}

                        <td>
                          {formatDate(
                            incident.detectedAt
                          )}
                        </td>

                        {/* Agent */}

                        <td>
                          {
                            incident.assignedAgent ??
                            "—"
                          }
                        </td>

                        {/* Actions */}

                        <td>
                          <div className="incident-actions">

                            {/* View RCA */}

                            <button
                              className="action-button secondary"
                              onClick={() =>
                                handleViewRca(
                                  incident
                                )
                              }
                            >
                              <Eye
                                size={15}
                              />

                              View RCA
                            </button>

                            {/* Full Investigation */}

                            <button
                              className="action-button primary"
                              onClick={() =>
                                handleInvestigate(
                                  incident
                                )
                              }
                              disabled={
                                investigating
                              }
                            >

                              {investigating ? (

                                <RefreshCw
                                  size={15}
                                  className="spin"
                                />

                              ) : (

                                <BrainCircuit
                                  size={15}
                                />

                              )}

                              {investigating
                                ? "Investigating..."
                                : "Investigate"}

                            </button>

                          </div>
                        </td>

                      </tr>
                    );
                  }
                )}

              </tbody>

            </table>

          </div>

        )}

      </div>

    </div>
  );
}

// ===========================================================
// Stat Card
// ===========================================================

function StatCard({
  title,
  value,
  icon,
}) {
  return (
    <div className="incident-stat-card">

      <div className="stat-card-icon">
        {icon}
      </div>

      <div>
        <span>
          {title}
        </span>

        <strong>
          {value}
        </strong>
      </div>

    </div>
  );
}

export default IncidentsPage;