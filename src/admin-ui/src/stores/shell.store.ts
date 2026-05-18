import { makeAutoObservable } from "mobx";

/**
 * S2-audit L4: header globals (alert count, connection status,
 * env tag) live on a dedicated MobX slice instead of being
 * hard-coded in AppShell JSX. The values surface as observable
 * fields so future feeders just call setters:
 *
 *  - S3 wires `connection` from the SignalR client (`online` /
 *    `stale` / `offline`).
 *  - S7 wires `alertCount` from the alerts store (poll-delta
 *    cursor against `/api/admin/p2p/alerts?since=`).
 *
 * For S2 the defaults stand in until those slices land.
 */
export type ConnectionStatus = "online" | "offline" | "stale";

export class ShellStore {
  alertCount = 0;
  connection: ConnectionStatus = "online";

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  setAlertCount(n: number) {
    this.alertCount = n;
  }

  setConnection(status: ConnectionStatus) {
    this.connection = status;
  }
}
