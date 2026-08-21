import { useEffect, useState } from 'react'
import Markdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { BookOpen, Loader2 } from 'lucide-react'
import { MARKDOWN_COMPONENTS } from './OcppDocPanel'
import { loadZaptecDoc } from './zaptecDocs'

/** Zaptec API reference for one call, rendered from src/pages/settings/zaptec-docs/<slug>.md. */
export function ZaptecDocPanel({ slug }: { slug: string }) {
  const [markdown, setMarkdown] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    const pending = loadZaptecDoc(slug)
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
  }, [slug])

  return (
    // Own card, own scroll — same shell as the OCPP reference panel.
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
            No reference for this call. The request is still sent as typed — the full endpoint list
            lives at{' '}
            <a
              href="https://docs.zaptec.com/reference/"
              className="text-blue-400 underline-offset-2 hover:underline"
            >
              docs.zaptec.com/reference
            </a>
            .
          </p>
        )}

        {!loading && markdown !== null && (
          <>
            <Markdown remarkPlugins={[remarkGfm]} components={MARKDOWN_COMPONENTS}>
              {markdown}
            </Markdown>
            <p className="mt-6 max-w-[72ch] border-t border-[#2a3042] pt-3 text-xs leading-5 text-[#8892a4]">
              Responses show verbatim next to the Send button with their HTTP status. Rate limits:
              10 requests/s on the API, 1 request/s on the token endpoint; a 429&#39;s Retry-After
              is honored once automatically.
            </p>
          </>
        )}
      </div>
    </section>
  )
}
