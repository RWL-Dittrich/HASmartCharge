import { useId } from 'react'
import { useHaEntities } from '@/hooks/useHa'

interface EntityPickerProps {
  label: string
  domain?: string
  /** Restrict suggestions to entities whose domain is one of these (client-side filter across multiple domains). */
  domains?: string[]
  value: string
  onChange: (value: string) => void
  placeholder?: string
  required?: boolean
}

/** Free-text input backed by a datalist of known HA entities, optionally filtered by domain(s). */
export function EntityPicker({ label, domain, domains, value, onChange, placeholder, required }: EntityPickerProps) {
  const listId = useId()
  const { data: allEntities } = useHaEntities(domains ? undefined : domain)
  const entities = domains
    ? allEntities?.filter((e) => domains.includes(e.entityId.split('.')[0]))
    : allEntities

  return (
    <label className="text-sm block">
      <span className="text-[#8892a4] block mb-1">{label}</span>
      <input
        list={listId}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder ?? (domain ? `${domain}.…` : 'entity_id')}
        required={required}
        className="w-full rounded-md border border-[#2a3042] bg-[#0f1117] px-3 py-2 text-white outline-none focus:border-blue-500"
      />
      <datalist id={listId}>
        {entities?.map((e) => (
          <option key={e.entityId} value={e.entityId}>
            {e.friendlyName ?? e.entityId}
          </option>
        ))}
      </datalist>
    </label>
  )
}
