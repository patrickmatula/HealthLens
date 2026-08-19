import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { ImportCurrentDto } from '../api/types'
import { Icon } from '../components/Icon'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { useTheme } from '../theme/ThemeContext'
import { COLOR_THEMES } from '../theme/themes.generated'
import { useUnits } from '../units/UnitsContext'
import { UploadPage } from './UploadPage'
import { formatDateTime } from '../utils/format'
import './DashboardPage.css'
import './MorePage.css'

const UNIT_OPTIONS = [
  { value: 'metric' as const, label: 'Metrisch (km)' },
  { value: 'imperial' as const, label: 'Imperial (mi)' },
]

export function MorePage() {
  const [status, setStatus] = useState<ImportCurrentDto | null>(null)
  const [showImport, setShowImport] = useState(false)
  const { unit, setUnit } = useUnits()
  const { colorTheme, setColorTheme } = useTheme()

  useEffect(() => {
    api.importCurrent().then(setStatus)
  }, [])

  if (showImport) {
    return <UploadPage onImported={() => window.location.reload()} onCancel={() => setShowImport(false)} />
  }

  return (
    <div>
      <TopAppBar title="Mehr" />

      <div className="ghl-page-content">
        <Surface tone="low" className="ghl-more-card">
          <h2 className="ghl-section-title">Einstellungen</h2>
          <div className="ghl-more-setting">
            <div className="ghl-upload-option__title">Einheiten</div>
            <SegmentedButton options={UNIT_OPTIONS} value={unit} onChange={setUnit} />
          </div>
          <div className="ghl-more-setting">
            <div className="ghl-upload-option__title">Farbthema</div>
            <div className="ghl-theme-swatches">
              {COLOR_THEMES.map((t) => (
                <button
                  key={t.key}
                  type="button"
                  className={`ghl-theme-swatch ${colorTheme === t.key ? 'ghl-theme-swatch--selected' : ''}`}
                  style={{ background: t.seed }}
                  onClick={() => setColorTheme(t.key)}
                  aria-label={t.label}
                  title={t.label}
                >
                  {colorTheme === t.key && <Icon name="check" size={16} />}
                </button>
              ))}
            </div>
          </div>
        </Surface>

        <Surface tone="low" className="ghl-more-card">
          <h2 className="ghl-section-title">Datenquelle</h2>
          {status && (
            <dl className="ghl-more-list">
              <dt>Speicherung</dt>
              <dd>{status.mode === 'Persistent' ? 'Dauerhaft (übersteht einen Neustart)' : 'Nur für diese Sitzung'}</dd>
              {status.lastImportAtUtc && (
                <>
                  <dt>Letzter Import</dt>
                  <dd>{formatDateTime(status.lastImportAtUtc)}</dd>
                </>
              )}
              {status.lastScope && (
                <>
                  <dt>Umfang</dt>
                  <dd>{status.lastScope === 'Curated' ? 'Kuratiert' : 'Vollständig'}</dd>
                </>
              )}
              {status.rowsImported != null && (
                <>
                  <dt>Importierte Zeilen</dt>
                  <dd>{status.rowsImported.toLocaleString('de-AT')}</dd>
                </>
              )}
            </dl>
          )}
          <md-outlined-button onClick={() => setShowImport(true)}>Neuen Export importieren</md-outlined-button>
        </Surface>

        <Surface tone="low" className="ghl-more-card">
          <h2 className="ghl-section-title">Über GoogleHealthLens</h2>
          <p className="ghl-more-text">
            Eine lokale Auswertungs-App für deinen Google-Takeout-Export (Google Health/Fitbit). Alle Daten bleiben auf diesem
            Rechner — es wird nichts an einen Server außerhalb deines eigenen Backends gesendet.
          </p>
          <div className="ghl-more-nav-links">
            <Link className="ghl-more-nav-link" to="/workouts">
              <Icon name="workouts" size={18} /> Workouts
            </Link>
            <Link className="ghl-more-nav-link" to="/sleep">
              <Icon name="sleep" size={18} /> Schlaf
            </Link>
            <Link className="ghl-more-nav-link" to="/heart">
              <Icon name="heart" size={18} /> Herz
            </Link>
            <Link className="ghl-more-nav-link" to="/recovery">
              <Icon name="recovery" size={18} /> Erholung
            </Link>
          </div>
        </Surface>
      </div>
    </div>
  )
}
