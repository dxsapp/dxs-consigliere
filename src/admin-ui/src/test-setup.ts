/// <reference types="vitest/globals" />
import "@testing-library/jest-dom/vitest";
import { afterEach } from "vitest";

// S7-S12-audit M1 followup: MockAuthClient + PrefStore both persist
// to localStorage in mock mode so the e2e specs survive full page
// reloads. Clear between every vitest case so unit tests stay
// hermetic.
afterEach(() => {
  if (typeof window !== "undefined") {
    try {
      window.localStorage.clear();
    } catch {
      /* swallow */
    }
  }
});
