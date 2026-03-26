import type { ReactNode } from "react";

export function FormCard(props: {
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

export function Panel(props: { title: string; count: number; wide?: boolean; children: ReactNode }) {
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

export function EmptyState(props: { text: string }) {
  return <div className="item item-subtle">{props.text}</div>;
}
