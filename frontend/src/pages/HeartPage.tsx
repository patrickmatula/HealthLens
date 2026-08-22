import { useEffect, useState } from 'react'
import { Bar, BarChart, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '../api/client'
import type { HeartOverviewDto, TimeframePreset } from '../api/types'
import { KpiTile } from '../components/KpiTile'
import { ReferenceRangeGauge } from '../components/ReferenceRangeGauge'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { useLanguage } from '../i18n/LanguageContext'
import {
  AZM_SOURCE,
  AZM_SOURCE_URL,
  AZM_WEEKLY_TARGET,
  HRV_DOMAIN,
  HRV_SOURCE,
  HRV_SOURCE_URL,
  RESPIRATORY_RATE_DOMAIN,
  RESPIRATORY_RATE_SOURCE,
  RESPIRATORY_RATE_SOURCE_URL,
  RESTING_HR_DOMAIN,
  RESTING_HR_SOURCE,
  RESTING_HR_SOURCE_URL,
  getHrvZones,
  getRespiratoryRateZones,
  getRestingHrZones,
  hrvAssessment,
  restingHrAssessment,
} from '../utils/references'
import './DashboardPage.css'

const tooltipStyle = { background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }

export function HeartPage() {
  const { language, t } = useLanguage()
  const [preset, setPreset] = useState<TimeframePreset>('1y')

  const PRESETS: { value: TimeframePreset; label: string }[] = [
    { value: '30d', label: t('preset.30d') },
    { value: '1y', label: t('preset.1y') },
    { value: 'all', label: t('preset.all') },
  ]
  const [data, setData] = useState<HeartOverviewDto | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    api
      .heartOverview({ preset })
      .then(setData)
      .finally(() => setLoading(false))
  }, [preset])

  const hasData = data && (data.restingHeartRate.length > 0 || data.hrv.length > 0)

  return (
    <div>
      <TopAppBar title={t('nav.heart')}>
        <SegmentedButton options={PRESETS} value={preset} onChange={setPreset} />
      </TopAppBar>

      <div className="ghl-page-content">
        {!loading && !hasData && (
          <Surface tone="low">
            <p>{t('heart.empty')}</p>
          </Surface>
        )}

        {hasData && data && (
          <>
            <div className="ghl-kpi-row">
              {data.avgRestingHeartRate != null && (
                <KpiTile label={t('heart.avgRestingHr')} value={Math.round(data.avgRestingHeartRate).toString()} unit="bpm" />
              )}
              {data.avgHrv != null && <KpiTile label={t('heart.avgHrv')} value={data.avgHrv.toFixed(1)} unit="ms" />}
            </div>

            {data.avgRestingHeartRate != null && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">{t('heart.restingHrAssessmentTitle')}</h2>
                <ReferenceRangeGauge value={data.avgRestingHeartRate} domain={RESTING_HR_DOMAIN} zones={getRestingHrZones(language)} unit="bpm" />
                <p className="ghl-chart-card__hint">
                  {restingHrAssessment(data.avgRestingHeartRate, language)} {t('heart.sourcePrefix')}{' '}
                  <a href={RESTING_HR_SOURCE_URL} target="_blank" rel="noreferrer">
                    {RESTING_HR_SOURCE}
                  </a>
                  {t('heart.medicalDisclaimer')}
                </p>
              </Surface>
            )}

            {data.restingHeartRate.length > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">{t('heart.restingHrOverTime')}</h2>
                <ResponsiveContainer width="100%" height={240}>
                  <LineChart data={data.restingHeartRate}>
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5)} stroke="var(--md-sys-color-outline)" />
                    <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={36} domain={['dataMin - 3', 'dataMax + 3']} />
                    <Tooltip contentStyle={tooltipStyle} formatter={(v) => `${Number(v).toFixed(0)} bpm`} />
                    <Line type="monotone" dataKey="bpm" stroke="#e53935" dot={false} strokeWidth={2} />
                  </LineChart>
                </ResponsiveContainer>
              </Surface>
            )}

            {data.avgHrv != null && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">{t('heart.hrvAssessmentTitle')}</h2>
                <ReferenceRangeGauge value={data.avgHrv} domain={HRV_DOMAIN} zones={getHrvZones(language)} unit="ms" />
                <p className="ghl-chart-card__hint">
                  {hrvAssessment(data.avgHrv, language)} {t('heart.sourcePrefix')}{' '}
                  <a href={HRV_SOURCE_URL} target="_blank" rel="noreferrer">
                    {HRV_SOURCE}
                  </a>
                  {t('heart.medicalDisclaimer')}
                </p>
              </Surface>
            )}

            {data.hrv.length > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">{t('heart.hrvTitle')}</h2>
                <ResponsiveContainer width="100%" height={220}>
                  <LineChart data={data.hrv}>
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5)} stroke="var(--md-sys-color-outline)" />
                    <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={36} />
                    <Tooltip contentStyle={tooltipStyle} formatter={(v) => `${Number(v).toFixed(1)} ms`} />
                    <Line type="monotone" dataKey="rmssdMs" stroke="#8e24aa" dot={false} strokeWidth={2} />
                  </LineChart>
                </ResponsiveContainer>
              </Surface>
            )}

            {data.activeZoneMinutes.length > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">{t('heart.azmTitle')}</h2>
                <p className="ghl-chart-card__hint">
                  {t('heart.azmHint', { target: AZM_WEEKLY_TARGET })} {t('heart.sourcePrefix')}{' '}
                  <a href={AZM_SOURCE_URL} target="_blank" rel="noreferrer">
                    {AZM_SOURCE}
                  </a>
                  {t('heart.medicalDisclaimer')}
                </p>
                <ResponsiveContainer width="100%" height={220}>
                  <BarChart data={data.activeZoneMinutes}>
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5)} stroke="var(--md-sys-color-outline)" />
                    <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={30} />
                    <Tooltip contentStyle={tooltipStyle} />
                    <Bar dataKey="fatBurnMinutes" stackId="azm" fill="#ffb300" name={t('heart.fatBurn')} />
                    <Bar dataKey="cardioMinutes" stackId="azm" fill="#fb8c00" name={t('heart.cardio')} />
                    <Bar dataKey="peakMinutes" stackId="azm" fill="#e53935" name={t('heart.peak')} />
                  </BarChart>
                </ResponsiveContainer>
              </Surface>
            )}

            {data.respiratoryRate.length > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">{t('heart.respiratoryRateAssessmentTitle')}</h2>
                <ReferenceRangeGauge
                  value={data.respiratoryRate[data.respiratoryRate.length - 1].breathsPerMinute}
                  domain={RESPIRATORY_RATE_DOMAIN}
                  zones={getRespiratoryRateZones(language)}
                  unit="/min"
                />
                <p className="ghl-chart-card__hint">
                  {t('heart.sourcePrefix')}{' '}
                  <a href={RESPIRATORY_RATE_SOURCE_URL} target="_blank" rel="noreferrer">
                    {RESPIRATORY_RATE_SOURCE}
                  </a>
                  {t('heart.medicalDisclaimer')}
                </p>
              </Surface>
            )}

            {data.respiratoryRate.length > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">{t('heart.respiratoryRate')}</h2>
                <ResponsiveContainer width="100%" height={180}>
                  <LineChart data={data.respiratoryRate}>
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5)} stroke="var(--md-sys-color-outline)" />
                    <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={30} domain={['dataMin - 1', 'dataMax + 1']} />
                    <Tooltip contentStyle={tooltipStyle} formatter={(v) => `${Number(v).toFixed(1)} /min`} />
                    <Line type="monotone" dataKey="breathsPerMinute" stroke="#26a69a" dot={false} strokeWidth={2} />
                  </LineChart>
                </ResponsiveContainer>
              </Surface>
            )}
          </>
        )}
      </div>
    </div>
  )
}
