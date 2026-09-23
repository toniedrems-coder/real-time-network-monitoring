import { useState } from "react";
import { apiFetch } from "../../services/api";

export default function EndpointForm({ onEndpointAdded }) {
  const [form, setForm] = useState({
    name: "",
    url: "",
  });

  const [message, setMessage] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function submit(event) {
    event.preventDefault();

    setMessage("");

    let parsedUrl;

    try {
      parsedUrl = new URL(form.url);
    } catch {
      setMessage("Enter a valid HTTP or HTTPS URL.");
      return;
    }

    if (!["http:", "https:"].includes(parsedUrl.protocol)) {
      setMessage("URL must use HTTP or HTTPS.");
      return;
    }

    try {
      setSubmitting(true);

      const response = await apiFetch("/endpoints", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(form),
      });

      if (!response.ok) {
        setMessage(
          "Unable to add endpoint. Check the name and URL."
        );

        return;
      }

      setForm({
        name: "",
        url: "",
      });

      setMessage("Endpoint added successfully.");

      onEndpointAdded?.();
    } catch {
      setMessage(
        "Unable to communicate with the monitoring API."
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form
      className="panel form-panel"
      onSubmit={submit}
    >
      <h2>Add endpoint</h2>

      <label>
        Name

        <input
          required
          value={form.name}
          onChange={(event) =>
            setForm({
              ...form,
              name: event.target.value,
            })
          }
          placeholder="Production API"
        />
      </label>

      <label>
        URL

        <input
          required
          type="url"
          value={form.url}
          onChange={(event) =>
            setForm({
              ...form,
              url: event.target.value,
            })
          }
          placeholder="https://example.com"
        />
      </label>

      <button
        className="button"
        type="submit"
        disabled={submitting}
      >
        {submitting
          ? "Adding..."
          : "Add endpoint"}
      </button>

      {message && (
        <p className="muted">
          {message}
        </p>
      )}
    </form>
  );
}