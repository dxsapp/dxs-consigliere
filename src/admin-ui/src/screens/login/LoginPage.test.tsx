import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { LoginPage } from "./LoginPage";

describe("LoginPage (S0 scaffold smoke)", () => {
  it("renders the brand header", () => {
    render(<LoginPage />);
    expect(screen.getByRole("heading", { name: /consigliere admin/i })).toBeInTheDocument();
  });

  it("notes that the real form lands in S2", () => {
    render(<LoginPage />);
    expect(screen.getByText(/login form ships in s2/i)).toBeInTheDocument();
  });
});
