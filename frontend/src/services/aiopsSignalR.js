import * as signalR from "@microsoft/signalr";

const API_BASE =
  import.meta.env.VITE_API_BASE_URL ??
  "http://localhost:5011";

let connection = null;

export function getAiOpsConnection() {
  if (connection) {
    return connection;
  }

  connection =
    new signalR.HubConnectionBuilder()
      .withUrl(
        `${API_BASE}/hubs/aiops`,
        {
          accessTokenFactory: () =>
            localStorage.getItem(
              "accessToken"
            ) ?? "",
        }
      )
      .withAutomaticReconnect([
        0,
        2000,
        5000,
        10000,
      ])
      .configureLogging(
        signalR.LogLevel.Information
      )
      .build();

  return connection;
}

export async function startAiOpsConnection() {
  const hub = getAiOpsConnection();

  if (
    hub.state ===
    signalR.HubConnectionState.Disconnected
  ) {
    try {
      await hub.start();

      console.log(
        "AIOps SignalR connected."
      );
    } catch (error) {
      console.error(
        "AIOps SignalR connection failed:",
        error
      );

      throw error;
    }
  }

  return hub;
}