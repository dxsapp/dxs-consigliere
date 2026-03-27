import { useEffect } from "react";
import { createBrowserRouter, useParams } from "react-router-dom";
import { AuthGuard } from "./AuthGuard";
import { AppShell } from "./AppShell";
import { LoginPage } from "@/pages/LoginPage";
import { DashboardPage } from "@/pages/DashboardPage";
import { AddressesPage } from "@/pages/AddressesPage";
import { AddressDetailPage } from "@/pages/AddressDetailPage";
import { TokensPage } from "@/pages/TokensPage";
import { TokenDetailPage } from "@/pages/TokenDetailPage";
import { RuntimePage } from "@/pages/RuntimePage";
import { StoragePage } from "@/pages/StoragePage";
import { FindingsPage } from "@/pages/FindingsPage";
import { addressListStore } from "@/stores/address-list.store";
import { addressDetailStore } from "@/stores/address-detail.store";
import { tokenListStore } from "@/stores/token-list.store";
import { tokenDetailStore } from "@/stores/token-detail.store";
import { dashboardStore } from "@/stores/dashboard.store";
import { findingsStore } from "@/stores/findings.store";
import { opsStore } from "@/stores/ops.store";

// ─── Route hydration wrappers ─────────────────────────────────────────────────
// Shell-level orchestration only. Pages read store state, they do not call APIs.

function DashboardRoute() {
  useEffect(() => {
    void dashboardStore.ensureLoaded();
  }, []);
  return <DashboardPage />;
}

function AddressesRoute() {
  useEffect(() => {
    void addressListStore.ensureLoaded();
  }, []);
  return <AddressesPage />;
}

function AddressDetailRoute() {
  const { address } = useParams<{ address: string }>();
  useEffect(() => {
    if (address) void addressDetailStore.ensureLoaded(decodeURIComponent(address));
  }, [address]);
  return <AddressDetailPage />;
}

function TokensRoute() {
  useEffect(() => {
    void tokenListStore.ensureLoaded();
  }, []);
  return <TokensPage />;
}

function TokenDetailRoute() {
  const { tokenId } = useParams<{ tokenId: string }>();
  useEffect(() => {
    if (tokenId) void tokenDetailStore.ensureLoaded(decodeURIComponent(tokenId));
  }, [tokenId]);
  return <TokenDetailPage />;
}

function FindingsRoute() {
  useEffect(() => {
    void findingsStore.ensureLoaded();
  }, []);
  return <FindingsPage />;
}

function RuntimeRoute() {
  useEffect(() => {
    void opsStore.ensureLoaded();
  }, []);
  return <RuntimePage />;
}

function StorageRoute() {
  useEffect(() => {
    void opsStore.ensureLoaded();
  }, []);
  return <StoragePage />;
}

// ─── Router ───────────────────────────────────────────────────────────────────

export const router = createBrowserRouter([
  {
    path: "/login",
    element: <LoginPage />,
  },
  {
    path: "/",
    element: (
      <AuthGuard>
        <AppShell />
      </AuthGuard>
    ),
    children: [
      { index: true, element: <DashboardRoute /> },
      { path: "addresses", element: <AddressesRoute /> },
      { path: "addresses/:address", element: <AddressDetailRoute /> },
      { path: "tokens", element: <TokensRoute /> },
      { path: "tokens/:tokenId", element: <TokenDetailRoute /> },
      { path: "runtime", element: <RuntimeRoute /> },
      { path: "storage", element: <StorageRoute /> },
      { path: "findings", element: <FindingsRoute /> },
    ],
  },
]);
