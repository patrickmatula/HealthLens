import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api/client'
import type { SleepSessionDetailDto } from '../api/types'
import { KpiTile } from '../components/KpiTile'
import { SleepHypnogram } from '../components/SleepHypnogram'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { useLanguage } from '../i18n/LanguageContext'
import { formatDateTime, formatMinutes } from '../utils/format'
import './WorkoutDetailPage.css'

export function SleepDetailPage() {
  const { language, t } = useLanguage()
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
        <TopAppBar title={t('nav.sleep')} />
      </div>
    )
  }

  if (!session) {
    return (
      <div>
        <TopAppBar title={t('nav.sleep')} />
        <div className="ghl-page-content">
          <Surface tone="low">
            <p>{t('sleep.notFound')}</p>
          </Surface>
        </div>
      </div>
    )
  }

  return (
    <div>
      <TopAppBar title={t('nav.sleep')} actions={<Link to="/sleep" className="ghl-back-link">{t('sleep.backToNights')}</Link>} />

      <div className="ghl-page-content">
        <Surface tone="low" className="ghl-workout-header">
          <div>
            {formatDateTime(session.startUtc)} –{' '}
            {new Date(session.endUtc + 'Z').toLocaleTimeString(language === 'de' ? 'de-AT' : 'en-US', { hour: '2-digit', minute: '2-digit' })}
          </div>
          <div className="ghl-workout-header__stats">
            <KpiTile label={t('sleep.duration')} value={formatMinutes(session.minutesAsleep)} />
            <KpiTile label={t('sleep.timeInBed')} value={formatMinutes(session.timeInBedMinutes)} />
            <KpiTile label={t('sleep.minutesAwake')} value={formatMinutes(session.minutesAwake)} />
            {session.efficiencyPercent != null && <KpiTile label={t('sleep.efficiency')} value={Math.round(session.efficiencyPercent).toString()} unit="%" />}
            {session.score != null && <KpiTile label={t('sleep.score')} value={Math.round(session.score.overallScore).toString()} />}
            {session.score?.restingHeartRate != null && (
              <KpiTile label={t('sleep.restingHr')} value={Math.round(session.score.restingHeartRate).toString()} unit="bpm" />
            )}
          </div>
        </Surface>

        {session.stages.length > 0 && (
          <Surface tone="low">
            <h2 className="ghl-section-title">{t('sleep.stages')}</h2>
            <SleepHypnogram stages={session.stages} startUtc={session.startUtc} endUtc={session.endUtc} />
          </Surface>
        )}

        {session.score != null && (
          <Surface tone="low">
            <h2 className="ghl-section-title">{t('sleep.scoreDetails')}</h2>
            <div className="ghl-kpi-row">
              <KpiTile label={t('sleep.deepSleep')} value={formatMinutes(session.score.deepSleepMinutes)} />
              <KpiTile label={t('sleep.remPercent')} value={Math.round(session.score.remSleepPercent).toString()} unit="%" />
              {session.score.restlessnessNormalized != null && (
                <KpiTile label={t('sleep.restlessness')} value={(session.score.restlessnessNormalized * 100).toFixed(1)} unit="%" />
              )}
            </div>
          </Surface>
        )}
      </div>
    </div>
  )
}
