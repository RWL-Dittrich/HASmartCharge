// Prism, JSON grammar only (the core build plus one language, not the full bundle).
// prism-tomorrow is a dark theme, which matches this app's palette.
import Prism from 'prismjs'
import 'prismjs/components/prism-json'
import 'prismjs/themes/prism-tomorrow.css'

/** Prism-highlighted HTML for a chunk of JSON. Escaping is Prism's job. */
export function highlightJson(code: string): string {
  return Prism.highlight(code, Prism.languages.json, 'json')
}

/** Pretty-print JSON text, or return it unchanged when it does not parse. */
export function tryFormatJson(text: string): string {
  try {
    return JSON.stringify(JSON.parse(text), null, 2)
  } catch {
    return text
  }
}
