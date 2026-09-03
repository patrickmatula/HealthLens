import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { GoogleHealthStatusDto, ImportCurrentDto } from '../api/types'
import { Icon } from '../components/Icon'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { useBodyFeature } from '../body/BodyFeatureContext'
import { useLanguage, type Language } from '../i18n/LanguageContext'
import { useShoesFeature } from '../shoes/ShoesFeatureContext'
import { useWeatherFeature } from '../weather/WeatherFeatureContext'
import { useTheme } from '../theme/ThemeContext'
import { COLOR_THEMES } from '../theme/themes.generated'
import { useUnits } from '../units/UnitsContext'
import { UploadPage } from './UploadPage'
import { formatDateTime } from '../utils/format'
import './DashboardPage.css'
import './MorePage.css'

export function MorePage() {
  const [status, setStatus] = useState<ImportCurrentDto | null>(null)
  const [showImport, setShowImport] = useState(false)
  const { unit, setUnit } = useUnits()
  const { colorTheme, setColorTheme } = useTheme()
  const { language, setLanguage, t } = useLanguage()
  const { enabled: shoesEnabled, setEnabled: setShoesEnabled } = useShoesFeature()
  const { enabled: bodyEnabled, setEnabled: setBodyEnabled } = useBodyFeature()
  const { enabled: weatherEnabled, setEnabled: setWeatherEnabled } = useWeatherFeature()

  const [googleHealth, setGoogleHealth] = useState<GoogleHealthStatusDto | null>(null)
  const [clientId, setClientId] = useState('')
  const [clientSecret, setClientSecret] = useState('')
  const [savingConfig, setSavingConfig] = useState(false)
  const [connecting, setConnecting] = useState(false)
  const [syncing, setSyncing] = useState(false)
  const [syncMessage, setSyncMessage] = useState<string | null>(null)
  const pollRef = useRef<number | null>(null)

  const unitOptions = [
    { value: 'metric' as const, label: t('more.unitMetric') },
    { value: 'imperial' as const, label: t('more.unitImperial') },
  ]
  const languageOptions: { value: Language; label: string }[] = [
    { value: 'de', label: 'Deutsch' },
    { value: 'en', label: 'English' },
  ]

  useEffect(() => {
    api.importCurrent().then(setStatus)
    api.googleHealthStatus().then(setGoogleHealth)
  }, [])

  useEffect(() => {
    return () => {
      if (pollRef.current != null) window.clearInterval(pollRef.current)
    }
  }, [])

  async function saveGoogleHealthConfig() {
    if (!clientId.trim() || !clientSecret.trim()) return
    setSavingConfig(true)
    try {
      const result = await api.saveGoogleHealthConfig(clientId.trim(), clientSecret.trim())
      setGoogleHealth(result)
      setClientId('')
      setClientSecret('')
    } finally {
      setSavingConfig(false)
    }
  }

  async function connectGoogleHealth() {
    setConnecting(true)
    try {
      const { url } = await api.googleHealthAuthorizeUrl()
      window.open(url, 'ghl-google-health-auth', 'width=520,height=680')

      let attempts = 0
      pollRef.current = window.setInterval(async () => {
        attempts += 1
        const latest = await api.googleHealthStatus()
        setGoogleHealth(latest)
        if (latest.connected || attempts >= 40) {
          if (pollRef.current != null) window.clearInterval(pollRef.current)
          pollRef.current = null
          setConnecting(false)
        }
      }, 3000)
    } catch {
      setConnecting(false)
    }
  }

  async function disconnectGoogleHealth() {
    await api.disconnectGoogleHealth()
    setGoogleHealth(await api.googleHealthStatus())
    setSyncMessage(null)
  }

  async function setAutoSync(enabled: boolean) {
    setGoogleHealth(await api.setGoogleHealthAutoSync(enabled))
  }

  async function syncGoogleHealth() {
    setSyncing(true)
    setSyncMessage(null)
    try {
      const result = await api.syncGoogleHealth()
      setSyncMessage(t('more.googleHealthSyncDone', { count: String(result.activityDaysSynced + result.weightEntriesSynced + result.workoutsSynced) }))
      setGoogleHealth(await api.googleHealthStatus())
    } catch (err) {
      setSyncMessage(err instanceof Error ? err.message : String(err))
    } finally {
      setSyncing(false)
    }
  }

  if (showImport) {
    return <UploadPage onImported={() => window.location.reload()} onCancel={() => setShowImport(false)} />
  }

  return (
    <div>
      <TopAppBar title={t('nav.more')} />

      <div className="ghl-page-content">
        <Surface tone="low" className="ghl-more-card">
          <h2 className="ghl-section-title">{t('more.settingsTitle')}</h2>
          <div className="ghl-more-setting">
            <div className="ghl-upload-option__title">{t('more.unitsLabel')}</div>
            <SegmentedButton options={unitOptions} value={unit} onChange={setUnit} />
          </div>
          <div className="ghl-more-setting">
            <div className="ghl-upload-option__title">{t('more.languageLabel')}</div>
            <SegmentedButton options={languageOptions} value={language} onChange={setLanguage} />
          </div>
          <div className="ghl-more-setting">
            <div className="ghl-upload-option__title">{t('more.colorThemeLabel')}</div>
            <div className="ghl-theme-swatches">
              {COLOR_THEMES.map((ct) => (
                <button
                  key={ct.key}
                  type="button"
                  className="ghl-theme-swatch-item"
                  onClick={() => setColorTheme(ct.key)}
                  aria-label={ct.label}
                  aria-pressed={colorTheme === ct.key}
                >
                  <span
                    className={`ghl-theme-swatch ${colorTheme === ct.key ? 'ghl-theme-swatch--selected' : ''}`}
                    style={{ background: ct.seed }}
                  >
                    {colorTheme === ct.key && <Icon name="check" size={16} />}
                  </span>
                  <span className="ghl-theme-swatch-label">{ct.label}</span>
                </button>
              ))}
            </div>
          </div>
        </Surface>

        <Surface tone="low" className="ghl-more-card">
          <Link className="ghl-more-nav-link" to="/year-in-review">
            <Icon name="trophy" size={18} /> {t('yearReview.navLink')}
          </Link>
        </Surface>

        <Surface tone="low" className="ghl-more-card">
          <h2 className="ghl-section-title">{t('more.optionalFeaturesTitle')}</h2>

          <div className="ghl-feature-row">
            <div className="ghl-feature-row__header">
              <Icon name="shoe" size={20} />
              <div className="ghl-feature-row__title">{t('shoes.settingsTitle')}</div>
              <md-switch
                selected={shoesEnabled || undefined}
                onChange={(e) => setShoesEnabled((e.target as HTMLInputElement & { selected: boolean }).selected)}
              />
            </div>
            <div className="ghl-feature-row__hint">{t('shoes.enableHint')}</div>
            <Link className="ghl-more-nav-link" to="/shoes">
              <Icon name="shoe" size={18} /> {t('shoes.manageLink')}
            </Link>
          </div>

          <div className="ghl-feature-row">
            <div className="ghl-feature-row__header">
              <Icon name="body" size={20} />
              <div className="ghl-feature-row__title">{t('body.settingsTitle')}</div>
              <md-switch
                selected={bodyEnabled || undefined}
                onChange={(e) => setBodyEnabled((e.target as HTMLInputElement & { selected: boolean }).selected)}
              />
            </div>
            <div className="ghl-feature-row__hint">{t('body.enableHint')}</div>
            <Link className="ghl-more-nav-link" to="/body">
              <Icon name="body" size={18} /> {t('body.manageLink')}
            </Link>
          </div>

          <div className="ghl-feature-row">
            <div className="ghl-feature-row__header">
              <Icon name="sun" size={20} />
              <div className="ghl-feature-row__title">{t('weather.settingsTitle')}</div>
              <md-switch
                selected={weatherEnabled || undefined}
                onChange={(e) => setWeatherEnabled((e.target as HTMLInputElement & { selected: boolean }).selected)}
              />
            </div>
            <div className="ghl-feature-row__hint">{t('weather.enableHint')}</div>
          </div>
        </Surface>

        <Surface tone="low" className="ghl-more-card">
          <h2 className="ghl-section-title">{t('more.dataSourceTitle')}</h2>
          {status && (
            <dl className="ghl-more-list">
              <dt>{t('more.storageLabel')}</dt>
              <dd>{status.mode === 'Persistent' ? t('more.storagePersistent') : t('more.storageEphemeral')}</dd>
              {status.lastImportAtUtc && (
                <>
                  <dt>{t('more.lastImportLabel')}</dt>
                  <dd>{formatDateTime(status.lastImportAtUtc)}</dd>
                </>
              )}
              {status.lastScope && (
                <>
                  <dt>{t('more.scopeLabel')}</dt>
                  <dd>{status.lastScope === 'Curated' ? t('more.scopeCurated') : t('more.scopeFull')}</dd>
                </>
              )}
              {status.rowsImported != null && (
                <>
                  <dt>{t('more.rowsImportedLabel')}</dt>
                  <dd>{status.rowsImported.toLocaleString(language === 'de' ? 'de-AT' : 'en-US')}</dd>
                </>
              )}
            </dl>
          )}
          <md-outlined-button onClick={() => setShowImport(true)}>{t('more.reimportButton')}</md-outlined-button>
        </Surface>

        <Surface tone="low" className="ghl-more-card">
          <div className="ghl-feature-row__header">
            <Icon name="sync" size={20} />
            <div className="ghl-feature-row__title">{t('more.googleHealthTitle')}</div>
          </div>
          <p className="ghl-more-text">{t('more.googleHealthIntro')}</p>

          {status?.mode !== 'Persistent' ? (
            <p className="ghl-more-text">{t('more.googleHealthNeedsPersistent')}</p>
          ) : (
            <>
              <ol className="ghl-google-health__steps">
                <li>
                  {t('more.googleHealthStep1')}{' '}
                  <a href="https://developers.google.com/health/setup" target="_blank" rel="noreferrer">
                    {t('more.googleHealthStep1Link')}
                  </a>
                </li>
                <li>{t('more.googleHealthStep2')}</li>
                <li>
                  {t('more.googleHealthStep3')} <code>{googleHealth?.redirectUri ?? '…'}</code>
                </li>
                <li>{t('more.googleHealthStep4')}</li>
              </ol>

              {!googleHealth?.configured && (
                <div className="ghl-google-health__config">
                  <div className="ghl-body-entry__input-wrap">
                    <input
                      type="text"
                      placeholder={t('more.googleHealthClientIdLabel')}
                      value={clientId}
                      onChange={(e) => setClientId(e.target.value)}
                    />
                  </div>
                  <div className="ghl-body-entry__input-wrap">
                    <input
                      type="password"
                      placeholder={t('more.googleHealthClientSecretLabel')}
                      value={clientSecret}
                      onChange={(e) => setClientSecret(e.target.value)}
                    />
                  </div>
                  <md-filled-button disabled={savingConfig || !clientId.trim() || !clientSecret.trim() || undefined} onClick={saveGoogleHealthConfig}>
                    {t('more.googleHealthSaveConfig')}
                  </md-filled-button>
                </div>
              )}

              {googleHealth?.configured && !googleHealth.connected && (
                <div className="ghl-google-health__connected">
                  {googleHealth.lastError && <p className="ghl-more-text ghl-google-health__error">{googleHealth.lastError}</p>}
                  <p className="ghl-more-text">{t('more.googleHealthHttpsNote')}</p>
                  <div className="ghl-google-health__actions">
                    <md-filled-button disabled={connecting || undefined} onClick={connectGoogleHealth}>
                      {connecting ? t('more.googleHealthConnecting') : t('more.googleHealthConnect')}
                    </md-filled-button>
                    <md-outlined-button onClick={disconnectGoogleHealth}>{t('more.googleHealthReset')}</md-outlined-button>
                  </div>
                </div>
              )}

              {googleHealth?.connected && (
                <div className="ghl-google-health__connected">
                  <dl className="ghl-more-list">
                    <dt>{t('more.googleHealthLastSyncLabel')}</dt>
                    <dd>{googleHealth.lastSyncUtc ? formatDateTime(googleHealth.lastSyncUtc) : t('more.googleHealthNeverSynced')}</dd>
                    {googleHealth.lastSyncSummary && (
                      <>
                        <dt>{t('more.googleHealthLastSyncSummaryLabel')}</dt>
                        <dd>{googleHealth.lastSyncSummary}</dd>
                      </>
                    )}
                  </dl>
                  {googleHealth.lastError && <p className="ghl-more-text ghl-google-health__error">{googleHealth.lastError}</p>}
                  {syncMessage && <p className="ghl-more-text">{syncMessage}</p>}
                  <div className="ghl-google-health__actions">
                    <md-filled-button disabled={syncing || undefined} onClick={syncGoogleHealth}>
                      {syncing ? t('more.googleHealthSyncing') : t('more.googleHealthSyncNow')}
                    </md-filled-button>
                    <md-outlined-button onClick={disconnectGoogleHealth}>{t('more.googleHealthDisconnect')}</md-outlined-button>
                  </div>
                  <div className="ghl-feature-row__header ghl-google-health__autosync">
                    <div className="ghl-feature-row__title">{t('more.googleHealthAutoSyncTitle')}</div>
                    <md-switch
                      selected={googleHealth.autoSyncEnabled || undefined}
                      onChange={(e) => setAutoSync((e.target as HTMLInputElement & { selected: boolean }).selected)}
                    />
                  </div>
                  <div className="ghl-feature-row__hint">{t('more.googleHealthAutoSyncHint')}</div>
                </div>
              )}
            </>
          )}
        </Surface>

        <Surface tone="low" className="ghl-more-card">
          <h2 className="ghl-section-title">{t('more.aboutTitle')}</h2>
          <p className="ghl-more-text">{t('more.aboutText')}</p>
          <div className="ghl-more-nav-links">
            <Link className="ghl-more-nav-link" to="/workouts">
              <Icon name="workouts" size={18} /> {t('nav.workouts')}
            </Link>
            <Link className="ghl-more-nav-link" to="/sleep">
              <Icon name="sleep" size={18} /> {t('nav.sleep')}
            </Link>
            <Link className="ghl-more-nav-link" to="/heart">
              <Icon name="heart" size={18} /> {t('nav.heart')}
            </Link>
            <Link className="ghl-more-nav-link" to="/recovery">
              <Icon name="recovery" size={18} /> {t('nav.recovery')}
            </Link>
          </div>
        </Surface>
      </div>
    </div>
  )
}
