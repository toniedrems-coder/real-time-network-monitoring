import { apiFetch } from "../../services/api";

export default function ReportExport() {
  async function download(format) {
    const response = await apiFetch(`/dashboard/reports?format=${format}`);
    if (!response.ok) throw new Error("Report download failed.");
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `network-monitor-report.${format}`;
    link.click();
    URL.revokeObjectURL(url);
  }

  return (
    <section className="panel report-actions">
      <div><h2>Quick reports</h2><p className="muted">Download the current dashboard summary.</p></div>
      <div className="button-row">
        <button className="button" type="button" onClick={() => download("csv")}>Export CSV</button>
        <button className="button secondary" type="button" onClick={() => download("pdf")}>Export PDF</button>
      </div>
    </section>
  );
}