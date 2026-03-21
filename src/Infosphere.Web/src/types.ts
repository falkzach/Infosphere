export type Workspace = {
  id: string;
  brainProfileId: string;
  key: string;
  name: string;
  description: string;
  createdUtc: string;
  updatedUtc: string;
};

export type Task = {
  id: string;
  workspaceId: string;
  title: string;
  state: {
    id: number;
    key: string;
    name: string;
  };
  assignedAgentId: string | null;
  priority: number;
  contextEntryId: string | null;
  createdUtc: string;
  updatedUtc: string;
};

export type AgentSession = {
  id: string;
  workspaceId: string;
  agentId: string;
  agentKind: string;
  state: {
    id: number;
    key: string;
    name: string;
  };
  displayName: string;
  currentTaskId: string | null;
  startedUtc: string;
  heartbeatUtc: string;
  endedUtc: string | null;
};

export type WorkspaceMessage = {
  id: string;
  workspaceId: string;
  authorType: string;
  authorId: string | null;
  messageKind: string;
  content: string;
  createdUtc: string;
};
