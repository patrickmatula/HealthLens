import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { ImportCurrentDto } from '../api/types'
import { Icon } from '../components/Icon'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { UploadPage } from './UploadPage'
import { formatDateTime } from '../utils/format'
import './DashboardPage.css'
import './MorePage.css'

export function MorePage() {
  const [status, setStatus] = useState<ImportCurrentDto | null>(null)
  const [showImport, setShowImport] = useState(false)

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
