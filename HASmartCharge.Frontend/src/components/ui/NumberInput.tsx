import { useState, type InputHTMLAttributes } from 'react'

type NumberInputProps = Omit<
  InputHTMLAttributes<HTMLInputElement>,
  'value' | 'onChange' | 'type'
> & {
  value: number
  onChange: (value: number) => void
}

/**
 * A number field that can actually be cleared. The plain
 * `value={n} onChange={(e) => set(Number(e.target.value))}` pattern turns an empty box into 0 the
 * moment the last digit is deleted (`Number('') === 0`), and the controlled re-render puts that 0
 * straight back — so "80" can never be cleared and retyped as "60".
 *
 * The typed text lives here; only parseable text is pushed up, and an empty (or unparseable) box
 * falls back to the last committed value on blur, so nothing can be saved as 0 by accident.
 */
export function NumberInput({ value, onChange, onBlur, ...rest }: NumberInputProps) {
  const [text, setText] = useState(() => String(value))
  const [lastValue, setLastValue] = useState(value)

  // Follow the prop when it changes from the outside (settings arriving from the API, a form
  // reset) without fighting what is being typed. Adjusted during render rather than in an effect:
  // no extra paint with the stale text, and no setState-in-effect.
  if (value !== lastValue) {
    setLastValue(value)
    if (Number(text) !== value || text.trim() === '') setText(String(value))
  }

  return (
    <input
      {...rest}
      type="number"
      value={text}
      onChange={(e) => {
        setText(e.target.value)
        const parsed = Number(e.target.value)
        if (e.target.value.trim() !== '' && !Number.isNaN(parsed)) onChange(parsed)
      }}
      onBlur={(e) => {
        if (text.trim() === '' || Number.isNaN(Number(text))) setText(String(value))
        onBlur?.(e)
      }}
    />
  )
}
