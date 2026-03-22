import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import {
  addTaskChecklistItem,
  completeTaskChecklistItem,
  createTask,
  createTaskArtifact,
  createTaskUpdate,
  createWorkspace,
  createWorkspaceMessage,
  getApiBaseUrl,
  getTaskExecution,
  listAgentSessions,
  listTasks,
  listWorkspaceMessages,
  listWorkspaces,
  registerAgentSession
} from "./api";
import type { AgentSession, Task, TaskExecution, Workspace, WorkspaceMessage } from "./types";

export function App() {
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [selectedWorkspaceId, setSelectedWorkspaceId] = useState("");
  const [tasks, setTasks] = useState<Task[]>([]);
  const [selectedTaskId, setSelectedTaskId] = useState("");
  const [taskExecution, setTaskExecution] = useState<TaskExecution | null>(null);
  const [sessions, setSessions] = useState<AgentSession[]>([]);
  const [messages, setMessages] = useState<WorkspaceMessage[]>([]);
  const [status, setStatus] = useState("Loading...");

  const selectedWorkspace = useMemo(
    () => workspaces.find((workspace) => workspace.id === selectedWorkspaceId) ?? null,
    [selectedWorkspaceId, workspaces],
  );
  const selectedTask = useMemo(
    () => tasks.find((task) => task.id === selectedTaskId) ?? null,
    [selectedTaskId, tasks],
  );
  const activeSessions = useMemo(
    () => sessions.filter((session) => session.state.key === "active"),
    [sessions],
  );

  const [msgKindFilter, setMsgKindFilter] = useState("all");
  const [msgAuthorFilter, setMsgAuthorFilter] = useState("all");
  const [expandedMsgIds, setExpandedMsgIds] = useState<Set<string>>(new Set());

  const msgKindOptions = useMemo(
    () => [...new Set(messages.map((m) => m.messageKind))].sort(),
    [messages],
  );

  const filteredMessages = useMemo(
    () => messages.filter((m) => {
      if (msgAuthorFilter !== "all" && m.authorType !== msgAuthorFilter) return false;
      if (msgKindFilter !== "all" && m.messageKind !== msgKindFilter) return false;
      return true;
    }),
    [messages, msgAuthorFilter, msgKindFilter],
  );

  async function refreshTaskExecution(taskId: string) {
    const execution = await getTaskExecution(taskId);
    setTaskExecution(execution);
  }

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
        setSelectedTaskId("");
        setTaskExecution(null);
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

      const activeTaskId =
        selectedTaskId && nextTasks.some((task) => task.id === selectedTaskId)
          ? selectedTaskId
          : nextTasks[0]?.id ?? "";

      setSelectedTaskId(activeTaskId);
      if (activeTaskId) {
        await refreshTaskExecution(activeTaskId);
      } else {
        setTaskExecution(null);
      }

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
      successCriteria: parseLines(String(formData.get("successCriteria") ?? "")),
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

  async function handleChecklistCreate(formData: FormData) {
    if (!selectedTaskId) return;

    await addTaskChecklistItem({
      taskId: selectedTaskId,
      title: String(formData.get("title") ?? ""),
      isRequired: String(formData.get("isRequired") ?? "true") !== "false",
      sessionId: (formData.get("sessionId") as string) || undefined,
    });
    await refreshTaskExecution(selectedTaskId);
  }

  async function handleTaskUpdateCreate(formData: FormData) {
    if (!selectedTaskId) return;

    await createTaskUpdate({
      taskId: selectedTaskId,
      updateKind: String(formData.get("updateKind") ?? ""),
      summary: String(formData.get("summary") ?? ""),
      sessionId: (formData.get("sessionId") as string) || undefined,
      details: parseJsonObject(String(formData.get("details") ?? "")),
    });
    await refreshTaskExecution(selectedTaskId);
  }

  async function handleTaskArtifactCreate(formData: FormData) {
    if (!selectedTaskId) return;

    await createTaskArtifact({
      taskId: selectedTaskId,
      artifactKind: String(formData.get("artifactKind") ?? ""),
      value: String(formData.get("value") ?? ""),
      sessionId: (formData.get("sessionId") as string) || undefined,
      metadata: parseJsonObject(String(formData.get("metadata") ?? "")),
    });
    await refreshTaskExecution(selectedTaskId);
  }

  async function handleChecklistToggle(checklistItemId: string, isCompleted: boolean) {
    if (!selectedTaskId) return;

    await completeTaskChecklistItem({
      taskId: selectedTaskId,
      checklistItemId,
      isCompleted,
    });
    await refreshTaskExecution(selectedTaskId);
  }

  return (
    <main className="app-shell">
      <section className="hero">
        <div>
          <p className="eyebrow">Infosphere</p>
          <h1>Agent Work Console</h1>
          <p className="lede">Watch tasks, execution criteria, progress updates, artifacts, and live messages from the Vite frontend.</p>
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
          <textarea name="successCriteria" placeholder="Success criteria, one per line" rows={4} />
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

      <section className="grid task-grid">
        <Panel title="Tasks" count={tasks.length}>
          {tasks.length === 0 ? <EmptyState text="No tasks yet." /> : tasks.map((task) => (
            <button
              type="button"
              className={`item item-button${task.id === selectedTaskId ? " is-selected" : ""}`}
              key={task.id}
              onClick={() => {
                setSelectedTaskId(task.id);
                void refreshTaskExecution(task.id);
              }}
            >
              <div className="item-head">
                <strong>{task.title}</strong>
                <span className="pill">{task.state.name}</span>
              </div>
              <div className="item-meta">Priority {task.priority} · Assigned {task.assignedAgentId ?? "unassigned"}</div>
              <div className="item-subtle">Updated {new Date(task.updatedUtc).toLocaleString()}</div>
            </button>
          ))}
        </Panel>

        <Panel title="Task Execution" count={selectedTask ? 1 : 0} wide>
          {!selectedTask || !taskExecution ? <EmptyState text="Select a task to inspect checklist items, updates, and artifacts." /> : (
            <div className="execution-stack">
              <article className="item item-detail">
                <div className="item-head">
                  <strong>{selectedTask.title}</strong>
                  <span className="pill">{selectedTask.state.name}</span>
                </div>
                <div className="item-meta">Priority {selectedTask.priority} · Assigned {selectedTask.assignedAgentId ?? "unassigned"}</div>
              </article>

              <section className="execution-grid">
                <div className="item">
                  <div className="item-head">
                    <strong>Checklist</strong>
                    <span className="count-chip">{taskExecution.checklistItems.length}</span>
                  </div>
                  <div className="list">
                    {taskExecution.checklistItems.length === 0 ? <EmptyState text="No success criteria yet." /> : taskExecution.checklistItems.map((item) => (
                      <button
                        type="button"
                        className={`item item-button checklist-item${item.isCompleted ? " is-complete" : ""}`}
                        key={item.id}
                        onClick={() => void handleChecklistToggle(item.id, !item.isCompleted)}
                      >
                        <div className="item-head">
                          <strong>{item.ordinal}. {item.title}</strong>
                          <span className="pill">{item.isCompleted ? "Done" : item.isRequired ? "Required" : "Optional"}</span>
                        </div>
                        <div className="item-subtle">
                          {item.isCompleted
                            ? `Completed ${formatTimestamp(item.completedUtc)}`
                            : "Click to mark complete"}
                        </div>
                      </button>
                    ))}
                  </div>
                </div>

                <div className="item">
                  <div className="item-head">
                    <strong>Updates</strong>
                    <span className="count-chip">{taskExecution.updates.length}</span>
                  </div>
                  <div className="list">
                    {taskExecution.updates.length === 0 ? <EmptyState text="No structured progress updates yet." /> : taskExecution.updates.map((update) => (
                      <article className="item" key={update.id}>
                        <div className="item-head">
                          <strong>{update.updateKind}</strong>
                          <span className="pill">{formatTimestamp(update.createdUtc)}</span>
                        </div>
                        <div className="item-meta">{update.summary}</div>
                        {Object.keys(update.details).length > 0 ? (
                          <pre className="code-block">{JSON.stringify(update.details, null, 2)}</pre>
                        ) : null}
                      </article>
                    ))}
                  </div>
                </div>

                <div className="item">
                  <div className="item-head">
                    <strong>Artifacts</strong>
                    <span className="count-chip">{taskExecution.artifacts.length}</span>
                  </div>
                  <div className="list">
                    {taskExecution.artifacts.length === 0 ? <EmptyState text="No branch, commit, PR, or validation artifacts yet." /> : taskExecution.artifacts.map((artifact) => (
                      <article className="item" key={artifact.id}>
                        <div className="item-head">
                          <strong>{artifact.artifactKind}</strong>
                          <span className="pill">{formatTimestamp(artifact.createdUtc)}</span>
                        </div>
                        <div className="item-meta break-all">{artifact.value}</div>
                        {Object.keys(artifact.metadata).length > 0 ? (
                          <pre className="code-block">{JSON.stringify(artifact.metadata, null, 2)}</pre>
                        ) : null}
                      </article>
                    ))}
                  </div>
                </div>
              </section>
            </div>
          )}
        </Panel>
      </section>

      <section className="grid">
        <FormCard title="Add Checklist Item" onSubmit={handleChecklistCreate}>
          <input name="title" placeholder="Success criterion" required />
          <select name="isRequired" defaultValue="true">
            <option value="true">Required</option>
            <option value="false">Optional</option>
          </select>
          <input name="sessionId" placeholder="session id (optional)" />
          <button type="submit" disabled={!selectedTaskId}>
            Add Criterion
          </button>
        </FormCard>

        <FormCard title="Post Task Update" onSubmit={handleTaskUpdateCreate}>
          <input name="updateKind" placeholder="progress | validation | blocked" defaultValue="progress" required />
          <textarea name="summary" placeholder="What changed?" rows={3} required />
          <textarea name="details" placeholder='JSON details, optional. Example: {"tests":["npm test"]}' rows={3} />
          <input name="sessionId" placeholder="session id (optional)" />
          <button type="submit" disabled={!selectedTaskId}>
            Post Update
          </button>
        </FormCard>

        <FormCard title="Add Task Artifact" onSubmit={handleTaskArtifactCreate}>
          <input name="artifactKind" placeholder="branch | commit | pr | test_result" defaultValue="pr" required />
          <input name="value" placeholder="Artifact URL or identifier" required />
          <textarea name="metadata" placeholder='JSON metadata, optional. Example: {"status":"open"}' rows={3} />
          <input name="sessionId" placeholder="session id (optional)" />
          <button type="submit" disabled={!selectedTaskId}>
            Attach Artifact
          </button>
        </FormCard>

        <Panel title="Agent Sessions" count={activeSessions.length}>
          {activeSessions.length === 0 ? <EmptyState text="No active agent sessions." /> : activeSessions.map((session) => (
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
      </section>

      <section className="grid">
        <section className="card wide">
          <div className="panel-head">
            <h2>Workspace Messages</h2>
            <span className="count-chip">{filteredMessages.length}{filteredMessages.length !== messages.length ? `/${messages.length}` : ""}</span>
          </div>
          <div className="msg-filters">
            <label htmlFor="msgAuthorFilter">Author</label>
            <select
              id="msgAuthorFilter"
              value={msgAuthorFilter}
              onChange={(e) => setMsgAuthorFilter(e.target.value)}
            >
              <option value="all">All</option>
              <option value="human">Human</option>
              <option value="agent">Agent</option>
            </select>
            <label htmlFor="msgKindFilter">Kind</label>
            <select
              id="msgKindFilter"
              value={msgKindFilter}
              onChange={(e) => setMsgKindFilter(e.target.value)}
            >
              <option value="all">All</option>
              {msgKindOptions.map((k) => (
                <option key={k} value={k}>{k}</option>
              ))}
            </select>
          </div>
          <div className="list">
            {filteredMessages.length === 0 ? (
              <EmptyState text={messages.length === 0 ? "No workspace messages yet." : "No messages match the current filter."} />
            ) : filteredMessages.map((message) => {
              const isHuman = message.authorType === "human";
              const isExpanded = expandedMsgIds.has(message.id);
              const isLong = message.content.length > 200;
              const displayContent = isLong && !isExpanded
                ? message.content.slice(0, 200) + "…"
                : message.content;
              const itemClass = `item msg-item${
                isHuman ? " msg-human"
                : message.messageKind === "assessment" ? " msg-assessment"
                : " msg-agent-note"
              }`;
              return (
                <article className={itemClass} key={message.id}>
                  <div className="item-head">
                    <strong>{message.messageKind}</strong>
                    <span className="pill">{message.authorType}</span>
                  </div>
                  <div className="item-meta">
                    {message.authorId ?? "anonymous"} · {new Date(message.createdUtc).toLocaleString()}
                  </div>
                  <div className="msg-body">{displayContent}</div>
                  {isLong && (
                    <button
                      type="button"
                      className="msg-expand"
                      onClick={() =>
                        setExpandedMsgIds((prev) => {
                          const next = new Set(prev);
                          if (isExpanded) next.delete(message.id);
                          else next.add(message.id);
                          return next;
                        })
                      }
                    >
                      {isExpanded ? "Show less" : "Show more"}
                    </button>
                  )}
                </article>
              );
            })}
          </div>
        </section>
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
          const form = event.currentTarget;
          await props.onSubmit(new FormData(form));
          form.reset();
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

function parseLines(value: string): string[] {
  return value
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean);
}

function parseJsonObject(value: string): Record<string, unknown> {
  const trimmed = value.trim();
  if (!trimmed) {
    return {};
  }

  try {
    const parsed = JSON.parse(trimmed) as unknown;
    return parsed && typeof parsed === "object" && !Array.isArray(parsed) ? parsed as Record<string, unknown> : {};
  } catch {
    return {};
  }
}

function formatTimestamp(value: string | null): string {
  return value ? new Date(value).toLocaleString() : "unknown";
}
