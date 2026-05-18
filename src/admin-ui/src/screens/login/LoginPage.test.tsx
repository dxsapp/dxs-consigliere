import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { LoginPage } from "./LoginPage";
import { ApiClient } from "@/lib/api/client";
import { AuthStore } from "@/stores/root";

function renderLogin() {
  const auth = new AuthStore(new ApiClient());
  return {
    auth,
    ...render(
      <MemoryRouter initialEntries={["/login"]}>
        <LoginPage auth={auth} />
      </MemoryRouter>
    ),
  };
}

describe("LoginPage (S2 form)", () => {
  it("renders the brand header + sign-in CTA", () => {
    renderLogin();
    expect(screen.getByText(/consigliere admin/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /sign in/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/operator name/i)).toBeInTheDocument();
  });

  it("notes that real auth wires in S3", () => {
    renderLogin();
    expect(screen.getByText(/S2 placeholder/i)).toBeInTheDocument();
  });
});
