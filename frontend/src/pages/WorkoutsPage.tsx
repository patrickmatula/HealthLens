import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Scatter, ScatterChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis, ZAxis } from 'recharts'
import { api } from '../api/client'
import type { PersonalRecordDto, ShoeDto, TimeframePreset, WorkoutListItemDto } from '../api/types'
import { Icon } from '../components/Icon'
import { Leaderboard, type LeaderboardEntry } from '../components/Leaderboard'
import { PersonalRecordCard } from '../components/PersonalRecordCard'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { useLanguage, type TranslationKey } from '../i18n/LanguageContext'
import { useShoesFeature } from '../shoes/ShoesFeatureContext'
import { categorizeWorkout, formatDate, formatDateTime, formatDistanceKm, formatDuration, formatPace, type WorkoutCategory } from '../utils/format'
import { predictRaceTimes } from '../utils/runningMetrics'
import './WorkoutsPage.css'
import './DashboardPage.css'

const tooltipStyle = { background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }

const PR_ORDER = [
  'FASTEST_KILOMETRE_NAME',
  'FASTEST_MILE_NAME',
  'FASTEST_2_KILOMETRES_NAME',
  'FASTEST_5K_NAME',
  'FASTEST_10K_NAME',
  'FASTEST_2_MILES_NAME',
  'FARTHEST_RUN_NAME',
]

const INSIGHTS_STORAGE_KEY = 'ghl-workouts-show-insights'

export function WorkoutsPage() {
  const { language, t } = useLanguage()
  const { enabled: shoesEnabled } = useShoesFeature()
  const [preset, setPreset] = useState<TimeframePreset>('all')
  const [workouts, setWorkouts] = useState<WorkoutListItemDto[]>([])
  const [records, setRecords] = useState<PersonalRecordDto[]>([])
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState<WorkoutCategory | 'Alle'>('Alle')
  const [showInsights, setShowInsights] = useState(() => localStorage.getItem(INSIGHTS_STORAGE_KEY) === '1')
  const [shoes, setShoes] = useState<ShoeDto[]>([])
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [assignShoeId, setAssignShoeId] = useState<string>('')
  const [assigning, setAssigning] = useState(false)

  const PRESETS: { value: TimeframePreset; label: string }[] = [
    { value: '30d', label: t('preset.30d') },
    { value: '1y', label: t('preset.1y') },
    { value: 'all', label: t('preset.all') },
  ]

  const CATEGORIES: { value: WorkoutCategory | 'Alle'; label: string }[] = [
    { value: 'Alle', label: t('category.all') },
    { value: 'Lauf', label: t('category.run') },
    { value: 'Spaziergang', label: t('category.walk') },
    { value: 'Rad', label: t('category.bike') },
    { value: 'Kraft', label: t('category.strength') },
    { value: 'Sonstiges', label: t('category.other') },
  ]

  useEffect(() => {
    localStorage.setItem(INSIGHTS_STORAGE_KEY, showInsights ? '1' : '0')
  }, [showInsights])

  useEffect(() => {
    setLoading(true)
    Promise.all([api.workouts({ preset }), api.bestPersonalRecords()])
      .then(([w, r]) => {
        setWorkouts(w)
        setRecords(r)
      })
      .finally(() => setLoading(false))
  }, [preset])

  useEffect(() => {
    if (shoesEnabled) {
      api.shoes().then(setShoes)
    } else {
      setSelectedIds(new Set())
    }
  }, [shoesEnabled])

  function toggleSelect(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  function selectAllFiltered() {
    setSelectedIds(new Set(filteredWorkouts.map((w) => w.id)))
  }

  function clearSelection() {
    setSelectedIds(new Set())
  }

  async function applyShoeAssignment() {
    setAssigning(true)
    try {
      const shoeId = assignShoeId === '' ? null : Number(assignShoeId)
      await api.assignShoe(shoeId, [...selectedIds])
      const updated = await api.workouts({ preset })
      setWorkouts(updated)
      const refreshedShoes = await api.shoes()
      setShoes(refreshedShoes)
      clearSelection()
    } finally {
      setAssigning(false)
    }
  }

  const bestRecords = PR_ORDER.map((name) => records.find((r) => r.nameLocalizationId === name)).filter((r): r is PersonalRecordDto => r != null)
  const racePrediction = useMemo(() => predictRaceTimes(records), [records])

  const leaderboards = useMemo(() => {
    const fastest5k: LeaderboardEntry[] = workouts
      .filter((w) => categorizeWorkout(w) === 'Lauf' && w.distanceMeters != null && w.distanceMeters >= 4900 && w.distanceMeters <= 5100)
      .sort((a, b) => a.durationSeconds - b.durationSeconds)
      .slice(0, 5)
      .map((w) => ({ workout: w, value: formatDuration(w.durationSeconds) }))

    const longest: LeaderboardEntry[] = workouts
      .filter((w) => w.distanceMeters != null)
      .sort((a, b) => (b.distanceMeters ?? 0) - (a.distanceMeters ?? 0))
      .slice(0, 5)
      .map((w) => ({ workout: w, value: formatDistanceKm(w.distanceMeters) }))

    const mostCalories: LeaderboardEntry[] = workouts
      .filter((w) => w.calories != null)
      .sort((a, b) => (b.calories ?? 0) - (a.calories ?? 0))
      .slice(0, 5)
      .map((w) => ({ workout: w, value: `${Math.round(w.calories!)} kcal` }))

    return { fastest5k, longest, mostCalories }
  }, [workouts])

  // Pace tends to be noisy run-to-run (terrain, effort, distance), but the trend across many runs
  // answers a question the official app doesn't: am I actually getting faster over time?
  const paceTrend = useMemo(
    () =>
      workouts
        .filter((w) => categorizeWorkout(w) === 'Lauf' && w.avgPaceSecPerKm != null && w.distanceMeters != null && w.distanceMeters >= 1500)
        .map((w) => ({ dateMs: new Date(`${w.startUtc}Z`).getTime(), pace: w.avgPaceSecPerKm!, label: formatDate(w.startUtc) }))
        .sort((a, b) => a.dateMs - b.dateMs),
    [workouts],
  )

  const filteredWorkouts = useMemo(() => {
    const q = search.trim().toLowerCase()
    return workouts.filter((w) => {
      if (category !== 'Alle' && categorizeWorkout(w) !== category) return false
      if (q && !w.activityName.toLowerCase().includes(q) && !formatDateTime(w.startUtc).toLowerCase().includes(q)) return false
      return true
    })
  }, [workouts, search, category])

  return (
    <div>
      <TopAppBar title={t('nav.workouts')}>
        <SegmentedButton options={PRESETS} value={preset} onChange={setPreset} />
      </TopAppBar>

      <div className="ghl-page-content">
        {bestRecords.length > 0 && (
          <section>
            <h2 className="ghl-section-title">{t('workouts.bestRecords')}</h2>
            <div className="ghl-pr-grid">
              {bestRecords.map((r) => (
                <PersonalRecordCard key={r.nameLocalizationId} record={r} />
              ))}
            </div>
          </section>
        )}

        {racePrediction && (
          <section>
            <h2 className="ghl-section-title">{t('workouts.racePrediction')}</h2>
            <p className="ghl-chart-card__hint">
              {t('workouts.racePredictionHint', { distance: formatDistanceKm(racePrediction.anchorMeters), time: formatDuration(racePrediction.anchorSeconds) })}
            </p>
            <div className="ghl-pr-grid">
              {racePrediction.predictions.map((p) => (
                <Surface key={p.key} tone="low" padded className="ghl-pr-card ghl-pr-card__surface">
                  <div className="ghl-pr-card__header">
                    <Icon name="route" size={18} />
                    <span className="ghl-pr-card__label">{t(`workouts.raceDistance.${p.key}` as TranslationKey)}</span>
                  </div>
                  <div className="ghl-pr-card__value">{formatDuration(p.seconds)}</div>
                </Surface>
              ))}
            </div>
          </section>
        )}

        {(leaderboards.fastest5k.length > 0 || leaderboards.longest.length > 0 || leaderboards.mostCalories.length > 0 || paceTrend.length >= 5) && (
          <section>
            <button
              type="button"
              className="ghl-insights-toggle"
              onClick={() => setShowInsights((v) => !v)}
              aria-expanded={showInsights}
            >
              <span>{t('workouts.insightsToggle')}</span>
              <Icon name="chevronRight" size={18} />
            </button>

            {showInsights && (
              <div className="ghl-insights-body">
                {(leaderboards.fastest5k.length > 0 || leaderboards.longest.length > 0 || leaderboards.mostCalories.length > 0) && (
                  <div className="ghl-leaderboard-grid">
                    <Leaderboard title={t('workouts.leaderboardFastest5k')} icon="route" entries={leaderboards.fastest5k} />
                    <Leaderboard title={t('workouts.leaderboardLongest')} icon="workouts" entries={leaderboards.longest} />
                    <Leaderboard title={t('workouts.leaderboardMostCalories')} icon="heart" entries={leaderboards.mostCalories} />
                  </div>
                )}

                {paceTrend.length >= 5 && (
                  <Surface tone="low" className="ghl-chart-card">
                    <h2 className="ghl-chart-card__title">{t('workouts.paceTrendTitle')}</h2>
                    <p className="ghl-chart-card__hint">{t('workouts.paceTrendHint')}</p>
                    <ResponsiveContainer width="100%" height={240}>
                      <ScatterChart>
                        <CartesianGrid stroke="var(--md-sys-color-outline-variant)" />
                        <XAxis
                          dataKey="dateMs"
                          type="number"
                          domain={['dataMin', 'dataMax']}
                          tickFormatter={(v: number) =>
                            new Date(v).toLocaleDateString(language === 'de' ? 'de-AT' : 'en-US', { month: '2-digit', year: '2-digit' })
                          }
                          tick={{ fontSize: 11 }}
                          stroke="var(--md-sys-color-outline)"
                        />
                        <YAxis dataKey="pace" reversed tickFormatter={(v: number) => formatPace(v)} tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={56} />
                        <ZAxis range={[50, 50]} />
                        <Tooltip contentStyle={tooltipStyle} formatter={(v) => formatPace(Number(v))} labelFormatter={() => ''} />
                        <Scatter data={paceTrend} fill="#1e88e5" />
                      </ScatterChart>
                    </ResponsiveContainer>
                  </Surface>
                )}
              </div>
            )}
          </section>
        )}

        <section>
          <div className="ghl-workout-filters">
            <h2 className="ghl-section-title">{t('workouts.allWorkouts')}</h2>
            <div className="ghl-workout-filters__row">
              <label className="ghl-search-bar">
                <Icon name="search" size={20} />
                <input
                  type="text"
                  className="ghl-search-bar__input"
                  placeholder={t('workouts.searchPlaceholder')}
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
                {search && (
                  <button type="button" className="ghl-search-bar__clear" onClick={() => setSearch('')} aria-label={t('workouts.clearSearchLabel')}>
                    <Icon name="close" size={16} />
                  </button>
                )}
              </label>
              <SegmentedButton options={CATEGORIES} value={category} onChange={setCategory} />
            </div>
          </div>

          {shoesEnabled && filteredWorkouts.length > 0 && (
            <div className="ghl-shoe-select-bar">
              <button type="button" className="ghl-shoe-select-all" onClick={selectedIds.size > 0 ? clearSelection : selectAllFiltered}>
                {selectedIds.size > 0 ? t('shoes.clearSelection', { count: selectedIds.size }) : t('shoes.selectAllFiltered')}
              </button>

              {selectedIds.size > 0 && (
                <div className="ghl-shoe-select-bar__actions">
                  <select
                    className="ghl-shoe-select-bar__picker"
                    value={assignShoeId}
                    onChange={(e) => setAssignShoeId(e.target.value)}
                  >
                    <option value="">{t('shoes.noneOption')}</option>
                    {shoes.map((shoe) => (
                      <option key={shoe.id} value={shoe.id}>
                        {shoe.name}
                      </option>
                    ))}
                  </select>
                  <md-filled-button disabled={assigning || undefined} onClick={applyShoeAssignment}>
                    {t('shoes.assignButton')}
                  </md-filled-button>
                </div>
              )}
            </div>
          )}

          {!loading && workouts.length === 0 && (
            <Surface tone="low">
              <p>{t('workouts.emptyRange')}</p>
            </Surface>
          )}

          {!loading && workouts.length > 0 && filteredWorkouts.length === 0 && (
            <Surface tone="low">
              <p>{t('workouts.emptyFilter')}</p>
            </Surface>
          )}

          <div className="ghl-workout-list">
            {filteredWorkouts.map((w) => (
              <div key={w.id} className="ghl-workout-row-wrap">
                {shoesEnabled && (
                  <input
                    type="checkbox"
                    className="ghl-workout-row__checkbox"
                    checked={selectedIds.has(w.id)}
                    onChange={() => toggleSelect(w.id)}
                    aria-label={w.activityName}
                  />
                )}
                <Link to={`/workouts/${w.id}`} className="ghl-workout-row">
                  <Surface tone="low" className="ghl-workout-row__surface">
                    <div className="ghl-workout-row__icon">
                      <Icon name={w.hasGps ? 'route' : 'workouts'} />
                    </div>
                    <div className="ghl-workout-row__main">
                      <div className="ghl-workout-row__title">{w.activityName}</div>
                      <div className="ghl-workout-row__date">{formatDateTime(w.startUtc)}</div>
                      {shoesEnabled && w.shoeName && (
                        <div className="ghl-workout-row__shoe">
                          <Icon name="shoe" size={12} /> {w.shoeName}
                        </div>
                      )}
                    </div>
                    <div className="ghl-workout-row__stats">
                      <span>{formatDuration(w.durationSeconds)}</span>
                      {w.distanceMeters != null && <span>{formatDistanceKm(w.distanceMeters)}</span>}
                      {w.avgPaceSecPerKm != null && <span>{formatPace(w.avgPaceSecPerKm)}</span>}
                      {w.avgHeartRate != null && <span>{Math.round(w.avgHeartRate)} bpm</span>}
                    </div>
                  </Surface>
                </Link>
              </div>
            ))}
          </div>
        </section>
      </div>
    </div>
  )
}
