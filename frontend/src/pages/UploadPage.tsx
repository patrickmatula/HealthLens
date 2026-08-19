import { useRef, useState } from 'react'
import { api } from '../api/client'
import type { ImportScope, ImportStatusDto } from '../api/types'
import { Icon } from '../components/Icon'
import { Surface } from '../components/Surface'
import './UploadPage.css'

const numberFmt = new Intl.NumberFormat('de-AT')

export function UploadPage({
  onImported,
  onCancel,
  onGoToDashboard,
}: {
  onImported: () => void
  onCancel?: () => void
  /** Escape hatch for the boot screen: a persistent DB file exists, so there may already be data worth looking at even if this particular check was inconclusive. */
  onGoToDashboard?: () => void
}) {
  const [file, setFile] = useState<File | null>(null)
  const [persistent, setPersistent] = useState(true)
  const [scope, setScope] = useState<ImportScope>('Curated')
  const [status, setStatus] = useState<ImportStatusDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const pollRef = useRef<number | null>(null)

  function pickFile() {
    inputRef.current?.click()
  }

  function onFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    setFile(e.target.files?.[0] ?? null)
    setError(null)
  }

  async function startImport() {
    if (!file) return
    setError(null)
    try {
      const { jobId } = await api.startImport(file, persistent, scope)
      poll(jobId)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Import konnte nicht gestartet werden.')
    }
  }

  function poll(jobId: number) {
    pollRef.current = window.setInterval(async () => {
      try {
        const s = await api.importStatus(jobId)
        setStatus(s)
        if (s.status === 'Completed') {
          stopPolling()
          onImported()
        } else if (s.status === 'Failed') {
          stopPolling()
          setError(s.errorMessage ?? 'Import fehlgeschlagen.')
        }
      } catch {
        stopPolling()
        setError('Verbindung zum Server verloren.')
      }
    }, 800)
  }

  function stopPolling() {
    if (pollRef.current) {
      window.clearInterval(pollRef.current)
      pollRef.current = null
    }
  }

  const importing = status !== null && status.status === 'Running'

  return (
    <div className="ghl-upload-page">
      <Surface className="ghl-upload-card" tone="low">
        {onCancel && !importing && (
          <md-text-button onClick={onCancel} className="ghl-upload-cancel">
            ← Zurück
          </md-text-button>
        )}
        <h1 className="ghl-upload-title">GoogleHealthLens</h1>
        <p className="ghl-upload-subtitle">
          Lade deinen Google-Takeout-Export (Google Health) als .zip hoch, um deine Fitbit/Health-Daten zu erkunden.
        </p>

        {onGoToDashboard && !importing && (
          <md-text-button onClick={onGoToDashboard} className="ghl-upload-dashboard-link">
            Zum Dashboard →
          </md-text-button>
        )}

        {!importing && (
          <>
            <button type="button" className="ghl-dropzone" onClick={pickFile}>
              <Icon name="upload" size={32} />
              <span>{file ? file.name : 'Zip-Datei auswählen...'}</span>
            </button>
            <input ref={inputRef} type="file" accept=".zip" hidden onChange={onFileChange} />

            <div className="ghl-upload-option">
              <md-switch selected={persistent || undefined} onChange={(e) => setPersistent((e.target as HTMLInputElement & { selected: boolean }).selected)} />
              <div>
                <div className="ghl-upload-option__title">Dauerhaft speichern</div>
                <div className="ghl-upload-option__hint">
                  {persistent
                    ? 'Daten werden in eine lokale SQLite-Datenbank geschrieben und bleiben nach einem Neustart erhalten.'
                    : 'Daten werden nur für diese Sitzung geladen — nach einem Neustart der App sind sie wieder weg.'}
                </div>
              </div>
            </div>

            <div className="ghl-upload-option">
              <div className="ghl-scope-radios" role="radiogroup" aria-label="Datenumfang">
                <label className="ghl-scope-radio">
                  <md-radio name="scope" checked={scope === 'Curated' || undefined} onChange={() => setScope('Curated')} />
                  <div>
                    <div className="ghl-upload-option__title">Kuratiert (empfohlen)</div>
                    <div className="ghl-upload-option__hint">
                      Zusammenfassungen vollständig, hochfrequente Rohdaten (Herzfrequenz u.a.) aggregiert — außer bei Workouts. Schneller Import.
                    </div>
                  </div>
                </label>
                <label className="ghl-scope-radio">
                  <md-radio name="scope" checked={scope === 'Full' || undefined} onChange={() => setScope('Full')} />
                  <div>
                    <div className="ghl-upload-option__title">Vollständig, alles roh</div>
                    <div className="ghl-upload-option__hint">
                      Jede Zeile 1:1 importiert. Größere Datenbank, deutlich längerer Import.
                    </div>
                  </div>
                </label>
              </div>
            </div>

            {error && <div className="ghl-upload-error">{error}</div>}

            <md-filled-button disabled={!file || undefined} onClick={startImport}>
              Import starten
            </md-filled-button>
          </>
        )}

        {status && (
          <div className="ghl-upload-progress">
            <div className="ghl-upload-progress__header">
              <span className="ghl-upload-progress__step">{status.currentStep}</span>
              <span className="ghl-upload-progress__percent">{status.progressPercent}%</span>
            </div>
            <md-linear-progress value={status.progressPercent / 100} indeterminate={status.progressPercent === 0 || undefined} />
            {status.rowsImported > 0 && (
              <div className="ghl-upload-progress__rows">{numberFmt.format(status.rowsImported)} Zeilen importiert</div>
            )}
            {error && <div className="ghl-upload-error">{error}</div>}
          </div>
        )}
      </Surface>
    </div>
  )
}
