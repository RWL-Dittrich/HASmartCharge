// Per-call Zaptec API reference docs, one markdown file per template in ./zaptec-docs.
// Same lazy-glob mechanism as ocppDocs.ts: each doc is its own chunk, fetched on selection.
const DOCS = import.meta.glob('./zaptec-docs/*.md', { query: '?raw', import: 'default' }) as Record<
  string,
  () => Promise<string>
>

/** Doc slugs that have a file, in alphabetical (glob) order. */
export const DOCUMENTED_ZAPTEC_CALLS = Object.keys(DOCS).map((path) =>
  path.replace('./zaptec-docs/', '').replace('.md', ''),
)

/** Markdown source for a template's doc slug, or null when undocumented. */
export function loadZaptecDoc(slug: string): Promise<string> | null {
  return DOCS[`./zaptec-docs/${slug}.md`]?.() ?? null
}
