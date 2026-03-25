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

export type PagedTasks = {
  items: Task[];
  totalCount: number;
  page: number;
  limit: number;
};

export type TaskChecklistItem = {
  id: string;
  taskId: string;
  ordinal: number;
  title: string;
  isRequired: boolean;
  isCompleted: boolean;
  completedByAgentSessionId: string | null;
  completedUtc: string | null;
  createdUtc: string;
  updatedUtc: string;
};

export type TaskUpdate = {
  id: number;
  taskId: string;
  agentSessionId: string | null;
  updateKind: string;
  summary: string;
  details: Record<string, unknown>;
  createdUtc: string;
};

export type TaskArtifact = {
  id: string;
  taskId: string;
  agentSessionId: string | null;
  artifactKind: string;
  value: string;
  metadata: Record<string, unknown>;
  createdUtc: string;
};

export type TaskExecution = {
  taskId: string;
  checklistItems: TaskChecklistItem[];
  updates: TaskUpdate[];
  artifacts: TaskArtifact[];
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
