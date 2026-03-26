import { useEffect, useMemo, useReducer, useRef, useState } from "react";

// Kanban columns in display order
const KANBAN_COLUMNS: { key: string; label: string }[] = [
  { key: "available", label: "Available" },
  { key: "in_progress", label: "In Progress" },
  { key: "blocked", label: "Blocked" },
  { key: "completed", label: "Completed" },
  { key: "cancelled", label: "Cancelled" },
];
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
import type { AgentSession, Task, TaskChecklistItem, TaskExecution, Workspace, WorkspaceMessage } from "./types";

// --- Selection state machine ---

type SelectionState = {
  selectedWorkspaceId: string;
  selectedTaskId: string;
};

type SelectionAction =
  | { type: "SELECT_WORKSPACE"; id: string }
  | { type: "SELECT_TASK"; id: string }
  | { type: "WORKSPACES_LOADED"; ids: string[] }
  | { type: "TASKS_LOADED"; ids: string[] };

function selectionReducer(state: SelectionState, action: SelectionAction): SelectionState {
  switch (action.type) {
    case "SELECT_WORKSPACE":
      // User explicitly chose a workspace — clear task selection (tasks are workspace-scoped)
      return { selectedWorkspaceId: action.id, selectedTaskId: "" };
    case "SELECT_TASK":
      return { ...state, selectedTaskId: action.id };
    case "WORKSPACES_LOADED": {
      // Preserve current workspace if it still exists; otherwise fall to first
      const keep = state.selectedWorkspaceId !== "" && action.ids.includes(state.selectedWorkspaceId);
      return { ...state, selectedWorkspaceId: keep ? state.selectedWorkspaceId : (action.ids[0] ?? "") };
    }
    case "TASKS_LOADED": {
      // Preserve current task if it still exists; otherwise fall to first
      const keep = state.selectedTaskId !== "" && action.ids.includes(state.selectedTaskId);
      return { ...state, selectedTaskId: keep ? state.selectedTaskId : (action.ids[0] ?? "") };
    }
  }
}

const LS_WORKSPACE_KEY = "infosphere.selectedWorkspaceId";
const LS_TASK_KEY = "infosphere.selectedTaskId";
const LS_THEME_KEY = "infosphere.theme";

// --- Theme ---

type Theme = "light" | "dark";

function getInitialTheme(): Theme {
  const stored = localStorage.getItem(LS_THEME_KEY);
  if (stored === "light" || stored === "dark") return stored;
  // Fall back to OS preference
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function applyTheme(theme: Theme) {
  document.documentElement.setAttribute("data-theme", theme);
}

function loadSelectionFromStorage(): SelectionState {
  return {
    selectedWorkspaceId: localStorage.getItem(LS_WORKSPACE_KEY) ?? "",
    selectedTaskId: localStorage.getItem(LS_TASK_KEY) ?? "",
  };
}

// --- App ---

const TASK_PAGE_SIZE = 25;

export function App() {
  const [theme, setTheme] = useState<Theme>(getInitialTheme);

  // Apply theme to document and persist whenever it changes
  useEffect(() => {
    applyTheme(theme);
    localStorage.setItem(LS_THEME_KEY, theme);
  }, [theme]);

  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [taskTotalCount, setTaskTotalCount] = useState(0);
  const [taskPage, setTaskPage] = useState(1);
  const [taskExecutions, setTaskExecutions] = useState<Map<string, TaskExecution>>(new Map());

  const [selection, dispatch] = useReducer(selectionReducer, undefined, loadSelectionFromStorage);
  const { selectedWorkspaceId, selectedTaskId } = selection;

  // Refs so interval callbacks always read current values without stale closures
  const selectionRef = useRef(selection);
  useEffect(() => { selectionRef.current = selection; }, [selection]);
  const taskPageRef = useRef(taskPage);
  useEffect(() => { taskPageRef.current = taskPage; }, [taskPage]);

  // Persist selection to localStorage whenever it changes
  useEffect(() => {
    localStorage.setItem(LS_WORKSPACE_KEY, selectedWorkspaceId);
    localStorage.setItem(LS_TASK_KEY, selectedTaskId);
  }, [selectedWorkspaceId, selectedTaskId]);

  // Reset to page 1 when workspace changes
  useEffect(() => {
    setTaskPage(1);
  }, [selectedWorkspaceId]);
  const [sessions, setSessions] = useState<AgentSession[]>([]);
  const [messages, setMessages] = useState<WorkspaceMessage[]>([]);
  const [status, setStatus] = useState("Loading...");

  const selectedWorkspace = useMemo(
    () => workspaces.find((w) => w.id === selectedWorkspaceId) ?? null,
    [selectedWorkspaceId, workspaces],
  );
  const selectedTask = useMemo(
    () => tasks.find((t) => t.id === selectedTaskId) ?? null,
    [selectedTaskId, tasks],
  );
  const taskExecution = taskExecutions.get(selectedTaskId) ?? null;
  const activeSessions = useMemo(
    () => sessions.filter((session) => session.state.key === "active"),
    [sessions],
  );

  const [msgKindFilter, setMsgKindFilter] = useState("all");
  const [msgAuthorFilter, setMsgAuthorFilter] = useState("all");
  const [expandedMsgIds, setExpandedMsgIds] = useState<Set<string>>(new Set());

  // Scroll to bottom whenever the messages list updates (new messages arrive)
  const msgScrollRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const el = msgScrollRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [messages]);

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
    setTaskExecutions((prev) => new Map(prev).set(taskId, execution));
  }

  async function refresh() {
    try {
      setStatus("Refreshing workspace state...");
      const nextWorkspaces = await listWorkspaces();
      setWorkspaces(nextWorkspaces);

      // Read selection from ref (always current, avoids stale interval closure).
      // Then dispatch to keep React state in sync — reducer applies the same logic.
      const currentWorkspaceId = selectionRef.current.selectedWorkspaceId;
      const activeWorkspaceId =
        currentWorkspaceId !== "" && nextWorkspaces.some((w) => w.id === currentWorkspaceId)
          ? currentWorkspaceId
          : nextWorkspaces[0]?.id ?? "";
      dispatch({ type: "WORKSPACES_LOADED", ids: nextWorkspaces.map((w) => w.id) });

      if (!activeWorkspaceId) {
        setTasks([]);
        setTaskExecutions(new Map());
        setSessions([]);
        setMessages([]);
        setStatus("Create a workspace to begin.");
        return;
      }

      const [pagedTasks, nextSessions, nextMessages] = await Promise.all([
        listTasks(activeWorkspaceId, taskPageRef.current, TASK_PAGE_SIZE),
        listAgentSessions(activeWorkspaceId),
        listWorkspaceMessages(activeWorkspaceId),
      ]);

      const nextTasks = pagedTasks.items;
      setTasks(nextTasks);
      setTaskTotalCount(pagedTasks.totalCount);
      setSessions(nextSessions);
      setMessages(nextMessages);

      const currentTaskId = selectionRef.current.selectedTaskId;
      const activeTaskId =
        currentTaskId !== "" && nextTasks.some((t) => t.id === currentTaskId)
          ? currentTaskId
          : nextTasks[0]?.id ?? "";
      void activeTaskId; // used by reducer dispatch below
      dispatch({ type: "TASKS_LOADED", ids: nextTasks.map((t) => t.id) });

      // Fetch execution for all tasks on the current page so checklist items are visible in the list view
      if (nextTasks.length > 0) {
        const execResults = await Promise.all(nextTasks.map((t) => getTaskExecution(t.id)));
        setTaskExecutions(new Map(execResults.map((e) => [e.taskId, e])));
      } else {
        setTaskExecutions(new Map());
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
          <button
            type="button"
            className="theme-toggle"
            onClick={() => setTheme((t) => (t === "dark" ? "light" : "dark"))}
            aria-label={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
          >
            {theme === "dark" ? "☀ Light" : "☾ Dark"}
          </button>
        </div>
      </section>

      <section className="toolbar card">
        <div className="toolbar-row">
          <label htmlFor="workspaceSelect">Workspace</label>
          <select
            id="workspaceSelect"
            value={selectedWorkspaceId}
            onChange={(event) => dispatch({ type: "SELECT_WORKSPACE", id: event.target.value })}
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
      </section>

      {/* Task workspace: kanban on the left, execution panel on the right when a task is selected */}
      <section className={`task-workspace${selectedTask ? " has-selection" : ""}`}>
        <section className="card kanban-section">
          <div className="panel-head">
            <h2>Tasks</h2>
            <span className="count-chip">{tasks.length}</span>
          </div>
          {tasks.length === 0
            ? <EmptyState text="No tasks yet." />
            : <KanbanBoard
                tasks={tasks}
                selectedTaskId={selectedTaskId}
                taskExecutions={taskExecutions}
                onTaskSelect={(id) => dispatch({ type: "SELECT_TASK", id })}
                onTaskExecutionRefresh={refreshTaskExecution}
              />
          }
        </section>

        {/* Right-side execution panel — only rendered when a task is selected */}
        {selectedTask && (
          <section className="card task-execution-panel">
            <article className="item item-detail">
              <div className="item-head">
                <strong>{selectedTask.title}</strong>
                <span className="pill">{selectedTask.state.name}</span>
              </div>
              <div className="item-meta">Priority {selectedTask.priority} · Assigned {selectedTask.assignedAgentId ?? "unassigned"}</div>
            </article>

            {!taskExecution ? (
              <EmptyState text="Loading execution data..." />
            ) : (
              <>
                {/* Checklist */}
                <div className="execution-section">
                  <div className="panel-head">
                    <strong>Checklist</strong>
                    <span className="count-chip">{taskExecution.checklistItems.length}</span>
                  </div>
                  <div className="list">
                    {taskExecution.checklistItems.length === 0 ? (
                      <EmptyState text="No success criteria yet." />
                    ) : taskExecution.checklistItems.map((item) => (
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
                  <form
                    className="inline-form"
                    onSubmit={async (e) => { e.preventDefault(); await handleChecklistCreate(new FormData(e.currentTarget)); e.currentTarget.reset(); }}
                  >
                    <input name="title" placeholder="Success criterion" required />
                    <select name="isRequired" defaultValue="true">
                      <option value="true">Required</option>
                      <option value="false">Optional</option>
                    </select>
                    <input name="sessionId" placeholder="session id (optional)" />
                    <button type="submit">Add Criterion</button>
                  </form>
                </div>

                {/* Updates */}
                <div className="execution-section">
                  <div className="panel-head">
                    <strong>Updates</strong>
                    <span className="count-chip">{taskExecution.updates.length}</span>
                  </div>
                  <div className="list">
                    {taskExecution.updates.length === 0 ? (
                      <EmptyState text="No structured progress updates yet." />
                    ) : taskExecution.updates.map((update) => (
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
                  <form
                    className="inline-form"
                    onSubmit={async (e) => { e.preventDefault(); await handleTaskUpdateCreate(new FormData(e.currentTarget)); e.currentTarget.reset(); }}
                  >
                    <input name="updateKind" placeholder="progress | validation | blocked" defaultValue="progress" required />
                    <textarea name="summary" placeholder="What changed?" rows={2} required />
                    <textarea name="details" placeholder='JSON details, optional. Example: {"tests":["npm test"]}' rows={2} />
                    <input name="sessionId" placeholder="session id (optional)" />
                    <button type="submit">Post Update</button>
                  </form>
                </div>

                {/* Artifacts */}
                <div className="execution-section">
                  <div className="panel-head">
                    <strong>Artifacts</strong>
                    <span className="count-chip">{taskExecution.artifacts.length}</span>
                  </div>
                  <div className="list">
                    {taskExecution.artifacts.length === 0 ? (
                      <EmptyState text="No branch, commit, PR, or validation artifacts yet." />
                    ) : taskExecution.artifacts.map((artifact) => (
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
                  <form
                    className="inline-form"
                    onSubmit={async (e) => { e.preventDefault(); await handleTaskArtifactCreate(new FormData(e.currentTarget)); e.currentTarget.reset(); }}
                  >
                    <input name="artifactKind" placeholder="branch | commit | pr | test_result" defaultValue="pr" required />
                    <input name="value" placeholder="Artifact URL or identifier" required />
                    <textarea name="metadata" placeholder='JSON metadata, optional. Example: {"status":"open"}' rows={2} />
                    <input name="sessionId" placeholder="session id (optional)" />
                    <button type="submit">Attach Artifact</button>
                  </form>
                </div>
              </>
            )}
          </section>
        )}
      </section>

      <section className="grid">
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
        <section className="card wide msg-pane">
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
          {/* Fixed-height scrollable message list */}
          <div className="msg-scroll list" ref={msgScrollRef}>
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
          {/* Integrated post message widget */}
          <div className="msg-post">
            <form
              className="msg-post-form"
              onSubmit={async (event) => {
                event.preventDefault();
                const form = event.currentTarget;
                await handleMessageCreate(new FormData(form));
                form.reset();
              }}
            >
              <div className="msg-post-meta">
                <input name="authorType" placeholder="author type" defaultValue="human" required />
                <input name="authorId" placeholder="author id (optional)" />
                <input name="messageKind" placeholder="message kind" defaultValue="note" required />
              </div>
              <textarea name="content" placeholder="What should the workspace know?" rows={3} required />
              <button type="submit" disabled={!selectedWorkspaceId}>
                Post Message
              </button>
            </form>
          </div>
        </section>
      </section>
    </main>
  );
}

function KanbanBoard(props: {
  tasks: Task[];
  selectedTaskId: string;
  taskExecutions: Map<string, TaskExecution>;
  onTaskSelect: (id: string) => void;
  onTaskExecutionRefresh: (id: string) => Promise<void>;
}) {
  const byState = useMemo(() => {
    const map = new Map<string, Task[]>(KANBAN_COLUMNS.map((col) => [col.key, []]));
    for (const task of props.tasks) {
      map.get(task.state.key)?.push(task);
    }
    // Sort each column by priority descending, then updated time descending
    for (const bucket of map.values()) {
      bucket.sort((a, b) => b.priority - a.priority || new Date(b.updatedUtc).getTime() - new Date(a.updatedUtc).getTime());
    }
    return map;
  }, [props.tasks]);

  return (
    <div className="kanban-board">
      {KANBAN_COLUMNS.map((col) => {
        const columnTasks = byState.get(col.key) ?? [];
        return (
          <div key={col.key} className={`kanban-column kanban-col-${col.key}`}>
            <div className="kanban-column-head">
              <strong>{col.label}</strong>
              <span className="count-chip">{columnTasks.length}</span>
            </div>
            <div className="kanban-cards">
              {columnTasks.length === 0 ? (
                <div className="item item-subtle kanban-empty">Empty</div>
              ) : columnTasks.map((task) => (
                <button
                  type="button"
                  className={`item item-button${task.id === props.selectedTaskId ? " is-selected" : ""}`}
                  key={task.id}
                  onClick={() => {
                    props.onTaskSelect(task.id);
                    void props.onTaskExecutionRefresh(task.id);
                  }}
                >
                  <div className="item-head">
                    <strong className="kanban-task-title">{task.title}</strong>
                  </div>
                  <div className="item-meta">P{task.priority} · {task.assignedAgentId ?? "unassigned"}</div>
                  <TaskChecklistPreview items={props.taskExecutions.get(task.id)?.checklistItems ?? []} />
                </button>
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );
}

function TaskChecklistPreview(props: { items: TaskChecklistItem[] }) {
  if (props.items.length === 0) return null;
  return (
    <div className="checklist-preview">
      {props.items.map((item) => (
        <div key={item.id} className={`checklist-preview-item${item.isCompleted ? " is-complete" : ""}`}>
          <span className="checklist-status" aria-hidden="true">{item.isCompleted ? "✓" : "○"}</span>
          <span className="checklist-label">{item.title}</span>
        </div>
      ))}
    </div>
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
