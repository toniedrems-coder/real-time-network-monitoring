import EndpointForm from "../components/Endpoints/EndpointForm";
import EndpointList from "../components/Endpoints/EndpointList";

export default function EndpointsPage() {
  return (
    <main className="page">
      <div className="page-heading"><div><p className="eyebrow">Inventory</p><h1>Monitored endpoints</h1></div></div>
      <section className="content-grid endpoints-layout">
        <EndpointForm />
        <EndpointList />
      </section>
    </main>
  );
}