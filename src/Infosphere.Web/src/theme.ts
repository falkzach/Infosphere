export type Theme = "light" | "dark";

export const LS_THEME_KEY = "infosphere.theme";

export function getInitialTheme(): Theme {
  const stored = localStorage.getItem(LS_THEME_KEY);
  if (stored === "light" || stored === "dark") return stored;
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

export function applyTheme(theme: Theme) {
  document.documentElement.setAttribute("data-theme", theme);
}
