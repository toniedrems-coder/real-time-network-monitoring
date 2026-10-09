// steps: [{ label, state: "done" | "active" | "failed" | "idle", detail }]
export default function ExecutionFlow({ title, steps }) {
  return (
    <section className="panel operations-section execution-flow">
      <h2>{title}</h2>
      <ol className="flow-steps">
        {steps.map((step, index) => (
          <li className={`flow-step ${step.state ?? "idle"}`} key={step.label}>
            <span className="flow-node">{index + 1}</span>
            <strong>{step.label}</strong>
            {step.detail && <small>{step.detail}</small>}
          </li>
        ))}
      </ol>
    </section>
  );
}
