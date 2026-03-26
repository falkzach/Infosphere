export type SelectionState = {
  selectedWorkspaceId: string;
  selectedTaskId: string;
};

export type SelectionAction =
  | { type: "SELECT_WORKSPACE"; id: string }
  | { type: "SELECT_TASK"; id: string }
  | { type: "WORKSPACES_LOADED"; ids: string[] }
  | { type: "TASKS_LOADED"; ids: string[] };

export function selectionReducer(state: SelectionState, action: SelectionAction): SelectionState {
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

export const LS_WORKSPACE_KEY = "infosphere.selectedWorkspaceId";
export const LS_TASK_KEY = "infosphere.selectedTaskId";

export function loadSelectionFromStorage(): SelectionState {
  return {
    selectedWorkspaceId: localStorage.getItem(LS_WORKSPACE_KEY) ?? "",
    selectedTaskId: localStorage.getItem(LS_TASK_KEY) ?? "",
  };
}
