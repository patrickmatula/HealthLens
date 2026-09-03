import { useEffect, useMemo, useState } from 'react'
import { Area, AreaChart, Bar, BarChart, ComposedChart, Legend, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '../api/client'
import type {
  ConsistencyDayDto,
  DailyActivityPointDto,
  DashboardOverviewDto,
  FlashbackDto,
  FunFactsDto,
  TimeframePreset,
  WeeklyDigestDto,
  WorkoutLocationDto,
} from '../api/types'
import { ConsistencyHeatmap } from '../components/ConsistencyHeatmap'
import { KpiTile } from '../components/KpiTile'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { Icon } from '../components/Icon'
import { TrainingLocationsMap } from '../components/TrainingLocationsMap'
import { useLanguage, type TranslationKey } from '../i18n/LanguageContext'
import { formatDate, formatDistanceKm, formatDuration } from '../utils/format'
import './DashboardPage.css'

type ChartGranularity = 'daily' | 'week' | 'month'

const CATEGORY_LABEL_KEYS: Record<string, 'category.run' | 'category.walk' | 'category.bike' | 'category.strength' | 'category.other'> = {
  Lauf: 'category.run',
  Spaziergang: 'category.walk',
  Rad: 'category.bike',
  Kraft: 'category.strength',
  Sonstiges: 'category.other',
}

function bucketKey(dateStr: string, mode: 'week' | 'month'): string {
  if (mode === 'month') return dateStr.slice(0, 7)
  const d = new Date(`${dateStr}T00:00:00Z`)
  const isoDay = d.getUTCDay() || 7
  d.setUTCDate(d.getUTCDate() - isoDay + 1)
  return d.toISOString().slice(0, 10)
}

function aggregateDays(days: DailyActivityPointDto[], mode: 'week' | 'month') {
  const buckets = new Map<string, { date: string; steps: number; rhrSum: number; rhrCount: number; sleepSum: number; sleepCount: number }>()
  for (const d of days) {
    const key = bucketKey(d.date, mode)
    const entry = buckets.get(key) ?? { date: key, steps: 0, rhrSum: 0, rhrCount: 0, sleepSum: 0, sleepCount: 0 }
    entry.steps += d.steps ?? 0
    if (d.restingHeartRateBpm != null) {
      entry.rhrSum += d.restingHeartRateBpm
      entry.rhrCount += 1
    }
    if (d.sleepScore != null) {
      entry.sleepSum += d.sleepScore
      entry.sleepCount += 1
    }
    buckets.set(key, entry)
  }
  return [...buckets.values()]
    .sort((a, b) => a.date.localeCompare(b.date))
    .map((b) => ({
      date: b.date,
      steps: b.steps,
      restingHeartRateBpm: b.rhrCount > 0 ? b.rhrSum / b.rhrCount : null,
      sleepScore: b.sleepCount > 0 ? b.sleepSum / b.sleepCount : null,
    }))
}

function formatBucketLabel(date: string, mode: ChartGranularity): string {
  if (mode === 'month') {
    const [, m] = date.split('-')
    return m
  }
  return date.slice(5)
}

// Reference distances for the "fun comparisons" gimmick -- all-time totals, not scoped to any timeframe
// preset, since "you've run to the moon" only means something as a running lifetime total.
const MARATHON_KM = 42.195
const MOON_DISTANCE_KM = 384_400
const VIENNA_RING_KM = 5.3
const EVEREST_HEIGHT_M = 8_849

function formatHoursMinutes(totalMinutes: number): string {
  const h = Math.floor(totalMinutes / 60)
  const m = Math.round(totalMinutes % 60)
  return h > 0 ? `${h}h ${m}min` : `${m}min`
}

function buildFunFacts(funFacts: FunFactsDto, t: (key: TranslationKey, params?: Record<string, string | number>) => string): string[] {
  const km = funFacts.totalDistanceMeters / 1000
  const facts: string[] = []
  if (km > 0) {
    facts.push(t('dashboard.funFactMarathons', { count: (km / MARATHON_KM).toFixed(1) }))
    facts.push(t('dashboard.funFactMoon', { percent: (km / MOON_DISTANCE_KM) * 100 < 0.01 ? '<0.01' : ((km / MOON_DISTANCE_KM) * 100).toFixed(2) }))
    facts.push(t('dashboard.funFactViennaRing', { count: (km / VIENNA_RING_KM).toFixed(1) }))
  }
  if (funFacts.totalElevationGainMeters > 0) {
    facts.push(t('dashboard.funFactEverest', { count: (funFacts.totalElevationGainMeters / EVEREST_HEIGHT_M).toFixed(1) }))
  }
  return facts
}

export function DashboardPage() {
  const { language, t } = useLanguage()
  const [preset, setPreset] = useState<TimeframePreset>('30d')
  const [data, setData] = useState<DashboardOverviewDto | null>(null)
  const [locations, setLocations] = useState<WorkoutLocationDto[]>([])
  const [heatmapDays, setHeatmapDays] = useState<ConsistencyDayDto[]>([])
  const [funFacts, setFunFacts] = useState<FunFactsDto | null>(null)
  const [flashback, setFlashback] = useState<FlashbackDto | null>(null)
  const [weeklyDigest, setWeeklyDigest] = useState<WeeklyDigestDto | null>(null)
  const [loading, setLoading] = useState(true)

  const numberFmt = useMemo(() => new Intl.NumberFormat(language === 'de' ? 'de-AT' : 'en-US', { maximumFractionDigits: 0 }), [language])

  const PRESETS: { value: TimeframePreset; label: string }[] = [
    { value: '7d', label: t('preset.7d') },
    { value: '30d', label: t('preset.30d') },
    { value: '1y', label: t('preset.1y') },
    { value: 'all', label: t('preset.all') },
  ]

  useEffect(() => {
    setLoading(true)
    api
      .dashboardOverview({ preset, lang: language })
      .then(setData)
      .finally(() => setLoading(false))
  }, [preset, language])

  useEffect(() => {
    api.dashboardWorkoutLocations({ preset }).then(setLocations)
  }, [preset])

  // Independent of the timeframe preset -- a consistency calendar only makes sense over a fixed,
  // always-comparable trailing year, so it's fetched once rather than reacting to `preset`.
  useEffect(() => {
    api.consistencyHeatmap(365).then(setHeatmapDays)
    api.funFacts().then(setFunFacts)
    api.flashback().then(setFlashback)
    api.weeklyDigest().then(setWeeklyDigest)
  }, [])

  const hasData = data && data.daysWithData > 0

  const chartGranularity: ChartGranularity = preset === '1y' ? 'week' : preset === 'all' ? 'month' : 'daily'
  const chartData = useMemo(() => {
    if (!data) return []
    return chartGranularity === 'daily'
      ? data.days.map((d) => ({ date: d.date, steps: d.steps ?? 0, restingHeartRateBpm: d.restingHeartRateBpm, sleepScore: d.sleepScore }))
      : aggregateDays(data.days, chartGranularity)
  }, [data, chartGranularity])

  const hasCrossMetricData = useMemo(() => chartData.some((d) => d.restingHeartRateBpm != null || d.sleepScore != null), [chartData])

  return (
    <div>
      <TopAppBar title={t('nav.dashboard')}>
        <div className="ghl-dashboard__timeframe">
          <SegmentedButton options={PRESETS} value={preset} onChange={setPreset} />
        </div>
      </TopAppBar>

      <div className="ghl-page-content">
        {!loading && !hasData && (
          <Surface tone="low">
            <p>{t('dashboard.noData')}</p>
          </Surface>
        )}

        {hasData && data && (
          <>
            {(data.insights.length > 0 || flashback?.hasFlashback) && (
              <div className="ghl-insights">
                {data.insights.map((insight, i) => (
                  <Surface key={i} className="ghl-insight-card">
                    <Icon name="trophy" size={18} />
                    <span>{insight}</span>
                  </Surface>
                ))}
                {flashback?.hasFlashback && (
                  <Surface className="ghl-insight-card">
                    <Icon name="moon" size={18} />
                    <span>
                      {t('dashboard.flashback', {
                        years: flashback.yearsAgo ?? 0,
                        date: formatDate(flashback.workoutStartUtc ?? ''),
                        activity: flashback.activityName ?? '',
                        detail:
                          flashback.distanceMeters != null
                            ? formatDistanceKm(flashback.distanceMeters)
                            : flashback.durationSeconds != null
                              ? formatDuration(flashback.durationSeconds)
                              : '',
                      })}
                    </span>
                  </Surface>
                )}
              </div>
            )}

            <div className="ghl-kpi-row">
              <KpiTile label={t('dashboard.totalSteps')} value={numberFmt.format(data.totalSteps)} icon={<Icon name="workouts" size={20} />} />
              <KpiTile label={t('dashboard.avgStepsPerDay')} value={numberFmt.format(data.avgStepsPerDay)} icon={<Icon name="dashboard" size={20} />} />
              <KpiTile label={t('dashboard.totalDistance')} value={formatDistanceKm(data.totalDistanceMeters)} icon={<Icon name="route" size={20} />} />
              <KpiTile
                label={t('dashboard.avgActiveMinutes')}
                value={numberFmt.format(data.avgActiveMinutesPerDay)}
                unit="min"
                icon={<Icon name="recovery" size={20} />}
              />
              <KpiTile label={t('dashboard.workouts')} value={data.workoutsInRange.toString()} icon={<Icon name="workouts" size={20} />} />
              {data.avgSleepScore != null && (
                <KpiTile label={t('dashboard.avgSleepScore')} value={Math.round(data.avgSleepScore).toString()} icon={<Icon name="sleep" size={20} />} />
              )}
              {data.avgRestingHeartRate != null && (
                <KpiTile label={t('dashboard.avgRestingHr')} value={Math.round(data.avgRestingHeartRate).toString()} unit="bpm" icon={<Icon name="heart" size={20} />} />
              )}
            </div>

            {weeklyDigest && weeklyDigest.workoutsCount + weeklyDigest.activeDaysCount > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">{t('dashboard.weeklyDigestTitle')}</h2>
                <p className="ghl-chart-card__hint">
                  {formatDate(weeklyDigest.from)} – {formatDate(weeklyDigest.to)}
                </p>
                <div className="ghl-kpi-row">
                  <KpiTile label={t('dashboard.totalDistance')} value={formatDistanceKm(weeklyDigest.totalDistanceMeters)} />
                  <KpiTile label={t('dashboard.workouts')} value={weeklyDigest.workoutsCount.toString()} />
                  <KpiTile label={t('dashboard.activeDays')} value={t('dashboard.activeDaysValue', { count: weeklyDigest.activeDaysCount })} />
                  {weeklyDigest.avgSleepScore != null && (
                    <KpiTile label={t('dashboard.avgSleepScore')} value={Math.round(weeklyDigest.avgSleepScore).toString()} />
                  )}
                  <KpiTile
                    label={t('dashboard.sleepDebt')}
                    value={weeklyDigest.sleepDebtMinutes > 0 ? formatHoursMinutes(weeklyDigest.sleepDebtMinutes) : t('dashboard.sleepDebtNone')}
                  />
                  {weeklyDigest.avgRestingHeartRate != null && (
                    <KpiTile
                      label={t('dashboard.avgRestingHr')}
                      value={Math.round(weeklyDigest.avgRestingHeartRate).toString()}
                      unit={weeklyDigest.restingHrDeltaFromPriorWeek != null ? `bpm (${weeklyDigest.restingHrDeltaFromPriorWeek >= 0 ? '+' : ''}${weeklyDigest.restingHrDeltaFromPriorWeek.toFixed(1)})` : 'bpm'}
                    />
                  )}
                </div>
              </Surface>
            )}

            {data.activityBreakdown.length > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">{t('dashboard.activityBreakdown')}</h2>
                <div className="ghl-kpi-row">
                  {data.activityBreakdown.map((c) => (
                    <KpiTile key={c.category} label={t(CATEGORY_LABEL_KEYS[c.category] ?? 'category.other')} value={c.count.toString()} />
                  ))}
                </div>
              </Surface>
            )}

            {heatmapDays.length > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">{t('dashboard.consistency')}</h2>
                <ConsistencyHeatmap days={heatmapDays} language={language} stepsLabel={t('dashboard.stepsUnit')} />
              </Surface>
            )}

            {locations.length > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">{t('dashboard.trainingLocations')}</h2>
                <p className="ghl-chart-card__hint">{t('dashboard.trainingLocationsHint')}</p>
                <TrainingLocationsMap locations={locations} countLabel={(count) => t('dashboard.locationTooltip', { count: String(count) })} />
              </Surface>
            )}

            <Surface tone="low" className="ghl-chart-card">
              <h2 className="ghl-chart-card__title">
                {t('dashboard.stepsOverTime')}
                {chartGranularity !== 'daily' && (
                  <span className="ghl-chart-card__subtitle"> — {chartGranularity === 'week' ? t('dashboard.perWeek') : t('dashboard.perMonth')}</span>
                )}
              </h2>
              <ResponsiveContainer width="100%" height={280}>
                {chartGranularity === 'daily' ? (
                  <AreaChart data={chartData}>
                    <defs>
                      <linearGradient id="stepsGradient" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="var(--md-sys-color-primary)" stopOpacity={0.35} />
                        <stop offset="100%" stopColor="var(--md-sys-color-primary)" stopOpacity={0} />
                      </linearGradient>
                    </defs>
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d) => formatBucketLabel(d, chartGranularity)} stroke="var(--md-sys-color-outline)" />
                    <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={40} />
                    <Tooltip contentStyle={{ background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }} />
                    <Area type="monotone" dataKey="steps" stroke="var(--md-sys-color-primary)" fill="url(#stepsGradient)" strokeWidth={2} />
                  </AreaChart>
                ) : (
                  <BarChart data={chartData}>
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d) => formatBucketLabel(d, chartGranularity)} stroke="var(--md-sys-color-outline)" />
                    <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={44} />
                    <Tooltip
                      contentStyle={{ background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }}
                      formatter={(v) => numberFmt.format(Number(v))}
                    />
                    <Bar dataKey="steps" fill="var(--md-sys-color-primary)" radius={[4, 4, 0, 0]} />
                  </BarChart>
                )}
              </ResponsiveContainer>
            </Surface>

            {hasCrossMetricData && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">{t('dashboard.rhrSleepOverTime')}</h2>
                <ResponsiveContainer width="100%" height={260}>
                  <ComposedChart data={chartData}>
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d) => formatBucketLabel(d, chartGranularity)} stroke="var(--md-sys-color-outline)" />
                    <YAxis yAxisId="rhr" tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={36} domain={['auto', 'auto']} />
                    <YAxis yAxisId="sleep" orientation="right" tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={36} domain={[0, 100]} />
                    <Tooltip contentStyle={{ background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }} />
                    <Legend wrapperStyle={{ fontSize: 12 }} />
                    <Line yAxisId="rhr" type="monotone" dataKey="restingHeartRateBpm" name={t('chart.restingHr')} stroke="#e53935" dot={false} strokeWidth={2} connectNulls />
                    <Line yAxisId="sleep" type="monotone" dataKey="sleepScore" name={t('chart.sleepScore')} stroke="#3949ab" dot={false} strokeWidth={2} connectNulls />
                  </ComposedChart>
                </ResponsiveContainer>
              </Surface>
            )}

            {funFacts && funFacts.totalWorkouts > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">{t('dashboard.funFactsTitle')}</h2>
                <ul className="ghl-fun-facts">
                  {buildFunFacts(funFacts, t).map((fact, i) => (
                    <li key={i}>{fact}</li>
                  ))}
                </ul>
              </Surface>
            )}
          </>
        )}
      </div>
    </div>
  )
}
