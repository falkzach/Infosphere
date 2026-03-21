import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import {
  createTask,
  createWorkspace,
  createWorkspaceMessage,
  getApiBaseUrl,
  listAgentSessions,
  listTasks,
  listWorkspaceMessages,
  listWorkspaces,
  registerAgentSession
} from "./api";
import type { AgentSession, Task, Workspace, WorkspaceMessage } from "./types";

export function App() {
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [selectedWorkspaceId, setSelectedWorkspaceId] = useState("");
  const [tasks, setTasks] = useState<Task[]>([]);
  const [sessions, setSessions] = useState<AgentSession[]>([]);
  const [messages, setMessages] = useState<WorkspaceMessage[]>([]);
  const [status, setStatus] = useState("Loading...");

  const selectedWorkspace = useMemo(
    () => workspaces.find((workspace) => workspace.id === selectedWorkspaceId) ?? null,
    [selectedWorkspaceId, workspaces],
  );

  async function refresh() {
    try {
      setStatus("Refreshing workspace state...");
      const nextWorkspaces = await listWorkspaces();
      setWorkspaces(nextWorkspaces);

      const activeWorkspaceId =
        selectedWorkspaceId && nextWorkspaces.some((workspace) => workspace.id === selectedWorkspaceId)
          ? selectedWorkspaceId
          : nextWorkspaces[0]?.id ?? "";

      setSelectedWorkspaceId(activeWorkspaceId);

      if (!activeWorkspaceId) {
        setTasks([]);
        setSessions([]);
        setMessages([]);
        setStatus("Create a workspace to begin.");
        return;
      }

      const [nextTasks, nextSessions, nextMessages] = await Promise.all([
        listTasks(activeWorkspaceId),
        listAgentSessions(activeWorkspaceId),
        listWorkspaceMessages(activeWorkspaceId),
      ]);

      setTasks(nextTasks);
      setSessions(nextSessions);
      setMessages(nextMessages);
      setStatus(`Watching ${nextWorkspaces.length} workspace(s). Last refresh ${new Date().toLocaleTimeString()}.`);
    } catch (error) {
      setStatus(`Refresh failed: ${(error as Error).message}`);
    }
  }

  useEffect(() => {
    void refresh();
    const intervalId = window.setInterval(() => {
      void refresh();
    }, 5000);

    return () => window.clearInterval(intervalId);
  }, []);

  async function handleWorkspaceCreate(formData: FormData) {
    await createWorkspace({
      key: String(formData.get("key") ?? ""),
      name: String(formData.get("name") ?? ""),
      description: String(formData.get("description") ?? ""),
    });
    await refresh();
  }

  async function handleSessionCreate(formData: FormData) {
    if (!selectedWorkspaceId) return;

    await registerAgentSession({
      workspaceId: selectedWorkspaceId,
      agentId: String(formData.get("agentId") ?? ""),
      agentKind: String(formData.get("agentKind") ?? ""),
      displayName: String(formData.get("displayName") ?? ""),
    });
    await refresh();
  }

  async function handleTaskCreate(formData: FormData) {
    if (!selectedWorkspaceId) return;

    await createTask({
      workspaceId: selectedWorkspaceId,
      title: String(formData.get("title") ?? ""),
      priority: Number(formData.get("priority") ?? 0),
    });
    await refresh();
  }

  async function handleMessageCreate(formData: FormData) {
    if (!selectedWorkspaceId) return;

    await createWorkspaceMessage({
      workspaceId: selectedWorkspaceId,
      authorType: String(formData.get("authorType") ?? ""),
      authorId: (formData.get("authorId") as string) || null,
      messageKind: String(formData.get("messageKind") ?? ""),
      content: String(formData.get("content") ?? ""),
    });
    await refresh();
  }

  return (
    <main className="app-shell">
      <section className="hero">
        <div>
          <p className="eyebrow">Infosphere</p>
          <h1>Agent Work Console</h1>
          <p className="lede">Watch tasks, sessions, and workspace messages update from the Vite frontend.</p>
        </div>
        <div className="hero-meta">
          <span className="badge">React + Vite</span>
          <a href={`${getApiBaseUrl()}/openapi/v0.json`} target="_blank" rel="noreferrer">
            OpenAPI
          </a>
        </div>
      </section>

      <section className="toolbar card">
        <div className="toolbar-row">
          <label htmlFor="workspaceSelect">Workspace</label>
          <select
            id="workspaceSelect"
            value={selectedWorkspaceId}
            onChange={(event) => setSelectedWorkspaceId(event.target.value)}
          >
            {workspaces.map((workspace) => (
              <option key={workspace.id} value={workspace.id}>
                {workspace.name}
              </option>
            ))}
          </select>
          <button type="button" onClick={() => void refresh()}>
            Refresh
          </button>
        </div>
        <p className="status-text">{status}</p>
        {selectedWorkspace ? <p className="status-text">{selectedWorkspace.description || "No description yet."}</p> : null}
      </section>

      <section className="grid">
        <FormCard title="Create Workspace" onSubmit={handleWorkspaceCreate}>
          <input name="key" placeholder="workspace-key" required />
          <input name="name" placeholder="Workspace name" required />
          <textarea name="description" placeholder="What is this workspace for?" rows={3} />
          <button type="submit">Create Workspace</button>
        </FormCard>

        <FormCard title="Register Agent" onSubmit={handleSessionCreate}>
          <input name="agentId" placeholder="agent id" required />
          <input name="agentKind" placeholder="agent kind" defaultValue="coding-agent" required />
          <input name="displayName" placeholder="display name" required />
          <button type="submit" disabled={!selectedWorkspaceId}>
            Register Session
          </button>
        </FormCard>

        <FormCard title="Create Task" onSubmit={handleTaskCreate}>
          <input name="title" placeholder="Task title" required />
          <input name="priority" placeholder="Priority" type="number" defaultValue={0} required />
          <button type="submit" disabled={!selectedWorkspaceId}>
            Create Task
          </button>
        </FormCard>

        <FormCard title="Post Message" onSubmit={handleMessageCreate}>
          <input name="authorType" placeholder="author type" defaultValue="human" required />
          <input name="authorId" placeholder="author id" />
          <input name="messageKind" placeholder="message kind" defaultValue="note" required />
          <textarea name="content" placeholder="What should the workspace know?" rows={4} required />
          <button type="submit" disabled={!selectedWorkspaceId}>
            Post Message
          </button>
        </FormCard>
      </section>

      <section className="grid">
        <Panel title="Tasks" count={tasks.length}>
          {tasks.length === 0 ? <EmptyState text="No tasks yet." /> : tasks.map((task) => (
            <article className="item" key={task.id}>
              <div className="item-head">
                <strong>{task.title}</strong>
                <span className="pill">{task.state.name}</span>
              </div>
              <div className="item-meta">Priority {task.priority} · Assigned {task.assignedAgentId ?? "unassigned"}</div>
            </article>
          ))}
        </Panel>

        <Panel title="Agent Sessions" count={sessions.length}>
          {sessions.length === 0 ? <EmptyState text="No agent sessions yet." /> : sessions.map((session) => (
            <article className="item" key={session.id}>
              <div className="item-head">
                <strong>{session.displayName}</strong>
                <span className="pill">{session.state.name}</span>
              </div>
              <div className="item-meta">{session.agentKind} · {session.agentId}</div>
              <div className="item-subtle">Heartbeat {new Date(session.heartbeatUtc).toLocaleString()}</div>
            </article>
          ))}
        </Panel>

        <Panel title="Workspace Messages" count={messages.length} wide>
          {messages.length === 0 ? <EmptyState text="No workspace messages yet." /> : messages.map((message) => (
            <article className="item" key={message.id}>
              <div className="item-head">
                <strong>{message.messageKind}</strong>
                <span className="pill">{message.authorType}</span>
              </div>
              <div className="item-meta">{message.authorId ?? "anonymous"} · {new Date(message.createdUtc).toLocaleString()}</div>
              <div className="item-subtle">{message.content}</div>
            </article>
          ))}
        </Panel>
      </section>
    </main>
  );
}

function FormCard(props: {
  title: string;
  onSubmit: (formData: FormData) => Promise<void>;
  children: ReactNode;
}) {
  return (
    <section className="card">
      <h2>{props.title}</h2>
      <form
        className="stack"
        onSubmit={async (event) => {
          event.preventDefault();
          await props.onSubmit(new FormData(event.currentTarget));
          event.currentTarget.reset();
        }}
      >
        {props.children}
      </form>
    </section>
  );
}

function Panel(props: { title: string; count: number; wide?: boolean; children: ReactNode }) {
  return (
    <section className={`card${props.wide ? " wide" : ""}`}>
      <div className="panel-head">
        <h2>{props.title}</h2>
        <span className="count-chip">{props.count}</span>
      </div>
      <div className="list">{props.children}</div>
    </section>
  );
}

function EmptyState(props: { text: string }) {
  return <div className="item item-subtle">{props.text}</div>;
}
