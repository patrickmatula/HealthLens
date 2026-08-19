import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { PersonalRecordDto, TimeframePreset, WorkoutListItemDto } from '../api/types'
import { Icon } from '../components/Icon'
import { PersonalRecordCard } from '../components/PersonalRecordCard'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { formatDateTime, formatDistanceKm, formatDuration, formatPace } from '../utils/format'
import './WorkoutsPage.css'

const PRESETS: { value: TimeframePreset; label: string }[] = [
  { value: '30d', label: '30 Tage' },
  { value: '1y', label: '1 Jahr' },
  { value: 'all', label: 'Alle' },
]

const PR_ORDER = [
  'FASTEST_KILOMETRE_NAME',
  'FASTEST_MILE_NAME',
  'FASTEST_2_KILOMETRES_NAME',
  'FASTEST_5K_NAME',
  'FASTEST_10K_NAME',
  'FASTEST_2_MILES_NAME',
  'FARTHEST_RUN_NAME',
]

export function WorkoutsPage() {
  const [preset, setPreset] = useState<TimeframePreset>('all')
  const [workouts, setWorkouts] = useState<WorkoutListItemDto[]>([])
  const [records, setRecords] = useState<PersonalRecordDto[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    Promise.all([api.workouts({ preset }), api.personalRecords()])
      .then(([w, r]) => {
        setWorkouts(w)
        setRecords(r)
      })
      .finally(() => setLoading(false))
  }, [preset])

  const standingRecords = PR_ORDER.map((name) => records.find((r) => r.nameLocalizationId === name && r.state === 'PERSONAL_RECORD_STATE_STANDING'))
    .filter((r): r is PersonalRecordDto => r != null)

  return (
    <div>
      <TopAppBar title="Workouts">
        <SegmentedButton options={PRESETS} value={preset} onChange={setPreset} />
      </TopAppBar>

      <div className="ghl-page-content">
        {standingRecords.length > 0 && (
          <section>
            <h2 className="ghl-section-title">Bestleistungen</h2>
            <div className="ghl-pr-grid">
              {standingRecords.map((r) => (
                <PersonalRecordCard key={r.id} record={r} />
              ))}
            </div>
          </section>
        )}

        <section>
          <h2 className="ghl-section-title">Alle Workouts</h2>
          {!loading && workouts.length === 0 && (
            <Surface tone="low">
              <p>Keine Workouts in diesem Zeitraum.</p>
            </Surface>
          )}

          <div className="ghl-workout-list">
            {workouts.map((w) => (
              <Link key={w.id} to={`/workouts/${w.id}`} className="ghl-workout-row">
                <Surface tone="low" className="ghl-workout-row__surface">
                  <div className="ghl-workout-row__icon">
                    <Icon name={w.hasGps ? 'route' : 'workouts'} />
                  </div>
                  <div className="ghl-workout-row__main">
                    <div className="ghl-workout-row__title">{w.activityName}</div>
                    <div className="ghl-workout-row__date">{formatDateTime(w.startUtc)}</div>
                  </div>
                  <div className="ghl-workout-row__stats">
                    <span>{formatDuration(w.durationSeconds)}</span>
                    {w.distanceMeters != null && <span>{formatDistanceKm(w.distanceMeters)}</span>}
                    {w.avgPaceSecPerKm != null && <span>{formatPace(w.avgPaceSecPerKm)}</span>}
                    {w.avgHeartRate != null && <span>{Math.round(w.avgHeartRate)} bpm</span>}
                  </div>
                </Surface>
              </Link>
            ))}
          </div>
        </section>
      </div>
    </div>
  )
}
