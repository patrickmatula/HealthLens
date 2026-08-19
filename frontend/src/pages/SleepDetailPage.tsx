import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api/client'
import type { SleepSessionDetailDto } from '../api/types'
import { KpiTile } from '../components/KpiTile'
import { SleepHypnogram } from '../components/SleepHypnogram'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { formatDateTime, formatMinutes } from '../utils/format'
import './WorkoutDetailPage.css'

export function SleepDetailPage() {
  const { id } = useParams<{ id: string }>()
  const [session, setSession] = useState<SleepSessionDetailDto | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!id) return
    setLoading(true)
    api
      .sleepDetail(id)
      .then(setSession)
      .finally(() => setLoading(false))
  }, [id])

  if (loading) {
    return (
      <div>
        <TopAppBar title="Schlaf" />
      </div>
    )
  }

  if (!session) {
    return (
      <div>
        <TopAppBar title="Schlaf" />
        <div className="ghl-page-content">
          <Surface tone="low">
            <p>Nacht nicht gefunden.</p>
          </Surface>
        </div>
      </div>
    )
  }

  return (
    <div>
      <TopAppBar title="Schlaf" actions={<Link to="/sleep" className="ghl-back-link">Alle Nächte</Link>} />

      <div className="ghl-page-content">
        <Surface tone="low" className="ghl-workout-header">
          <div>
            {formatDateTime(session.startUtc)} – {new Date(session.endUtc + 'Z').toLocaleTimeString('de-AT', { hour: '2-digit', minute: '2-digit' })}
          </div>
          <div className="ghl-workout-header__stats">
            <KpiTile label="Schlafdauer" value={formatMinutes(session.minutesAsleep)} />
            <KpiTile label="Zeit im Bett" value={formatMinutes(session.timeInBedMinutes)} />
            <KpiTile label="Wachphasen" value={formatMinutes(session.minutesAwake)} />
            {session.efficiencyPercent != null && <KpiTile label="Effizienz" value={Math.round(session.efficiencyPercent).toString()} unit="%" />}
            {session.score != null && <KpiTile label="Schlaf-Score" value={Math.round(session.score.overallScore).toString()} />}
            {session.score?.restingHeartRate != null && <KpiTile label="Ruhepuls" value={Math.round(session.score.restingHeartRate).toString()} unit="bpm" />}
          </div>
        </Surface>

        {session.stages.length > 0 && (
          <Surface tone="low">
            <h2 className="ghl-section-title">Schlafphasen</h2>
            <SleepHypnogram stages={session.stages} startUtc={session.startUtc} endUtc={session.endUtc} />
          </Surface>
        )}

        {session.score != null && (
          <Surface tone="low">
            <h2 className="ghl-section-title">Score-Details</h2>
            <div className="ghl-kpi-row">
              <KpiTile label="Tiefschlaf" value={formatMinutes(session.score.deepSleepMinutes)} />
              <KpiTile label="REM-Anteil" value={Math.round(session.score.remSleepPercent).toString()} unit="%" />
              {session.score.restlessnessNormalized != null && (
                <KpiTile label="Unruhe" value={(session.score.restlessnessNormalized * 100).toFixed(1)} unit="%" />
              )}
            </div>
          </Surface>
        )}
      </div>
    </div>
  )
}
