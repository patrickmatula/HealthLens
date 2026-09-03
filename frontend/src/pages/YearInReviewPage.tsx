import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { YearInReviewDto } from '../api/types'
import { Icon } from '../components/Icon'
import { KpiTile } from '../components/KpiTile'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { useLanguage } from '../i18n/LanguageContext'
import { formatDistanceKm } from '../utils/format'
import './DashboardPage.css'
import './YearInReviewPage.css'

const CATEGORY_LABEL_KEYS: Record<string, 'category.run' | 'category.walk' | 'category.bike' | 'category.strength' | 'category.other'> = {
  Lauf: 'category.run',
  Spaziergang: 'category.walk',
  Rad: 'category.bike',
  Kraft: 'category.strength',
  Sonstiges: 'category.other',
}

export function YearInReviewPage() {
  const { language, t } = useLanguage()
  const [year, setYear] = useState<number | null>(null)
  const [data, setData] = useState<YearInReviewDto | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    api
      .yearInReview(year ?? undefined)
      .then((d) => {
        setData(d)
        if (year === null) setYear(d.year)
      })
      .finally(() => setLoading(false))
  }, [year])

  const numberFmt = new Intl.NumberFormat(language === 'de' ? 'de-AT' : 'en-US', { maximumFractionDigits: 0 })
  const monthFmt = new Intl.DateTimeFormat(language === 'de' ? 'de-AT' : 'en-US', { month: 'long' })

  const years = data ? Array.from({ length: data.latestYear - data.earliestYear + 1 }, (_, i) => data.earliestYear + i).reverse() : []

  const distanceDeltaPercent =
    data?.priorYearDistanceMeters != null && data.priorYearDistanceMeters > 0
      ? ((data.totalDistanceMeters - data.priorYearDistanceMeters) / data.priorYearDistanceMeters) * 100
      : null

  return (
    <div>
      <TopAppBar title={t('yearReview.title')} actions={<Link to="/more" className="ghl-back-link">{t('shoes.backToSettings')}</Link>} />

      <div className="ghl-page-content">
        {years.length > 1 && (
          <div className="ghl-year-picker">
            {years.map((y) => (
              <button key={y} type="button" className={`ghl-year-picker__chip ${y === year ? 'ghl-year-picker__chip--active' : ''}`} onClick={() => setYear(y)}>
                {y}
              </button>
            ))}
          </div>
        )}

        {!loading && data && !data.hasData && (
          <Surface tone="low">
            <p>{t('yearReview.noData')}</p>
          </Surface>
        )}

        {data && data.hasData && (
          <>
            <Surface tone="low" className="ghl-year-hero">
              <div className="ghl-year-hero__year">{data.year}</div>
              <div className="ghl-year-hero__distance">{formatDistanceKm(data.totalDistanceMeters)}</div>
              {distanceDeltaPercent != null && (
                <div className="ghl-year-hero__delta">
                  {t('yearReview.vsLastYear', { percent: `${distanceDeltaPercent >= 0 ? '+' : ''}${distanceDeltaPercent.toFixed(0)}` })}
                </div>
              )}
            </Surface>

            <div className="ghl-kpi-row">
              <KpiTile label={t('dashboard.workouts')} value={data.totalWorkouts.toString()} icon={<Icon name="workouts" size={20} />} />
              <KpiTile label={t('dashboard.activeDays')} value={data.activeDays.toString()} icon={<Icon name="dashboard" size={20} />} />
              {data.longestRunMeters != null && (
                <KpiTile label={t('yearReview.longestRun')} value={formatDistanceKm(data.longestRunMeters)} icon={<Icon name="route" size={20} />} />
              )}
              <KpiTile label={t('detail.elevation')} value={numberFmt.format(data.totalElevationGainMeters)} unit="m" icon={<Icon name="recovery" size={20} />} />
              {data.bestMonth != null && (
                <KpiTile
                  label={t('yearReview.bestMonth')}
                  value={monthFmt.format(new Date(Date.UTC(2000, data.bestMonth - 1, 1)))}
                  unit={t('yearReview.bestMonthUnit', { count: data.bestMonthWorkouts })}
                  icon={<Icon name="trophy" size={20} />}
                />
              )}
            </div>

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
          </>
        )}
      </div>
    </div>
  )
}
