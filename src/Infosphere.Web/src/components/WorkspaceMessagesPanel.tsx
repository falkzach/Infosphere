import { useEffect, useMemo, useRef, useState } from "react";
import type { WorkspaceMessage } from "../types";
import { EmptyState } from "./common";

export function WorkspaceMessagesPanel(props: {
  messages: WorkspaceMessage[];
  selectedWorkspaceId: string;
  onMessageCreate: (formData: FormData) => Promise<void>;
}) {
  const { messages, selectedWorkspaceId, onMessageCreate } = props;

  const [msgKindFilter, setMsgKindFilter] = useState("all");
  const [msgAuthorFilter, setMsgAuthorFilter] = useState("all");
  const [expandedMsgIds, setExpandedMsgIds] = useState<Set<string>>(new Set());
  const msgScrollRef = useRef<HTMLDivElement>(null);

  // Scroll to bottom whenever the messages list updates (new messages arrive)
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

  return (
    <section className="card wide msg-pane">
      <div className="panel-head">
        <h2>Workspace Messages</h2>
        <span className="count-chip">
          {filteredMessages.length}{filteredMessages.length !== messages.length ? `/${messages.length}` : ""}
        </span>
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
            await onMessageCreate(new FormData(form));
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
  );
}
