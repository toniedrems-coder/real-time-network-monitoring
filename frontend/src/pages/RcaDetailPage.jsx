import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from "react";

import {
  AlertTriangle,
  ArrowLeft,
  BrainCircuit,
  CheckCircle2,
  FileText,
  Gauge,
  Network,
  RefreshCw,
  ScrollText,
  ServerCrash,
} from "lucide-react";

import {
  useNavigate,
  useParams,
} from "react-router-dom";

import {
  getIncident,
  getRca,
  runRca,
} from "../services/aiopsApi";

import {
  getFailureType,
  getSeverity,
  getStatus,
} from "../utils/incidentEnums";

import "./RcaDetailPage.css";
import useAiOpsSignalR from "../hooks/useAiOpsSignalR";

export default function RcaDetailPage() {
  const { incidentId } = useParams();
  const navigate = useNavigate();

  const [liveRcaStatus, setLiveRcaStatus] =
    useState(null);

  const [incident, setIncident] =
    useState(null);

  const [analysis, setAnalysis] =
    useState(null);

  const [loading, setLoading] =
    useState(true);

  const [running, setRunning] =
    useState(false);

  const [error, setError] =
    useState("");

  const [noAnalysis, setNoAnalysis] =
    useState(false);

  // -------------------------------------------------
  // Load incident + latest RCA
  // -------------------------------------------------

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError("");

      const incidentData =
        await getIncident(incidentId);

      setIncident(incidentData);

      try {
        const rcaData =
          await getRca(incidentId);

        setAnalysis(rcaData);
        setNoAnalysis(false);
      } catch (rcaError) {
        /*
         * A 404 from GET /api/rca/{incidentId}
         * means that the incident exists but no
         * complete RCA result is currently stored.
         */
        if (
          rcaError.status === 404 ||
          rcaError.message?.includes(
            "No RCA analysis"
          ) ||
          rcaError.message?.includes("404")
        ) {
          setAnalysis(null);
          setNoAnalysis(true);
        } else {
          throw rcaError;
        }
      }
    } catch (err) {
      console.error(
        "Unable to load RCA:",
        err
      );

      setError(
        err.message ||
          "Unable to load RCA."
      );
    } finally {
      setLoading(false);
    }
  }, [incidentId]);

  // -------------------------------------------------
  // Initial page load
  // -------------------------------------------------

  useEffect(() => {
    load();
  }, [load]);

  // -------------------------------------------------
  // Group RCA evidence
  // -------------------------------------------------

  const evidenceGroups = useMemo(() => {
    const groups = {
      endpoint: [],
      metrics: [],
      logs: [],
      incident: [],
      other: [],
    };

    if (!analysis?.evidence) {
      return groups;
    }

    for (const item of analysis.evidence) {
      const source =
        item.source?.toLowerCase() ?? "";

      if (source.includes("endpoint")) {
        groups.endpoint.push(item);
      } else if (
        source.includes("metric")
      ) {
        groups.metrics.push(item);
      } else if (
        source.includes("log")
      ) {
        groups.logs.push(item);
      } else if (
        source.includes("incident")
      ) {
        groups.incident.push(item);
      } else {
        groups.other.push(item);
      }
    }

    return groups;
  }, [analysis]);

  // -------------------------------------------------
  // SignalR live RCA events
  //
  // IMPORTANT:
  // This hook MUST remain above every conditional
  // return in this component.
  // -------------------------------------------------

  const signalRHandlers = useMemo(
    () => ({
      RcaStarted: (event) => {
        if (
          String(event.incidentId)
            .toLowerCase() !==
          String(incidentId)
            .toLowerCase()
        ) {
          return;
        }

        console.log(
          "RcaStarted:",
          event
        );

        setLiveRcaStatus(
          "Investigating"
        );

        setRunning(true);
        setError("");
      },

      RcaCompleted: async (event) => {
        if (
          String(event.incidentId)
            .toLowerCase() !==
          String(incidentId)
            .toLowerCase()
        ) {
          return;
        }

        console.log(
          "RcaCompleted:",
          event
        );

        setLiveRcaStatus(
          "Completed"
        );

        setRunning(false);

        // Reload the complete RCA from the
        // retrieval API after SignalR tells us
        // that processing has completed.
        await load();
      },

      RcaFailed: (event) => {
        if (
          String(event.incidentId)
            .toLowerCase() !==
          String(incidentId)
            .toLowerCase()
        ) {
          return;
        }

        console.error(
          "RcaFailed:",
          event
        );

        setLiveRcaStatus(
          "Failed"
        );

        setRunning(false);

        setError(
          event.message ||
            "RCA execution failed."
        );
      },
    }),
    [incidentId, load]
  );

  useAiOpsSignalR(
    signalRHandlers
  );

  // -------------------------------------------------
  // Run / Re-run RCA
  // -------------------------------------------------

  async function handleRunRca() {
    try {
      setRunning(true);
      setError("");

      setLiveRcaStatus(
        "Investigating"
      );

      await runRca(incidentId);

      /*
       * SignalR RcaCompleted should normally
       * trigger load().
       *
       * Keep this HTTP reload as a fallback in
       * case the browser temporarily misses the
       * SignalR event.
       */
      await load();
    } catch (err) {
      console.error(
        "Unable to run RCA:",
        err
      );

      setRunning(false);

      setLiveRcaStatus(
        "Failed"
      );

      setError(
        err.message ||
          "Unable to run RCA."
      );
    } finally {
      setRunning(false);
    }
  }

  // -------------------------------------------------
  // ALL HOOKS HAVE NOW EXECUTED.
  // Conditional returns are safe below this point.
  // -------------------------------------------------

  if (loading) {
    return (
      <div className="rca-state">
        <RefreshCw
          className="spin"
          size={28}
        />

        <p>
          Loading RCA investigation...
        </p>
      </div>
    );
  }

  if (error && !incident) {
    return (
      <div className="rca-state">
        <AlertTriangle size={30} />

        <h2>Unable to load RCA</h2>

        <p>{error}</p>
      </div>
    );
  }

  if (!incident) {
    return null;
  }

  const severity =
    getSeverity(incident.severity);

  const status =
    getStatus(incident.status);

  // Your existing return (...) continues here

  return (
    <div className="rca-page">
      <button
        className="back-button"
        onClick={() =>
          navigate("/incidents")
        }
      >
        <ArrowLeft size={17} />
        Back to Incidents
      </button>

      <div className="rca-header">
        <div>
          <p className="page-eyebrow">
            Root Cause Analysis
          </p>

          <h1>
            {incident.incidentNumber}
          </h1>

          <p className="rca-target">
            {incident.target}
          </p>

          <div className="rca-badges">
            <span className="failure-badge">
              {getFailureType(
                incident.failureType
              )}
            </span>

            <span
              className={`badge ${severity.className}`}
            >
              {severity.label}
            </span>

            <span
              className={`badge ${status.className}`}
            >
              {status.label}
            </span>
          </div>
        </div>

        <button
          className="run-rca-button"
          onClick={handleRunRca}
          disabled={running}
        >
          {running ? (
            <RefreshCw
              size={17}
              className="spin"
            />
          ) : (
            <BrainCircuit size={17} />
          )}

          {running
            ? "Running RCA..."
            : analysis
              ? "Re-run RCA"
              : "Run RCA"}
        </button>
      </div>

      {error && (
        <div className="rca-error">
          <AlertTriangle size={18} />
          {error}
        </div>
      )}

      {noAnalysis ? (
        <div className="no-rca-card">
          <BrainCircuit size={40} />

          <h2>
            No RCA analysis yet
          </h2>

          <p>
            Run the RCA Agent to investigate
            this incident.
          </p>

          <button
            className="run-rca-button"
            onClick={handleRunRca}
            disabled={running}
          >
            <BrainCircuit size={17} />
            Run RCA
          </button>
        </div>
      ) : analysis ? (
        <>
          <div className="rca-summary-grid">
            <section className="rca-card root-cause-card">
              <div className="card-heading">
                <BrainCircuit size={20} />
                <h2>
                  Probable Root Cause
                </h2>
              </div>

              <p>
                {analysis.probableCause}
              </p>
            </section>

            <section className="rca-card">
              <div className="card-heading">
                <Gauge size={20} />
                <h2>Confidence</h2>
              </div>

              <div className="confidence-value">
                {Math.round(
                  analysis.confidence * 100
                )}
                %
              </div>

              <strong>
                {analysis.confidenceLevel}
              </strong>
            </section>

            <section className="rca-card">
              <div className="card-heading">
                <AlertTriangle size={20} />
                <h2>Escalation</h2>
              </div>

              <div
                className={
                  analysis.requiresEscalation
                    ? "escalation required"
                    : "escalation not-required"
                }
              >
                {analysis.requiresEscalation
                  ? "Required"
                  : "Not Required"}
              </div>
            </section>
          </div>

          <h2 className="section-title">
            Investigation Evidence
          </h2>

          <div className="evidence-grid">
            <EvidenceCard
              title="Endpoint Evidence"
              icon={<Network size={20} />}
              items={evidenceGroups.endpoint}
            />

            <EvidenceCard
              title="Metrics Evidence"
              icon={<Gauge size={20} />}
              items={evidenceGroups.metrics}
            />

            <EvidenceCard
              title="Log Evidence"
              icon={<ScrollText size={20} />}
              items={evidenceGroups.logs}
            />

            {evidenceGroups.incident.length >
              0 && (
              <EvidenceCard
                title="Incident Evidence"
                icon={
                  <ServerCrash size={20} />
                }
                items={
                  evidenceGroups.incident
                }
              />
            )}

            {evidenceGroups.other.length >
              0 && (
              <EvidenceCard
                title="Additional Evidence"
                icon={
                  <FileText size={20} />
                }
                items={
                  evidenceGroups.other
                }
              />
            )}
          </div>

          <section className="rca-card recommendations-card">
            <div className="card-heading">
              <CheckCircle2 size={20} />

              <h2>
                Recommended Actions
              </h2>
            </div>

            <div className="recommendations-list">
              {analysis.recommendedActions
                ?.map((action, index) => (
                  <div
                    className="recommendation"
                    key={`${action}-${index}`}
                  >
                    <CheckCircle2
                      size={17}
                    />

                    <span>{action}</span>
                  </div>
                ))}
            </div>
          </section>
        </>
      ) : null}
    </div>
  );
}


function EvidenceCard({
  title,
  icon,
  items = [],
}) {
  return (
    <section className="rca-card evidence-card">
      <div className="card-heading">
        {icon}
        <h2>{title}</h2>
      </div>

      {items.length === 0 ? (
        <div className="evidence-empty">
          No evidence collected.
        </div>
      ) : (
        <div className="evidence-list">
          {items.map((item, index) => (
            <div
              className="evidence-item"
              key={`${item.source}-${item.type}-${index}`}
            >
              <div className="evidence-item-header">
                <strong>
                  {item.type || "Evidence"}
                </strong>

                {item.source && (
                  <span className="evidence-source">
                    {item.source}
                  </span>
                )}
              </div>

              {item.description && (
                <p className="evidence-description">
                  {item.description}
                </p>
              )}

              <div className="evidence-value">
                {formatEvidenceValue(
                  item.value
                )}
              </div>

              {item.collectedAt && (
                <span className="evidence-time">
                  {formatEvidenceDate(
                    item.collectedAt
                  )}
                </span>
              )}
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

function formatEvidenceValue(value) {
  if (
    value === null ||
    value === undefined ||
    value === ""
  ) {
    return "—";
  }

  if (typeof value === "object") {
    try {
      return JSON.stringify(
        value,
        null,
        2
      );
    } catch {
      return String(value);
    }
  }

  return String(value);
}

function formatEvidenceDate(value) {
  if (!value) {
    return "";
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "";
  }

  return new Intl.DateTimeFormat(
    undefined,
    {
      dateStyle: "medium",
      timeStyle: "medium",
    }
  ).format(date);
}
