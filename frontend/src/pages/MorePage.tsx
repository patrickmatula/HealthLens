import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { ImportCurrentDto } from '../api/types'
import { Icon } from '../components/Icon'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { useLanguage, type Language } from '../i18n/LanguageContext'
import { useShoesFeature } from '../shoes/ShoesFeatureContext'
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
  }, [])

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
                  className={`ghl-theme-swatch ${colorTheme === ct.key ? 'ghl-theme-swatch--selected' : ''}`}
                  style={{ background: ct.seed }}
                  onClick={() => setColorTheme(ct.key)}
                  aria-label={ct.label}
                  title={ct.label}
                >
                  {colorTheme === ct.key && <Icon name="check" size={16} />}
                </button>
              ))}
            </div>
          </div>
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
