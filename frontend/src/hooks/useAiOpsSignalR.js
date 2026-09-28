import { useEffect } from "react";
import {
  getAiOpsConnection,
  startAiOpsConnection,
} from "../services/aiopsSignalR";

const AIOPS_EVENTS = [
  "AgentStarted",
  "AgentCompleted",
  "AgentFailed",

  "IncidentCreated",
  "IncidentUpdated",
  "IncidentResolved",

  "RcaStarted",
  "RcaCompleted",
  "RcaFailed",
];

export default function useAiOpsSignalR(
  handlers = {}
) {
  useEffect(() => {
    const connection =
      getAiOpsConnection();

    const registrations = [];

    for (const eventName of AIOPS_EVENTS) {
      /*
       * Register a handler for every known
       * backend AIOps event.
       *
       * This prevents SignalR from warning when
       * the current page is not interested in
       * a particular event.
       */
      const handler = (payload) => {
        const pageHandler =
          handlers[eventName];

        if (
          typeof pageHandler === "function"
        ) {
          pageHandler(payload);
        }
      };

      connection.on(
        eventName,
        handler
      );

      registrations.push({
        eventName,
        handler,
      });
    }

    startAiOpsConnection()
      .catch((error) => {
        console.error(
          "Unable to start AIOps SignalR:",
          error
        );
      });

    return () => {
      for (const {
        eventName,
        handler,
      } of registrations) {
        connection.off(
          eventName,
          handler
        );
      }
    };
  }, [handlers]);
}