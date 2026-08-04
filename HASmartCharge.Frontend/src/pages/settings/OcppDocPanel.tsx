import { useEffect, useState } from 'react'
import Markdown, { type Components } from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { BookOpen, Loader2 } from 'lucide-react'
import { loadOcppDoc } from './ocppDocs'

// No typography plugin in this project, so map each markdown node to the tab's palette by hand.
// Body copy runs at text-sm/relaxed rather than text-xs: this panel is read, not scanned.
const MARKDOWN_COMPONENTS: Components = {
  h1: ({ children }) => (
    <h4 className="text-lg font-semibold tracking-tight text-white">{children}</h4>
  ),
  h2: ({ children }) => (
    <h5 className="mt-7 mb-2 border-b border-[#2a3042] pb-1.5 text-xs font-semibold uppercase tracking-[0.08em] text-[#7f8ba0]">
      {children}
    </h5>
  ),
  h3: ({ children }) => (
    <h6 className="mt-5 mb-1.5 text-sm font-semibold text-white">{children}</h6>
  ),
  p: ({ children }) => (
    <p className="my-2.5 max-w-[72ch] text-sm leading-6 text-[#c8cfdc]">{children}</p>
  ),
  ul: ({ children }) => (
    <ul className="my-2.5 max-w-[72ch] list-disc space-y-1.5 pl-5 text-sm leading-6 text-[#c8cfdc] marker:text-[#5c6478]">
      {children}
    </ul>
  ),
  ol: ({ children }) => (
    <ol className="my-2.5 max-w-[72ch] list-decimal space-y-1.5 pl-5 text-sm leading-6 text-[#c8cfdc]">
      {children}
    </ol>
  ),
  li: ({ children }) => <li className="[&>p]:my-0">{children}</li>,
  strong: ({ children }) => <strong className="font-semibold text-white">{children}</strong>,
  em: ({ children }) => <em className="text-[#9aa5b8]">{children}</em>,
  code: ({ children }) => (
    <code className="rounded border border-[#2a3042] bg-[#0f1117] px-1.5 py-0.5 font-mono text-[0.8125rem] text-blue-300">
      {children}
    </code>
  ),
  pre: ({ children }) => (
    <pre className="my-3 overflow-x-auto rounded-md border border-[#2a3042] bg-[#0f1117] p-3 font-mono text-xs leading-relaxed text-[#c3cad8] [&_code]:border-0 [&_code]:bg-transparent [&_code]:p-0 [&_code]:text-[#c3cad8]">
      {children}
    </pre>
  ),
  blockquote: ({ children }) => (
    <blockquote className="my-3 max-w-[72ch] rounded-r-md border-l-2 border-amber-500 bg-amber-500/5 px-3 py-2 text-sm text-amber-300 [&_p]:my-0 [&_p]:text-amber-300">
      {children}
    </blockquote>
  ),
  // Tables carry the field reference, so let them scroll rather than squash the layout.
  table: ({ children }) => (
    <div className="my-3 overflow-x-auto rounded-md border border-[#2a3042]">
      <table className="w-full border-collapse text-left text-sm">{children}</table>
    </div>
  ),
  thead: ({ children }) => <thead className="bg-[#0f1117]">{children}</thead>,
  th: ({ children }) => (
    <th className="whitespace-nowrap border-b border-[#2a3042] px-3 py-2 text-xs font-semibold uppercase tracking-wide text-[#7f8ba0]">
      {children}
    </th>
  ),
  // Zebra striping so a wide field row stays readable across the panel.
  tr: ({ children }) => <tr className="even:bg-[#0f1117]/40">{children}</tr>,
  td: ({ children }) => (
    <td className="border-b border-[#22283a] px-3 py-2 align-top leading-6 text-[#c8cfdc] last:border-r-0">
      {children}
    </td>
  ),
  hr: () => <hr className="my-5 border-[#2a3042]" />,
  a: ({ href, children }) => (
    <a href={href} className="text-blue-400 underline-offset-2 hover:underline">
      {children}
    </a>
  ),
}

/** OCPP 1.6 reference for one action, rendered from src/pages/settings/ocpp-docs/<Action>.md. */
export function OcppDocPanel({ action }: { action: string }) {
  const [markdown, setMarkdown] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    const pending = loadOcppDoc(action.trim())
    if (!pending) {
      setMarkdown(null)
      setLoading(false)
      return
    }

    let cancelled = false
    setLoading(true)
    pending
      .then((text) => {
        if (!cancelled) setMarkdown(text)
      })
      .catch(() => {
        if (!cancelled) setMarkdown(null)
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [action])

  return (
    // Own card, own scroll: it fills the grid column so long references never stretch the page.
    <section className="flex min-h-0 flex-col rounded-lg border border-[#2a3042] bg-[#151a26] shadow-lg shadow-black/20 lg:overflow-hidden">
      <header className="flex shrink-0 items-center gap-2 border-b border-[#2a3042] px-4 py-3 text-xs font-semibold uppercase tracking-wide text-[#8892a4]">
        <BookOpen className="h-3.5 w-3.5" /> Reference
      </header>

      <div className="min-h-0 flex-1 px-5 py-4 lg:overflow-y-auto">
        {loading && (
          <div className="flex items-center gap-2 text-xs text-[#8892a4]">
            <Loader2 className="h-3.5 w-3.5 animate-spin" /> Loading…
          </div>
        )}

        {!loading && markdown === null && (
          <p className="max-w-[72ch] text-sm leading-6 text-[#8892a4]">
            No reference for <code className="text-blue-300">{action || '(empty)'}</code>. The
            action is still sent as typed — the charger decides whether it understands it, and
            answers with a <code className="text-blue-300">NotImplemented</code> CALLERROR if it
            does not.
          </p>
        )}

        {!loading && markdown !== null && (
          <>
            <Markdown remarkPlugins={[remarkGfm]} components={MARKDOWN_COMPONENTS}>
              {markdown}
            </Markdown>
            <p className="mt-6 max-w-[72ch] border-t border-[#2a3042] pt-3 text-xs leading-5 text-[#8892a4]">
              Response fields arrive under <code className="text-blue-300">rawPayload</code> in the
              result next to the Send button. Field lengths follow OCPP 1.6J; a charger may still
              reject values it does not support.
            </p>
          </>
        )}
      </div>
    </section>
  )
}
