import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Area, AreaChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '../api/client'
import type { SleepSummaryDto, TimeframePreset } from '../api/types'
import { Icon } from '../components/Icon'
import { KpiTile } from '../components/KpiTile'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { useLanguage } from '../i18n/LanguageContext'
import { formatDate, formatMinutes } from '../utils/format'
import './DashboardPage.css'
import './SleepPage.css'

export function SleepPage() {
  const { t } = useLanguage()
  const [preset, setPreset] = useState<TimeframePreset>('all')

  const PRESETS: { value: TimeframePreset; label: string }[] = [
    { value: '30d', label: t('preset.30d') },
    { value: '1y', label: t('preset.1y') },
    { value: 'all', label: t('preset.all') },
  ]
  const [data, setData] = useState<SleepSummaryDto | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    api
      .sleepSummary({ preset })
      .then(setData)
      .finally(() => setLoading(false))
  }, [preset])

  const chartData = data?.sessions
    .slice()
    .reverse()
    .map((s) => ({ date: s.startUtc.slice(0, 10), minutesAsleep: s.minutesAsleep, overallScore: s.overallScore }))

  return (
    <div>
      <TopAppBar title={t('nav.sleep')}>
        <SegmentedButton options={PRESETS} value={preset} onChange={setPreset} />
      </TopAppBar>

      <div className="ghl-page-content">
        {!loading && data && data.nights === 0 && (
          <Surface tone="low">
            <p>{t('sleep.empty')}</p>
          </Surface>
        )}

        {data && data.nights > 0 && (
          <>
            <div className="ghl-kpi-row">
              <KpiTile label={t('sleep.nights')} value={data.nights.toString()} />
              <KpiTile label={t('sleep.avgDuration')} value={formatMinutes(data.avgMinutesAsleep)} />
              <KpiTile label={t('sleep.avgTimeInBed')} value={formatMinutes(data.avgTimeInBedMinutes)} />
              <KpiTile label={t('sleep.avgEfficiency')} value={Math.round(data.avgEfficiencyPercent).toString()} unit="%" />
              {data.avgOverallScore != null && <KpiTile label={t('sleep.avgScore')} value={Math.round(data.avgOverallScore).toString()} />}
            </div>

            <Surface tone="low" className="ghl-chart-card">
              <h2 className="ghl-chart-card__title">{t('sleep.durationOverTime')}</h2>
              <ResponsiveContainer width="100%" height={240}>
                <AreaChart data={chartData}>
                  <defs>
                    <linearGradient id="sleepGradient" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor="#42a5f5" stopOpacity={0.4} />
                      <stop offset="100%" stopColor="#42a5f5" stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5)} stroke="var(--md-sys-color-outline)" />
                  <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={40} tickFormatter={(v: number) => `${Math.round(v / 60)}h`} />
                  <Tooltip
                    contentStyle={{ background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }}
                    formatter={(v) => formatMinutes(Number(v))}
                  />
                  <Area type="monotone" dataKey="minutesAsleep" stroke="#42a5f5" fill="url(#sleepGradient)" strokeWidth={2} />
                </AreaChart>
              </ResponsiveContainer>
            </Surface>

            <section>
              <h2 className="ghl-section-title">{t('sleep.nights')}</h2>
              <div className="ghl-sleep-list">
                {data.sessions.map((s) => (
                  <Link key={s.id} to={`/sleep/${s.id}`} className="ghl-sleep-row">
                    <Surface tone="low" className="ghl-sleep-row__surface">
                      <Icon name="sleep" />
                      <div className="ghl-sleep-row__main">
                        <div className="ghl-sleep-row__title">{formatDate(s.startUtc)}</div>
                        <div className="ghl-sleep-row__sub">
                          {formatMinutes(s.minutesAsleep)} {t('sleep.sleepSuffix')}
                        </div>
                      </div>
                      {s.overallScore != null && <div className="ghl-sleep-row__score">{Math.round(s.overallScore)}</div>}
                    </Surface>
                  </Link>
                ))}
              </div>
            </section>
          </>
        )}
      </div>
    </div>
  )
}
