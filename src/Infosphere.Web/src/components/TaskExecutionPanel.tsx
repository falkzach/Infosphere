import type { Task, TaskExecution } from "../types";
import { formatTimestamp } from "../utils";
import { EmptyState } from "./common";

export function TaskExecutionPanel(props: {
  selectedTask: Task;
  taskExecution: TaskExecution | null;
  onChecklistToggle: (id: string, isCompleted: boolean) => Promise<void>;
  onChecklistCreate: (formData: FormData) => Promise<void>;
  onTaskUpdateCreate: (formData: FormData) => Promise<void>;
  onTaskArtifactCreate: (formData: FormData) => Promise<void>;
}) {
  const { selectedTask, taskExecution } = props;

  return (
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
                  onClick={() => void props.onChecklistToggle(item.id, !item.isCompleted)}
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
              onSubmit={async (e) => { e.preventDefault(); await props.onChecklistCreate(new FormData(e.currentTarget)); e.currentTarget.reset(); }}
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
              onSubmit={async (e) => { e.preventDefault(); await props.onTaskUpdateCreate(new FormData(e.currentTarget)); e.currentTarget.reset(); }}
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
              onSubmit={async (e) => { e.preventDefault(); await props.onTaskArtifactCreate(new FormData(e.currentTarget)); e.currentTarget.reset(); }}
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
  );
}
