import { render, screen } from "@testing-library/react";
import { App } from "./App";

const workspaces = [
  {
    id: "workspace-1",
    brainProfileId: "brain-1",
    key: "workspace-1",
    name: "Workspace 1",
    description: "Primary workspace",
    createdUtc: "2026-03-21T00:00:00Z",
    updatedUtc: "2026-03-21T00:00:00Z",
  },
];

vi.mock("./api", () => ({
  getApiBaseUrl: () => "http://localhost:5080",
  listWorkspaces: async () => workspaces,
  listTasks: async () => ({
    items: [
      {
        id: "task-1",
        workspaceId: "workspace-1",
        title: "Expose task execution state",
        state: {
          id: 2,
          key: "in_progress",
          name: "In Progress",
        },
        assignedAgentId: "agent-active",
        priority: 5,
        contextEntryId: null,
        createdUtc: "2026-03-21T00:00:00Z",
        updatedUtc: "2026-03-21T00:05:00Z",
      },
    ],
    totalCount: 1,
    page: 1,
    limit: 25,
  }),
  getTaskExecution: async () => ({
    taskId: "task-1",
    checklistItems: [
      {
        id: "check-1",
        taskId: "task-1",
        ordinal: 1,
        title: "Render checklist in the dashboard",
        isRequired: true,
        isCompleted: false,
        completedByAgentSessionId: null,
        completedUtc: null,
        createdUtc: "2026-03-21T00:00:00Z",
        updatedUtc: "2026-03-21T00:00:00Z",
      },
    ],
    updates: [],
    artifacts: [],
  }),
  listAgentSessions: async () => [
    {
      id: "session-active",
      workspaceId: "workspace-1",
      agentId: "agent-active",
      agentKind: "coding-agent",
      state: {
        id: 1,
        key: "active",
        name: "Active",
      },
      displayName: "Active Agent",
      currentTaskId: null,
      startedUtc: "2026-03-21T00:00:00Z",
      heartbeatUtc: "2026-03-21T00:05:00Z",
      endedUtc: null,
    },
    {
      id: "session-closed",
      workspaceId: "workspace-1",
      agentId: "agent-closed",
      agentKind: "coding-agent",
      state: {
        id: 5,
        key: "closed",
        name: "Closed",
      },
      displayName: "Closed Agent",
      currentTaskId: null,
      startedUtc: "2026-03-21T00:00:00Z",
      heartbeatUtc: "2026-03-21T00:05:00Z",
      endedUtc: "2026-03-21T00:10:00Z",
    },
  ],
  listWorkspaceMessages: async () => [],
  createWorkspace: vi.fn(),
  createTask: vi.fn(),
  addTaskChecklistItem: vi.fn(),
  completeTaskChecklistItem: vi.fn(),
  createTaskUpdate: vi.fn(),
  createTaskArtifact: vi.fn(),
  registerAgentSession: vi.fn(),
  createWorkspaceMessage: vi.fn(),
}));

describe("App", () => {
  it("renders the dashboard heading", async () => {
    render(<App />);
    expect(await screen.findByText("Agent Work Console")).toBeInTheDocument();
  });

  it("shows only active agent sessions", async () => {
    render(<App />);

    expect(await screen.findByText("Active Agent")).toBeInTheDocument();
    expect(screen.queryByText("Closed Agent")).not.toBeInTheDocument();
    // checklist title appears in both the task list preview and the execution detail panel
    expect(screen.getAllByText(/Render checklist in the dashboard/i).length).toBeGreaterThan(0);
    expect(screen.getByText("No structured progress updates yet.")).toBeInTheDocument();
  });
});
