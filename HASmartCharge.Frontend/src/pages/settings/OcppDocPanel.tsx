import { useEffect, useState } from 'react'
import Markdown, { type Components } from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { BookOpen, Loader2 } from 'lucide-react'
import { loadOcppDoc } from './ocppDocs'

// No typography plugin in this project, so map each markdown node to the tab's palette by hand.
const MARKDOWN_COMPONENTS: Components = {
  h1: ({ children }) => <h4 className="text-sm font-semibold text-white">{children}</h4>,
  h2: ({ children }) => (
    <h5 className="mt-5 mb-2 text-xs font-semibold uppercase tracking-wide text-[#8892a4]">
      {children}
    </h5>
  ),
  p: ({ children }) => <p className="my-2 text-xs leading-relaxed text-[#c3cad8]">{children}</p>,
  ul: ({ children }) => (
    <ul className="my-2 list-disc space-y-1 pl-4 text-xs leading-relaxed text-[#c3cad8]">
      {children}
    </ul>
  ),
  li: ({ children }) => <li>{children}</li>,
  strong: ({ children }) => <strong className="font-semibold text-white">{children}</strong>,
  em: ({ children }) => <em className="text-[#8892a4]">{children}</em>,
  code: ({ children }) => (
    <code className="rounded bg-[#0f1117] px-1 py-0.5 font-mono text-[11px] text-blue-300">
      {children}
    </code>
  ),
  blockquote: ({ children }) => (
    <blockquote className="my-3 rounded-r-md border-l-2 border-amber-500 bg-amber-500/5 px-3 py-1.5 text-amber-300 [&_p]:text-amber-300">
      {children}
    </blockquote>
  ),
  // Tables carry the field reference, so let them scroll rather than squash the layout.
  table: ({ children }) => (
    <div className="my-2 overflow-x-auto rounded-md border border-[#2a3042]">
      <table className="w-full border-collapse text-left text-xs">{children}</table>
    </div>
  ),
  thead: ({ children }) => <thead className="bg-[#0f1117]">{children}</thead>,
  th: ({ children }) => (
    <th className="whitespace-nowrap border-b border-[#2a3042] px-2.5 py-1.5 font-medium text-[#8892a4]">
      {children}
    </th>
  ),
  td: ({ children }) => (
    <td className="border-b border-[#2a3042] px-2.5 py-1.5 align-top text-[#c3cad8] last:border-r-0">
      {children}
    </td>
  ),
  hr: () => <hr className="my-4 border-[#2a3042]" />,
  a: ({ href, children }) => (
    <a href={href} className="text-blue-400 hover:underline">
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
    <div className="rounded-lg border border-[#2a3042] bg-[#0f1117]/40 p-4">
      <div className="mb-3 flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-[#8892a4]">
        <BookOpen className="h-3.5 w-3.5" /> Reference
      </div>

      {loading && (
        <div className="flex items-center gap-2 text-xs text-[#8892a4]">
          <Loader2 className="h-3.5 w-3.5 animate-spin" /> Loading…
        </div>
      )}

      {!loading && markdown === null && (
        <p className="text-xs leading-relaxed text-[#8892a4]">
          No reference for <code className="text-blue-300">{action || '(empty)'}</code>. The action
          is still sent as typed — the charger decides whether it understands it, and answers with
          a <code className="text-blue-300">NotImplemented</code> CALLERROR if it does not.
        </p>
      )}

      {!loading && markdown !== null && (
        <div className="max-h-[32rem] overflow-y-auto pr-1">
          <Markdown remarkPlugins={[remarkGfm]} components={MARKDOWN_COMPONENTS}>
            {markdown}
          </Markdown>
          <p className="mt-4 border-t border-[#2a3042] pt-3 text-[11px] leading-relaxed text-[#8892a4]">
            Response fields arrive under <code className="text-blue-300">rawPayload</code> in the
            result below. Field lengths follow OCPP 1.6J; a charger may still reject values it
            does not support.
          </p>
        </div>
      )}
    </div>
  )
}
