import { useRef, useState } from 'react'
import { api } from '../api/client'
import type { ImportScope, ImportStatusDto } from '../api/types'
import { Icon } from '../components/Icon'
import { Logo } from '../components/Logo'
import { Surface } from '../components/Surface'
import { useLanguage } from '../i18n/LanguageContext'
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
  const { t } = useLanguage()
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
      setError(e instanceof Error ? e.message : t('upload.errorGeneric'))
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
          setError(s.errorMessage ?? t('upload.errorFailed'))
        }
      } catch {
        stopPolling()
        setError(t('upload.errorConnection'))
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
            {t('upload.backButton')}
          </md-text-button>
        )}
        <div className="ghl-upload-heading">
          <Logo size={36} className="ghl-upload-logo" />
          <h1 className="ghl-upload-title">{t('upload.title')}</h1>
        </div>
        <p className="ghl-upload-subtitle">{t('upload.subtitle')}</p>

        {onGoToDashboard && !importing && (
          <md-text-button onClick={onGoToDashboard} className="ghl-upload-dashboard-link">
            {t('upload.dashboardButton')}
          </md-text-button>
        )}

        {!importing && (
          <>
            <button type="button" className="ghl-dropzone" onClick={pickFile}>
              <Icon name="upload" size={32} />
              <span>{file ? file.name : t('upload.dropzoneCta')}</span>
            </button>
            <input ref={inputRef} type="file" accept=".zip" hidden onChange={onFileChange} />

            <div className="ghl-upload-option">
              <md-switch selected={persistent || undefined} onChange={(e) => setPersistent((e.target as HTMLInputElement & { selected: boolean }).selected)} />
              <div>
                <div className="ghl-upload-option__title">{t('upload.persistentTitle')}</div>
                <div className="ghl-upload-option__hint">
                  {persistent ? t('upload.persistentHintOn') : t('upload.persistentHintOff')}
                </div>
              </div>
            </div>

            <div className="ghl-upload-option">
              <div className="ghl-scope-radios" role="radiogroup" aria-label={t('upload.scopeGroupLabel')}>
                <label className="ghl-scope-radio">
                  <md-radio name="scope" checked={scope === 'Curated' || undefined} onChange={() => setScope('Curated')} />
                  <div>
                    <div className="ghl-upload-option__title">{t('upload.scopeCuratedTitle')}</div>
                    <div className="ghl-upload-option__hint">{t('upload.scopeCuratedHint')}</div>
                  </div>
                </label>
                <label className="ghl-scope-radio">
                  <md-radio name="scope" checked={scope === 'Full' || undefined} onChange={() => setScope('Full')} />
                  <div>
                    <div className="ghl-upload-option__title">{t('upload.scopeFullTitle')}</div>
                    <div className="ghl-upload-option__hint">{t('upload.scopeFullHint')}</div>
                  </div>
                </label>
              </div>
            </div>

            {error && <div className="ghl-upload-error">{error}</div>}

            <md-filled-button disabled={!file || undefined} onClick={startImport}>
              {t('upload.startButton')}
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
              <div className="ghl-upload-progress__rows">
                {t('upload.progressRows', { count: numberFmt.format(status.rowsImported) })}
              </div>
            )}
            {error && <div className="ghl-upload-error">{error}</div>}
          </div>
        )}
      </Surface>
    </div>
  )
}
