// Per-action OCPP 1.6 reference docs, one markdown file per call in ./ocpp-docs.
// Lazy glob: each doc becomes its own chunk and is fetched when the action is selected,
// so 19 reference files don't ride along in the main bundle.
const DOCS = import.meta.glob('./ocpp-docs/*.md', { query: '?raw', import: 'default' }) as Record<
  string,
  () => Promise<string>
>

/** Action names that have a doc file, in alphabetical (glob) order. */
export const DOCUMENTED_ACTIONS = Object.keys(DOCS).map((path) =>
  path.replace('./ocpp-docs/', '').replace('.md', ''),
)

/** Markdown source for an action, or null when undocumented (e.g. a hand-typed action). */
export function loadOcppDoc(action: string): Promise<string> | null {
  return DOCS[`./ocpp-docs/${action}.md`]?.() ?? null
}
