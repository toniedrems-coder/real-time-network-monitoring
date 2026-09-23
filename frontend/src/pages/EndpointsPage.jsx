import { useState } from "react";
import EndpointForm from "../components/Endpoints/EndpointForm";
import EndpointList from "../components/Endpoints/EndpointList";

export default function EndpointsPage() {
  const [refreshKey, setRefreshKey] = useState(0);

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

      <section className="content-grid endpoints-layout">
        <EndpointForm onEndpointAdded={handleEndpointAdded} />

        <EndpointList refreshKey={refreshKey} />
      </section>
    </main>
  );
}