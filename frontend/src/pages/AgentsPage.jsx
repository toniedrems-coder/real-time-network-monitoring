import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from "react";

import {
  Activity,
  AlertTriangle,
  Bot,
  BrainCircuit,
  CheckCircle2,
  Clock3,
  Play,
  RefreshCw,
  ServerCog,
  ShieldAlert,
} from "lucide-react";

import {
  executeAgent,
  getAgents,
} from "../services/aiopsApi";

import {
  getAgentStatus,
} from "../utils/agentEnums";

import "./AgentsPage.css";

import useAiOpsSignalR from "../hooks/useAiOpsSignalR";

export default function AgentsPage() {
  const [agents, setAgents] = useState([]);
  const [loading, setLoading] =
    useState(true);

  const [error, setError] =
    useState("");

  const [executingId, setExecutingId] =
    useState(null);

  const [lastExecution, setLastExecution] =
    useState(null);

  const loadAgents = useCallback(async () => {
    try {
      setError("");

      const data = await getAgents();

      setAgents(
        Array.isArray(data)
          ? data
          : []
      );
    } catch (err) {
      console.error(
        "Unable to load agents:",
        err
      );

      setError(
        err.message ||
          "Unable to load agents."
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadAgents();
  }, [loadAgents]);

  const signalRHandlers = useMemo(
    () => ({
      AgentStarted: (event) => {
        console.log("AgentStarted:", event);

        setAgents((currentAgents) =>
          currentAgents.map((agent) =>
            agent.id === event.agentId
              ? {
                  ...agent,
                  status: event.status ?? "Executing",
                }
              : agent
          )
        );

        setExecutingId(event.agentId);
      },

      AgentCompleted: (event) => {
        console.log("AgentCompleted:", event);

        setExecutingId((current) =>
          current === event.agentId ? null : current
        );

        loadAgents();
      },

      AgentFailed: (event) => {
        console.error("AgentFailed:", event);

        setExecutingId((current) =>
          current === event.agentId ? null : current
        );

        setError(
          event.message ||
            `${event.agentName ?? "Agent"} execution failed.`
        );

        loadAgents();
      },
    }),
    [loadAgents]
  );

  useAiOpsSignalR(signalRHandlers);

  const stats = useMemo(() => {
    return {
      total: agents.length,

      running: agents.filter(
        (agent) =>
          agent.status === 2 ||
          agent.status === 4 ||
          agent.status === "Running" ||
          agent.status === "Executing"
      ).length,

      investigating: agents.filter(
        (agent) =>
          agent.status === 3 ||
          agent.status === "Investigating"
      ).length,

      failed: agents.filter(
        (agent) =>
          agent.status === 5 ||
          agent.status === "Failed"
      ).length,

      disabled: agents.filter(
        (agent) =>
          agent.status === 6 ||
          agent.status === "Disabled" ||
          agent.enabled === false
      ).length,
    };
  }, [agents]);

  async function handleExecute(agent) {
    try {
      setExecutingId(agent.id);
      setError("");

      let context = {};

      /*
       * Monitoring Agent can execute without
       * an incident context.
       *
       * RCA Agent requires an IncidentId, so
       * incident-specific RCA execution should
       * continue to happen from the Incidents
       * and RCA pages.
       */
      if (agent.id === "rca-agent") {
        setError(
          "The RCA Agent requires an incident. Run it from the Incidents or RCA page."
        );

        return;
      }

      const result =
        await executeAgent(
          agent.id,
          context
        );

      setLastExecution({
        agentId: agent.id,
        agentName: agent.name,
        ...result,
      });

      await loadAgents();
    } catch (err) {
      console.error(
        "Agent execution failed:",
        err
      );

      setError(
        err.message ||
          "Unable to execute agent."
      );
    } finally {
      setExecutingId(null);
    }
  }

  return (
    <div className="agents-page">
      <div className="agents-header">
        <div>
          <p className="page-eyebrow">
            Agentic AIOps
          </p>

          <h1>Agents</h1>

          <p className="page-description">
            Monitor and execute autonomous
            operational agents across the
            AIOps platform.
          </p>
        </div>

        <button
          className="agents-refresh-button"
          onClick={loadAgents}
          disabled={loading}
        >
          <RefreshCw
            size={17}
            className={
              loading ? "spin" : ""
            }
          />

          Refresh
        </button>
      </div>

      {error && (
        <div className="agents-error">
          <AlertTriangle size={18} />
          <span>{error}</span>
        </div>
      )}

      <div className="agent-stat-grid">
        <AgentStatCard
          title="Registered Agents"
          value={stats.total}
          icon={<Bot size={20} />}
        />

        <AgentStatCard
          title="Running"
          value={stats.running}
          icon={<Activity size={20} />}
        />

        <AgentStatCard
          title="Investigating"
          value={stats.investigating}
          icon={
            <BrainCircuit size={20} />
          }
        />

        <AgentStatCard
          title="Failed"
          value={stats.failed}
          icon={
            <ShieldAlert size={20} />
          }
        />

        <AgentStatCard
          title="Disabled"
          value={stats.disabled}
          icon={<ServerCog size={20} />}
        />
      </div>

      <div className="agents-section-heading">
        <div>
          <h2>Registered Agents</h2>

          <p>
            Operational agents currently
            registered with the platform.
          </p>
        </div>
      </div>

      {loading ? (
        <div className="agents-state">
          <RefreshCw
            size={28}
            className="spin"
          />

          <p>Loading agents...</p>
        </div>
      ) : agents.length === 0 ? (
        <div className="agents-state">
          <Bot size={36} />

          <h3>No agents registered</h3>

          <p>
            No AIOps agents are currently
            registered.
          </p>
        </div>
      ) : (
        <div className="agents-grid">
          {agents.map((agent) => (
            <AgentCard
              key={agent.id}
              agent={agent}
              executing={
                executingId === agent.id
              }
              onExecute={handleExecute}
            />
          ))}
        </div>
      )}

      {lastExecution && (
        <ExecutionResult
          result={lastExecution}
        />
      )}
    </div>
  );
}

function AgentCard({
  agent,
  executing,
  onExecute,
}) {
  const status =
    getAgentStatus(agent.status);

  // const isRcaAgent =
  //   agent.id === "rca-agent";

    const requiresIncident =
  agent.id === "rca-agent" ||
  agent.id === "knowledge-agent";

  const disabled =
    executing ||
    agent.enabled === false || requiresIncident;
  //  isRcaAgent || requiresIncident;
  return (
    <article className="agent-card">
      <div className="agent-card-header">
        <div className="agent-icon">
          {requiresIncident ? (
            <BrainCircuit size={24} />
          ) : (
            <Bot size={24} />
          )}
        </div>

        <span
          className={`agent-status ${status.className}`}
        >
          <span className="status-dot" />

          {status.label}
        </span>
      </div>

      <div className="agent-card-body">
        <h3>{agent.name}</h3>

        <span className="agent-id">
          {agent.id}
        </span>

        <p>
          {agent.description ||
            "No agent description available."}
        </p>
      </div>

      <div className="agent-meta">
        <div>
          <span>Enabled</span>

          <strong>
            {agent.enabled
              ? "Yes"
              : "No"}
          </strong>
        </div>

        <div>
          <span>Status</span>

          <strong>
            {status.label}
          </strong>
        </div>
      </div>

      {requiresIncident  && (
        <div className="agent-context-note">
          <BrainCircuit size={16} />

          <span>
            Requires an incident context.
            Execute from the Incidents or
            RCA page.
          </span>
        </div>
      )}

      <div className="agent-card-footer">
        <button
          className="execute-agent-button"
          disabled={disabled}
          onClick={() =>
            onExecute(agent)
          }
        >
          {executing ? (
            <>
              <RefreshCw
                size={16}
                className="spin"
              />

              Executing...
            </>
          ) : (
            <>
              <Play size={16} />
              Execute Agent
            </>
          )}
        </button>
      </div>
    </article>
  );
}

function AgentStatCard({
  title,
  value,
  icon,
}) {
  return (
    <div className="agent-stat-card">
      <div className="agent-stat-icon">
        {icon}
      </div>

      <div>
        <span>{title}</span>
        <strong>{value}</strong>
      </div>
    </div>
  );
}

function ExecutionResult({ result }) {
  const success = result.success;

  return (
    <section className="execution-panel">
      <div className="execution-heading">
        <div>
          {success ? (
            <CheckCircle2 size={21} />
          ) : (
            <AlertTriangle size={21} />
          )}

          <div>
            <h2>
              Latest Agent Execution
            </h2>

            <p>
              {result.agentName}
            </p>
          </div>
        </div>

        <span
          className={
            success
              ? "execution-success"
              : "execution-failed"
          }
        >
          {success
            ? "Successful"
            : "Failed"}
        </span>
      </div>

      <div className="execution-message">
        {result.message}
      </div>

      <div className="execution-times">
        <div>
          <Clock3 size={16} />

          <span>
            Started:{" "}
            {formatDate(
              result.startedAt
            )}
          </span>
        </div>

        <div>
          <Clock3 size={16} />

          <span>
            Completed:{" "}
            {formatDate(
              result.completedAt
            )}
          </span>
        </div>
      </div>

      {result.data && (
        <details className="execution-data">
          <summary>
            View execution data
          </summary>

          <pre>
            {JSON.stringify(
              result.data,
              null,
              2
            )}
          </pre>
        </details>
      )}
    </section>
  );
}

function formatDate(value) {
  if (!value) {
    return "—";
  }

  return new Intl.DateTimeFormat(
    undefined,
    {
      dateStyle: "medium",
      timeStyle: "medium",
    }
  ).format(new Date(value));
}
