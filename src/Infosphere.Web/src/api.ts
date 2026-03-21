import type { AgentSession, Task, Workspace, WorkspaceMessage } from "./types";

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

export function createTask(payload: { workspaceId: string; title: string; priority: number }): Promise<Task> {
  return request<Task>("/api/v0/tasks", {
    method: "POST",
    body: JSON.stringify(payload)
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
