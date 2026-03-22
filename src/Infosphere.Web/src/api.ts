import type {
  AgentSession,
  Task,
  TaskArtifact,
  TaskChecklistItem,
  TaskExecution,
  TaskUpdate,
  Workspace,
  WorkspaceMessage
} from "./types";

declare global {
  interface Window {
    INFOSPHERE_CONFIG?: {
      apiBaseUrl?: string;
    };
  }
}

const apiBaseUrl = (window.INFOSPHERE_CONFIG?.apiBaseUrl ?? "").replace(/\/$/, "");

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {})
    },
    ...init
  });

  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`);
  }

  return response.json() as Promise<T>;
}

export function getApiBaseUrl(): string {
  return apiBaseUrl;
}

export function listWorkspaces(): Promise<Workspace[]> {
  return request<Workspace[]>("/api/v0/workspaces");
}

export function createWorkspace(payload: Pick<Workspace, "key" | "name" | "description">): Promise<Workspace> {
  return request<Workspace>("/api/v0/workspaces", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export function listTasks(workspaceId: string): Promise<Task[]> {
  return request<Task[]>(`/api/v0/tasks?workspaceId=${encodeURIComponent(workspaceId)}`);
}

export function createTask(payload: {
  workspaceId: string;
  title: string;
  priority: number;
  successCriteria?: string[];
}): Promise<Task> {
  return request<Task>("/api/v0/tasks", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export function getTaskExecution(taskId: string): Promise<TaskExecution> {
  return request<TaskExecution>(`/api/v0/tasks/${encodeURIComponent(taskId)}/execution`);
}

export function addTaskChecklistItem(payload: {
  taskId: string;
  title: string;
  isRequired: boolean;
  ordinal?: number;
  sessionId?: string;
}): Promise<TaskChecklistItem> {
  return request<TaskChecklistItem>(`/api/v0/tasks/${encodeURIComponent(payload.taskId)}/checklist`, {
    method: "POST",
    body: JSON.stringify({
      title: payload.title,
      isRequired: payload.isRequired,
      ordinal: payload.ordinal ?? null,
      sessionId: payload.sessionId ?? null
    })
  });
}

export function completeTaskChecklistItem(payload: {
  taskId: string;
  checklistItemId: string;
  isCompleted: boolean;
  sessionId?: string;
}): Promise<TaskChecklistItem> {
  return request<TaskChecklistItem>(
    `/api/v0/tasks/${encodeURIComponent(payload.taskId)}/checklist/${encodeURIComponent(payload.checklistItemId)}/completion`,
    {
      method: "POST",
      body: JSON.stringify({
        isCompleted: payload.isCompleted,
        sessionId: payload.sessionId ?? null
      })
    },
  );
}

export function createTaskUpdate(payload: {
  taskId: string;
  updateKind: string;
  summary: string;
  details?: Record<string, unknown>;
  sessionId?: string;
}): Promise<TaskUpdate> {
  return request<TaskUpdate>(`/api/v0/tasks/${encodeURIComponent(payload.taskId)}/updates`, {
    method: "POST",
    body: JSON.stringify({
      sessionId: payload.sessionId ?? null,
      updateKind: payload.updateKind,
      summary: payload.summary,
      details: payload.details ?? {}
    })
  });
}

export function createTaskArtifact(payload: {
  taskId: string;
  artifactKind: string;
  value: string;
  metadata?: Record<string, unknown>;
  sessionId?: string;
}): Promise<TaskArtifact> {
  return request<TaskArtifact>(`/api/v0/tasks/${encodeURIComponent(payload.taskId)}/artifacts`, {
    method: "POST",
    body: JSON.stringify({
      sessionId: payload.sessionId ?? null,
      artifactKind: payload.artifactKind,
      value: payload.value,
      metadata: payload.metadata ?? {}
    })
  });
}

export function listAgentSessions(workspaceId: string): Promise<AgentSession[]> {
  return request<AgentSession[]>(`/api/v0/agent-sessions?workspaceId=${encodeURIComponent(workspaceId)}`);
}

export function registerAgentSession(payload: {
  workspaceId: string;
  agentId: string;
  agentKind: string;
  displayName: string;
}): Promise<AgentSession> {
  return request<AgentSession>("/api/v0/agent-sessions", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export function listWorkspaceMessages(workspaceId: string): Promise<WorkspaceMessage[]> {
  return request<WorkspaceMessage[]>(`/api/v0/workspace-messages?workspaceId=${encodeURIComponent(workspaceId)}`);
}

export function createWorkspaceMessage(payload: {
  workspaceId: string;
  authorType: string;
  authorId: string | null;
  messageKind: string;
  content: string;
}): Promise<WorkspaceMessage> {
  return request<WorkspaceMessage>("/api/v0/workspace-messages", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}
