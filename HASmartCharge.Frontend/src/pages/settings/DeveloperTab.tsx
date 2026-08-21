import { useChargerSettings } from '@/hooks/useSettings'
import { OcppDeveloperTab } from './OcppDeveloperTab'
import { ZaptecDeveloperTab } from './ZaptecDeveloperTab'

/**
 * The Developer tab shows the console that matches the configured charger type: the OCPP call
 * console + live frame log, or the Zaptec API console + reference. Zaptec mode has no OCPP
 * session, so showing both would just be dead weight.
 */
export function DeveloperTab() {
  const { data: chargerSettings } = useChargerSettings()

  return chargerSettings?.chargerType === 'Zaptec' ? <ZaptecDeveloperTab /> : <OcppDeveloperTab />
}
