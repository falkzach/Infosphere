import { render, screen } from "@testing-library/react";
import { App } from "./App";

vi.mock("./api", () => ({
  getApiBaseUrl: () => "http://localhost:5080",
  listWorkspaces: async () => [],
  listTasks: async () => [],
  listAgentSessions: async () => [],
  listWorkspaceMessages: async () => [],
  createWorkspace: vi.fn(),
  createTask: vi.fn(),
  registerAgentSession: vi.fn(),
  createWorkspaceMessage: vi.fn(),
}));

describe("App", () => {
  it("renders the dashboard heading", async () => {
    render(<App />);
    expect(await screen.findByText("Agent Work Console")).toBeInTheDocument();
  });
});
