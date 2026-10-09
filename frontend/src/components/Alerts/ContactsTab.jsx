import { useState } from "react";
import { deleteAlertContact, saveAlertContact } from "../../services/aiopsApi";
import { readError } from "../../utils/errors";

const EMPTY = { name: "", email: "", phone: "", active: true };

export default function ContactsTab({ contacts, transport, onChanged }) {
  const [form, setForm] = useState(EMPTY);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function submit(event) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      await saveAlertContact(form);
      setForm(EMPTY);
      onChanged();
    } catch (requestError) {
      setError(readError(requestError));
    } finally {
      setBusy(false);
    }
  }

  async function remove(contact) {
    if (!window.confirm(`Delete ${contact.name}? Rules will stop alerting this person.`)) return;
    try {
      await deleteAlertContact(contact.id);
      onChanged();
    } catch (requestError) {
      setError(readError(requestError));
    }
  }

  function edit(contact) {
    setForm({ id: contact.id, name: contact.name, email: contact.email ?? "", phone: contact.phone ?? "", active: contact.active });
  }

  return (
    <div className="alert-tab">
      <section className="panel operations-section">
        <h2>{form.id ? "Edit support contact" : "Add support contact"}</h2>
        <form className="alert-form" onSubmit={submit}>
          <label>Name<input value={form.name} maxLength={100} required onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
          <label>Email<input type="email" value={form.email} maxLength={254} placeholder="oncall@example.com" onChange={(e) => setForm({ ...form, email: e.target.value })} /></label>
          <label>Phone (E.164)<input value={form.phone} maxLength={16} placeholder="+2348012345678" onChange={(e) => setForm({ ...form, phone: e.target.value })} /></label>
          <label className="checkbox-field"><input type="checkbox" checked={form.active} onChange={(e) => setForm({ ...form, active: e.target.checked })} /> Active</label>
          <div className="inline-actions">
            <button className="button" disabled={busy}>{busy ? "Saving…" : form.id ? "Save changes" : "Add contact"}</button>
            {form.id && <button type="button" className="button secondary" onClick={() => setForm(EMPTY)}>Cancel</button>}
          </div>
        </form>
        {error && <p className="error" role="alert">{error}</p>}
        {transport && (!transport.email || !transport.sms) && (
          <p className="muted">Server delivery not configured for: {[!transport.email && "email (SMTP)", !transport.sms && "SMS (Twilio)"].filter(Boolean).join(", ")}. Alerts on those channels will be recorded as not delivered.</p>
        )}
      </section>
      <section className="panel operations-section">
        <h2>Support contacts</h2>
        {contacts.length === 0 ? <p className="muted">No contacts yet.</p> : (
          <div className="operations-table-wrap">
            <table className="operations-table">
              <thead><tr><th>Name</th><th>Email</th><th>Phone</th><th>Status</th><th></th></tr></thead>
              <tbody>{contacts.map((c) => (
                <tr key={c.id}>
                  <td>{c.name}</td><td>{c.email || "—"}</td><td>{c.phone || "—"}</td>
                  <td><span className={`status ${c.active ? "healthy" : "warning"}`}>{c.active ? "Active" : "Paused"}</span></td>
                  <td className="inline-actions"><button className="button secondary" onClick={() => edit(c)}>Edit</button><button className="button secondary" onClick={() => remove(c)}>Delete</button></td>
                </tr>
              ))}</tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
