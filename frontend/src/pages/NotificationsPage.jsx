import { useCallback, useEffect, useState } from "react";
import ContactsTab from "../components/Alerts/ContactsTab";
import HistoryTab from "../components/Alerts/HistoryTab";
import RulesTab from "../components/Alerts/RulesTab";
import {
  getAlertContacts,
  getAlertEvents,
  getAlertRules,
  getAlertTransport,
  getContainers,
  getEndpoints,
  getNotificationStatus,
  sendNotificationTest,
} from "../services/aiopsApi";
import { readError } from "../utils/errors";

const TABS = ["Rules", "Contacts", "History", "Channels"];

export default function NotificationsPage() {
  const [tab, setTab] = useState("Rules");
  const [status, setStatus] = useState(null);
  const [transport, setTransport] = useState(null);
  const [rules, setRules] = useState([]);
  const [contacts, setContacts] = useState([]);
  const [events, setEvents] = useState([]);
  const [endpoints, setEndpoints] = useState([]);
  const [containers, setContainers] = useState([]);
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const refresh = useCallback(async () => {
    try {
      setError("");
      const [statusData, transportData, ruleData, contactData, eventData, endpointData] = await Promise.all([
        getNotificationStatus(), getAlertTransport(), getAlertRules(), getAlertContacts(), getAlertEvents(), getEndpoints(),
      ]);
      setStatus(statusData);
      setTransport(transportData);
      setRules(ruleData);
      setContacts(contactData);
      setEvents(eventData);
      setEndpoints(endpointData);
    } catch (requestError) {
      setError(readError(requestError, "Unable to load alerting data."));
    }
    // Container names are optional suggestions; Docker monitoring may not be configured.
    getContainers()
      .then((items) => setContainers(items.map((c) => (c.names || "").replace(/^\//, "")).filter(Boolean)))
      .catch(() => setContainers([]));
  }, []);

  useEffect(() => {
    refresh();
    const timer = setInterval(refresh, 30000);
    return () => clearInterval(timer);
  }, [refresh]);

  async function test(channel) {
    setBusy(channel);
    setError("");
    setNotice("");
    try {
      await sendNotificationTest(channel);
      setNotice(`Test ${channel.toUpperCase()} notification sent to the configured recipient.`);
    } catch (requestError) {
      setError(readError(requestError, `${channel.toUpperCase()} test failed.`));
    } finally {
      setBusy("");
    }
  }

  return (
    <main className="page">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Alerting</p>
          <h1>Notifications</h1>
          <p className="muted">Alert the right support people by email or SMS when an endpoint or container crosses a threshold.</p>
        </div>
        <button className="button secondary" onClick={refresh}>Refresh</button>
      </div>
      {error && <p className="operations-alert error" role="alert">{error}</p>}
      {notice && <p className="operations-alert success" role="status">{notice}</p>}

      <div className="tabs" role="tablist">
        {TABS.map((name) => (
          <button key={name} role="tab" aria-selected={tab === name} className={`tab ${tab === name ? "active" : ""}`} onClick={() => setTab(name)}>{name}</button>
        ))}
      </div>

      {tab === "Rules" && <RulesTab rules={rules} contacts={contacts} endpoints={endpoints} containers={containers} onChanged={refresh} />}
      {tab === "Contacts" && <ContactsTab contacts={contacts} transport={transport} onChanged={refresh} />}
      {tab === "History" && <HistoryTab events={events} />}
      {tab === "Channels" && (
        <>
          <section className="notification-grid">
            {["email", "sms", "webhook"].map((channel) => {
              const value = status?.[channel];
              const label = channel === "email" ? "Email (SMTP)" : channel === "sms" ? "SMS (Twilio)" : "Signed webhook";
              return (
                <article className="panel" key={channel}>
                  <div className="section-title"><h2>{label}</h2><span className={`status ${value?.status === "Configured" ? "healthy" : "warning"}`}>{value?.status || "Loading"}</span></div>
                  <p className="muted">{channel === "webhook" ? "Incident workflow completion callback" : `Incident workflow recipient: ${value?.destination || "Not configured"}`}</p>
                  {channel !== "webhook" && <button className="button" disabled={value?.status !== "Configured" || Boolean(busy)} onClick={() => test(channel)}>
                    {busy === channel ? "Sending…" : "Send test"}
                  </button>}
                </article>
              );
            })}
          </section>
          <section className="panel operations-section">
            <h2>Configuration</h2>
            <p className="muted">SMTP and Twilio credentials are set through backend environment variables or a secret manager. Alert rules send to the contacts you define; the recipients above are only used for incident workflow summaries and tests.</p>
          </section>
        </>
      )}
    </main>
  );
}
