import { makeAutoObservable } from "mobx";

export type NotifySeverity = "success" | "error" | "warning" | "info";

export class NotifyStore {
  message = "";
  severity: NotifySeverity = "success";
  open = false;

  constructor() {
    makeAutoObservable(this);
  }

  success(message: string) {
    this.message = message;
    this.severity = "success";
    this.open = true;
  }

  error(message: string) {
    this.message = message;
    this.severity = "error";
    this.open = true;
  }

  warning(message: string) {
    this.message = message;
    this.severity = "warning";
    this.open = true;
  }

  close() {
    this.open = false;
  }
}

export const notifyStore = new NotifyStore();
