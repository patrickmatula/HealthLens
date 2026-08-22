import { useEffect, useState } from 'react'
import { Area, AreaChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Scatter, ScatterChart, Tooltip, XAxis, YAxis, ZAxis } from 'recharts'
import { api } from '../api/client'
import type { CorrelationPointDto, RecoveryOverviewDto, TimeframePreset } from '../api/types'
import { ReferenceRangeGauge } from '../components/ReferenceRangeGauge'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { useLanguage } from '../i18n/LanguageContext'
import {
  READINESS_DOMAIN,
  READINESS_SOURCE,
  READINESS_SOURCE_URL,
  SPO2_DOMAIN,
  SPO2_SOURCE,
  SPO2_SOURCE_URL,
  STRESS_SCORE_DOMAIN,
  STRESS_SCORE_SOURCE,
  STRESS_SCORE_SOURCE_URL,
  TEMPERATURE_SOURCE,
  TEMPERATURE_SOURCE_URL,
  getReadinessZones,
  getSpo2Zones,
  getStressScoreZones,
} from '../utils/references'
import './DashboardPage.css'

const tooltipStyle = { background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }

export function RecoveryPage() {
  const { t, language } = useLanguage()
  const [preset, setPreset] = useState<TimeframePreset>('all')

  const PRESETS: { value: TimeframePreset; label: string }[] = [
    { value: '30d', label: t('preset.30d') },
    { value: '1y', label: t('preset.1y') },
    { value: 'all', label: t('preset.all') },
  ]
  const [data, setData] = useState<RecoveryOverviewDto | null>(null)
  const [correlation, setCorrelation] = useState<CorrelationPointDto[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    Promise.all([api.recoveryOverview({ preset }), api.correlation()])
      .then(([overview, corr]) => {
        setData(overview)
        setCorrelation(corr)
      })
      .finally(() => setLoading(false))
  }, [preset])

  const hasData =
    data && (data.stressScore.length > 0 || data.readiness.length > 0 || data.spO2.length > 0 || data.temperature.length > 0)

  const rhrCorrelation = correlation.filter((c) => c.restingHeartRate != null)

  return (
    <div>
      <TopAppBar title={t('nav.recovery')}>
        <SegmentedButton options={PRESETS} value={preset} onChange={setPreset} />
      </TopAppBar>

      <div className="ghl-page-content">
        {!loading && !hasData && correlation.length === 0 && (
          <Surface tone="low">
            <p>{t('recovery.empty')}</p>
          </Surface>
        )}

        {rhrCorrelation.length >= 3 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">{t('recovery.correlationTitle')}</h2>
            <p className="ghl-chart-card__hint">{t('recovery.correlationHint')}</p>
            <ResponsiveContainer width="100%" height={280}>
              <ScatterChart>
                <CartesianGrid stroke="var(--md-sys-color-outline-variant)" />
                <XAxis type="number" dataKey="sleepScore" name={t('recovery.sleepScore')} tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" domain={['dataMin - 5', 'dataMax + 5']} />
                <YAxis type="number" dataKey="restingHeartRate" name={t('recovery.restingHr')} tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={36} domain={['dataMin - 2', 'dataMax + 2']} />
                <ZAxis range={[60, 60]} />
                <Tooltip contentStyle={tooltipStyle} cursor={{ strokeDasharray: '3 3' }} formatter={(v) => Number(v).toFixed(1)} />
                <Scatter data={rhrCorrelation} fill="#8e24aa" />
              </ScatterChart>
            </ResponsiveContainer>
          </Surface>
        )}

        {data && data.stressScore.length > 0 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">{t('recovery.stressScoreAssessmentTitle')}</h2>
            <ReferenceRangeGauge
              value={data.stressScore[data.stressScore.length - 1].score}
              domain={STRESS_SCORE_DOMAIN}
              zones={getStressScoreZones(language)}
              unit=""
            />
            <p className="ghl-chart-card__hint">
              {t('heart.sourcePrefix')}{' '}
              <a href={STRESS_SCORE_SOURCE_URL} target="_blank" rel="noreferrer">
                {STRESS_SCORE_SOURCE}
              </a>
              {t('heart.medicalDisclaimer')}
            </p>
          </Surface>
        )}

        {data && data.stressScore.length > 0 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">{t('recovery.stressScore')}</h2>
            <ResponsiveContainer width="100%" height={200}>
              <LineChart data={data.stressScore}>
                <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5, 10)} stroke="var(--md-sys-color-outline)" />
                <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={30} domain={[0, 100]} />
                <Tooltip contentStyle={tooltipStyle} />
                <Line type="monotone" dataKey="score" stroke="#fb8c00" dot={{ r: 3 }} strokeWidth={2} />
              </LineChart>
            </ResponsiveContainer>
          </Surface>
        )}

        {data && data.readiness.length > 0 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">{t('recovery.readinessAssessmentTitle')}</h2>
            <ReferenceRangeGauge
              value={data.readiness[data.readiness.length - 1].score}
              domain={READINESS_DOMAIN}
              zones={getReadinessZones(language)}
              unit=""
            />
            <p className="ghl-chart-card__hint">
              {t('heart.sourcePrefix')}{' '}
              <a href={READINESS_SOURCE_URL} target="_blank" rel="noreferrer">
                {READINESS_SOURCE}
              </a>
              {t('heart.medicalDisclaimer')}
            </p>
          </Surface>
        )}

        {data && data.readiness.length > 0 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">{t('recovery.readiness')}</h2>
            <ResponsiveContainer width="100%" height={200}>
              <LineChart data={data.readiness}>
                <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5, 10)} stroke="var(--md-sys-color-outline)" />
                <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={30} domain={[0, 100]} />
                <Tooltip contentStyle={tooltipStyle} />
                <Line type="monotone" dataKey="score" stroke="#43a047" dot={{ r: 3 }} strokeWidth={2} />
              </LineChart>
            </ResponsiveContainer>
          </Surface>
        )}

        {data && data.spO2.length > 0 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">{t('recovery.spo2AssessmentTitle')}</h2>
            <ReferenceRangeGauge
              value={data.spO2[data.spO2.length - 1].averagePercent}
              domain={SPO2_DOMAIN}
              zones={getSpo2Zones(language)}
              unit="%"
            />
            <p className="ghl-chart-card__hint">
              {t('heart.sourcePrefix')}{' '}
              <a href={SPO2_SOURCE_URL} target="_blank" rel="noreferrer">
                {SPO2_SOURCE}
              </a>
              {t('heart.medicalDisclaimer')}
            </p>
          </Surface>
        )}

        {data && data.spO2.length > 0 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">{t('recovery.spo2')}</h2>
            <ResponsiveContainer width="100%" height={200}>
              <AreaChart data={data.spO2}>
                <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5, 10)} stroke="var(--md-sys-color-outline)" />
                <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={30} domain={['dataMin - 2', 100]} />
                <Tooltip contentStyle={tooltipStyle} formatter={(v) => `${Number(v).toFixed(1)}%`} />
                <Area type="monotone" dataKey="averagePercent" stroke="#039be5" fill="#039be5" fillOpacity={0.2} strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          </Surface>
        )}

        {data && data.temperature.length > 0 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">{t('recovery.temperature')}</h2>
            <p className="ghl-chart-card__hint">
              {t('recovery.temperatureHint')} {t('heart.sourcePrefix')}{' '}
              <a href={TEMPERATURE_SOURCE_URL} target="_blank" rel="noreferrer">
                {TEMPERATURE_SOURCE}
              </a>
              {t('heart.medicalDisclaimer')}
            </p>
            <ResponsiveContainer width="100%" height={200}>
              <LineChart data={data.temperature}>
                <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5, 10)} stroke="var(--md-sys-color-outline)" />
                <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={30} unit="°" />
                <Tooltip contentStyle={tooltipStyle} formatter={(v) => `${Number(v).toFixed(2)}°C`} />
                <Line type="monotone" dataKey="deltaFromBaseline" stroke="#d81b60" dot={{ r: 3 }} strokeWidth={2} connectNulls />
              </LineChart>
            </ResponsiveContainer>
          </Surface>
        )}
      </div>
    </div>
  )
}
