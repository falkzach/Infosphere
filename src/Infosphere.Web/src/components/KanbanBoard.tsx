import { useMemo } from "react";
import type { Task, TaskChecklistItem, TaskExecution } from "../types";

const KANBAN_COLUMNS: { key: string; label: string }[] = [
  { key: "available", label: "Available" },
  { key: "in_progress", label: "In Progress" },
  { key: "blocked", label: "Blocked" },
  { key: "completed", label: "Completed" },
  { key: "cancelled", label: "Cancelled" },
];

export function KanbanBoard(props: {
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
