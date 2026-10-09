import { useEffect, useState } from "react";
import EndpointForm from "../components/Endpoints/EndpointForm";
import EndpointList from "../components/Endpoints/EndpointList";
import LogDiscoveryPanel from "../components/Endpoints/LogDiscoveryPanel";
import ExecutionFlow from "../components/ExecutionFlow";
import { getEndpoints, getWorkflows } from "../services/aiopsApi";
import { workflowFlowSteps } from "../utils/flows";

export default function EndpointsPage() {
  const [refreshKey, setRefreshKey] = useState(0);
  const [workflows, setWorkflows] = useState([]);
  const [endpointCount, setEndpointCount] = useState(null);

  useEffect(() => {
    Promise.all([getWorkflows(), getEndpoints()])
      .then(([workflowData, endpointData]) => {
        setWorkflows(Array.isArray(workflowData) ? workflowData : []);
        setEndpointCount(Array.isArray(endpointData) ? endpointData.length : 0);
      })
      .catch(() => {});
  }, [refreshKey]);

  function handleEndpointAdded() {
    setRefreshKey((current) => current + 1);
  }

  return (
    <main className="page">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Inventory</p>
          <h1>Monitored endpoints</h1>
        </div>
      </div>

      <ExecutionFlow title="Endpoint monitoring flow" steps={workflowFlowSteps(workflows, endpointCount)} />

      <section className="content-grid endpoints-layout">
        <EndpointForm onEndpointAdded={handleEndpointAdded} />

        <EndpointList refreshKey={refreshKey} />
      </section>
      <LogDiscoveryPanel onEndpointsAdded={handleEndpointAdded} />
    </main>
  );
}
