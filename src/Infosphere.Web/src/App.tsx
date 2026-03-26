import { useEffect, useMemo, useReducer, useRef, useState } from "react";
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
  registerAgentSession,
} from "./api";
import type { AgentSession, Task, TaskExecution, Workspace, WorkspaceMessage } from "./types";
import { applyTheme, getInitialTheme, LS_THEME_KEY, type Theme } from "./theme";
import { loadSelectionFromStorage, LS_TASK_KEY, LS_WORKSPACE_KEY, selectionReducer } from "./selectionReducer";
import { parseJsonObject, parseLines } from "./utils";
import { EmptyState, FormCard, Panel } from "./components/common";
import { KanbanBoard } from "./components/KanbanBoard";
import { TaskExecutionPanel } from "./components/TaskExecutionPanel";
import { WorkspaceMessagesPanel } from "./components/WorkspaceMessagesPanel";

const TASK_PAGE_SIZE = 25;

export function App() {
  const [theme, setTheme] = useState<Theme>(getInitialTheme);
  useEffect(() => {
    applyTheme(theme);
    localStorage.setItem(LS_THEME_KEY, theme);
  }, [theme]);

  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [taskExecutions, setTaskExecutions] = useState<Map<string, TaskExecution>>(new Map());

  const [selection, dispatch] = useReducer(selectionReducer, undefined, loadSelectionFromStorage);
  const { selectedWorkspaceId, selectedTaskId } = selection;

  // Refs so interval callbacks always read current values without stale closures
  const selectionRef = useRef(selection);
  useEffect(() => { selectionRef.current = selection; }, [selection]);
  const taskPageRef = useRef(1);

  useEffect(() => {
    localStorage.setItem(LS_WORKSPACE_KEY, selectedWorkspaceId);
    localStorage.setItem(LS_TASK_KEY, selectedTaskId);
  }, [selectedWorkspaceId, selectedTaskId]);

  // Reset to page 1 when workspace changes
  useEffect(() => { taskPageRef.current = 1; }, [selectedWorkspaceId]);

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
    () => sessions.filter((s) => s.state.key === "active"),
    [sessions],
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
      setSessions(nextSessions);
      setMessages(nextMessages);
      dispatch({ type: "TASKS_LOADED", ids: nextTasks.map((t) => t.id) });

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
    const intervalId = window.setInterval(() => { void refresh(); }, 5000);
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
    await completeTaskChecklistItem({ taskId: selectedTaskId, checklistItemId, isCompleted });
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
          <a href={`${getApiBaseUrl()}/openapi/v0.json`} target="_blank" rel="noreferrer">OpenAPI</a>
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
            onChange={(e) => dispatch({ type: "SELECT_WORKSPACE", id: e.target.value })}
          >
            {workspaces.map((w) => (
              <option key={w.id} value={w.id}>{w.name}</option>
            ))}
          </select>
          <button type="button" onClick={() => void refresh()}>Refresh</button>
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
          <button type="submit" disabled={!selectedWorkspaceId}>Register Session</button>
        </FormCard>

        <FormCard title="Create Task" onSubmit={handleTaskCreate}>
          <input name="title" placeholder="Task title" required />
          <input name="priority" placeholder="Priority" type="number" defaultValue={0} required />
          <textarea name="successCriteria" placeholder="Success criteria, one per line" rows={4} />
          <button type="submit" disabled={!selectedWorkspaceId}>Create Task</button>
        </FormCard>
      </section>

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

        {selectedTask && (
          <TaskExecutionPanel
            selectedTask={selectedTask}
            taskExecution={taskExecution}
            onChecklistToggle={handleChecklistToggle}
            onChecklistCreate={handleChecklistCreate}
            onTaskUpdateCreate={handleTaskUpdateCreate}
            onTaskArtifactCreate={handleTaskArtifactCreate}
          />
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
        <WorkspaceMessagesPanel
          messages={messages}
          selectedWorkspaceId={selectedWorkspaceId}
          onMessageCreate={handleMessageCreate}
        />
      </section>
    </main>
  );
}
