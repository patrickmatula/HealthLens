import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Area, AreaChart, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '../api/client'
import {
  BODY_MEASUREMENT_TYPES,
  BODY_PAIRED_TYPES,
  type BodyMeasurementTypeKey,
  type BodyOverviewDto,
  type BodySideKey,
  type TimeframePreset,
} from '../api/types'
import { useBodyFeature } from '../body/BodyFeatureContext'
import { ReferenceRangeGauge } from '../components/ReferenceRangeGauge'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { useLanguage, type TranslationKey } from '../i18n/LanguageContext'
import {
  BMI_SOURCE,
  BMI_SOURCE_URL,
  BODY_FAT_DOMAIN,
  BODY_FAT_SOURCE,
  BODY_FAT_SOURCE_URL,
  NAVY_BF_SOURCE,
  NAVY_BF_SOURCE_URL,
  WAIST_HIP_DOMAIN,
  WAIST_HIP_SOURCE,
  WAIST_HIP_SOURCE_URL,
  WAIST_SOURCE,
  WAIST_SOURCE_URL,
  WHTR_DOMAIN,
  WHTR_SOURCE,
  WHTR_SOURCE_URL,
  bmiAssessment,
  computeBmi,
  computeNavyBodyFat,
  getBmiDomain,
  getBmiZones,
  getBodyFatZones,
  getWaistDomain,
  getWaistHipZones,
  getWaistZones,
  getWhtrZones,
  type BodySex,
} from '../utils/bodyReferences'
import './DashboardPage.css'
import './BodyPage.css'

const TYPE_META: Record<BodyMeasurementTypeKey, { labelKey: TranslationKey; unit: string; step: string }> = {
  WeightKg: { labelKey: 'body.type.weightKg', unit: 'kg', step: '0.1' },
  BodyFatPercent: { labelKey: 'body.type.bodyFatPercent', unit: '%', step: '0.1' },
  WaistCm: { labelKey: 'body.type.waistCm', unit: 'cm', step: '0.5' },
  HipCm: { labelKey: 'body.type.hipCm', unit: 'cm', step: '0.5' },
  ChestCm: { labelKey: 'body.type.chestCm', unit: 'cm', step: '0.5' },
  NeckCm: { labelKey: 'body.type.neckCm', unit: 'cm', step: '0.5' },
  BicepCm: { labelKey: 'body.type.bicepCm', unit: 'cm', step: '0.5' },
  ThighCm: { labelKey: 'body.type.thighCm', unit: 'cm', step: '0.5' },
  CalfCm: { labelKey: 'body.type.calfCm', unit: 'cm', step: '0.5' },
}

const tooltipStyle = { background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }

function todayIso() {
  return new Date().toISOString().slice(0, 10)
}

function isPaired(type: BodyMeasurementTypeKey): boolean {
  return (BODY_PAIRED_TYPES as readonly string[]).includes(type)
}

function key(type: BodyMeasurementTypeKey, side: BodySideKey): string {
  return `${type}|${side}`
}

interface Point {
  date: string
  value: number
}

export function BodyPage() {
  const { language, t } = useLanguage()
  const { trackedTypes, setTypeTracked } = useBodyFeature()
  const [preset, setPreset] = useState<TimeframePreset>('1y')
  const [overview, setOverview] = useState<BodyOverviewDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [entryDate, setEntryDate] = useState(todayIso)
  const [entryValues, setEntryValues] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  const [heightInput, setHeightInput] = useState('')
  const [sex, setSex] = useState<BodySex | ''>('')

  const PRESETS: { value: TimeframePreset; label: string }[] = [
    { value: '30d', label: t('preset.30d') },
    { value: '1y', label: t('preset.1y') },
    { value: 'all', label: t('preset.all') },
  ]

  function load() {
    setLoading(true)
    api
      .bodyOverview({ preset })
      .then((data) => {
        setOverview(data)
        setHeightInput(data.profile.heightCm != null ? String(data.profile.heightCm) : '')
        setSex(data.profile.sex ?? '')
      })
      .finally(() => setLoading(false))
  }

  useEffect(load, [preset])

  const seriesByKey = useMemo(() => {
    const map = new Map<string, Point[]>()
    for (const m of overview?.measurements ?? []) {
      const k = key(m.type, m.side)
      const list = map.get(k) ?? []
      list.push({ date: m.date, value: m.value })
      map.set(k, list)
    }
    return map
  }, [overview])

  function latest(type: BodyMeasurementTypeKey, side: BodySideKey = 'None'): number | null {
    const series = seriesByKey.get(key(type, side))
    return series && series.length > 0 ? series[series.length - 1].value : null
  }

  async function handleEntrySubmit(e: FormEvent) {
    e.preventDefault()
    const values: { type: BodyMeasurementTypeKey; side: BodySideKey; value: number }[] = []
    for (const [k, raw] of Object.entries(entryValues)) {
      if (!raw || raw.trim() === '') continue
      const parsed = Number(raw)
      if (Number.isNaN(parsed)) continue
      const [type, side] = k.split('|') as [BodyMeasurementTypeKey, BodySideKey]
      values.push({ type, side, value: parsed })
    }

    if (values.length === 0) return

    setSaving(true)
    try {
      await api.submitBodyEntry(entryDate, values)
      setEntryValues({})
      load()
    } finally {
      setSaving(false)
    }
  }

  async function saveProfile() {
    const parsedHeight = heightInput.trim() === '' ? null : Number(heightInput)
    await api.updateBodyProfile(parsedHeight != null && Number.isFinite(parsedHeight) ? parsedHeight : null, sex === '' ? null : sex)
    load()
  }

  const heightCm = overview?.profile.heightCm ?? null
  const weightNow = latest('WeightKg')
  const waistNow = latest('WaistCm')
  const hipNow = latest('HipCm')
  const neckNow = latest('NeckCm')
  const bodyFatNow = latest('BodyFatPercent')

  const showBmi = heightCm != null && weightNow != null
  const showWaist = sex !== '' && waistNow != null
  const showWaistHip = sex !== '' && waistNow != null && hipNow != null
  const showBodyFat = sex !== '' && bodyFatNow != null
  const showWhtr = heightCm != null && waistNow != null
  const navyEstimate =
    bodyFatNow == null && sex !== '' && neckNow != null && waistNow != null && heightCm != null
      ? computeNavyBodyFat(sex as BodySex, neckNow, waistNow, heightCm, hipNow)
      : null
  const hasAssessments = showBmi || showWaist || showWaistHip || showBodyFat || showWhtr || navyEstimate != null

  // History table: every entry, newest first.
  const historyRows = useMemo(() => {
    return [...(overview?.measurements ?? [])].sort((a, b) => (a.date < b.date ? 1 : a.date > b.date ? -1 : 0))
  }, [overview])

  function rowLabel(type: BodyMeasurementTypeKey, side: BodySideKey): string {
    const base = t(TYPE_META[type].labelKey)
    if (side === 'Left') return `${base} (${t('body.sideLeft')})`
    if (side === 'Right') return `${base} (${t('body.sideRight')})`
    return base
  }

  return (
    <div>
      <TopAppBar title={t('body.title')}>
        <SegmentedButton options={PRESETS} value={preset} onChange={setPreset} />
      </TopAppBar>

      <div className="ghl-page-content">
        <Surface tone="low">
          <h2 className="ghl-section-title">{t('body.newEntryTitle')}</h2>
          <form className="ghl-body-entry" onSubmit={handleEntrySubmit}>
            <label className="ghl-body-entry__field">
              <span>{t('body.dateLabel')}</span>
              <input
                type="date"
                className="ghl-body-entry__date-input"
                value={entryDate}
                max={todayIso()}
                onChange={(e) => setEntryDate(e.target.value)}
              />
            </label>

            {trackedTypes.size === 0 && <p className="ghl-more-text">{t('body.noTypesTracked')}</p>}

            {trackedTypes.size > 0 && (
              <div className="ghl-body-entry__grid">
                {[...trackedTypes].map((type) =>
                  isPaired(type) ? (
                    <div key={type} className="ghl-body-entry__paired-field">
                      <span>{t(TYPE_META[type].labelKey)}</span>
                      <div className="ghl-body-entry__paired-inputs">
                        {(['Left', 'Right'] as const).map((side) => (
                          <div key={side} className="ghl-body-entry__input-wrap">
                            <span className="ghl-body-entry__side-label">{t(side === 'Left' ? 'body.sideLeft' : 'body.sideRight')}</span>
                            <input
                              type="number"
                              inputMode="decimal"
                              step={TYPE_META[type].step}
                              value={entryValues[key(type, side)] ?? ''}
                              onChange={(e) => setEntryValues((prev) => ({ ...prev, [key(type, side)]: e.target.value }))}
                            />
                            <span className="ghl-body-entry__unit">{TYPE_META[type].unit}</span>
                          </div>
                        ))}
                      </div>
                    </div>
                  ) : (
                    <label key={type} className="ghl-body-entry__field">
                      <span>{t(TYPE_META[type].labelKey)}</span>
                      <div className="ghl-body-entry__input-wrap">
                        <input
                          type="number"
                          inputMode="decimal"
                          step={TYPE_META[type].step}
                          value={entryValues[key(type, 'None')] ?? ''}
                          onChange={(e) => setEntryValues((prev) => ({ ...prev, [key(type, 'None')]: e.target.value }))}
                        />
                        <span className="ghl-body-entry__unit">{TYPE_META[type].unit}</span>
                      </div>
                    </label>
                  ),
                )}
              </div>
            )}

            <md-filled-button disabled={saving || trackedTypes.size === 0 || undefined} type="submit">
              {t('body.saveEntry')}
            </md-filled-button>
          </form>
        </Surface>

        {hasAssessments && (
          <section>
            <h2 className="ghl-section-title">{t('body.assessmentsTitle')}</h2>
            <div className="ghl-body-gauges">
              {showBmi && (
                <Surface tone="low" className="ghl-chart-card">
                  <h2 className="ghl-chart-card__title">{t('body.bmiTitle')}</h2>
                  <ReferenceRangeGauge
                    value={weightNow!}
                    domain={getBmiDomain(heightCm!)}
                    zones={getBmiZones(heightCm!, language)}
                    unit=""
                    valueLabel={computeBmi(weightNow!, heightCm!).toFixed(1)}
                  />
                  <p className="ghl-chart-card__hint">
                    {bmiAssessment(weightNow!, heightCm!, language)}{' '}
                    <a href={BMI_SOURCE_URL} target="_blank" rel="noreferrer">
                      {BMI_SOURCE}
                    </a>
                  </p>
                </Surface>
              )}

              {showWhtr && (
                <Surface tone="low" className="ghl-chart-card">
                  <h2 className="ghl-chart-card__title">{t('body.whtrTitle')}</h2>
                  <ReferenceRangeGauge
                    value={waistNow! / heightCm!}
                    domain={WHTR_DOMAIN}
                    zones={getWhtrZones(language)}
                    unit=""
                    valueLabel={(waistNow! / heightCm!).toFixed(2)}
                  />
                  <p className="ghl-chart-card__hint">
                    {t('body.whtrHint')}{' '}
                    <a href={WHTR_SOURCE_URL} target="_blank" rel="noreferrer">
                      {WHTR_SOURCE}
                    </a>
                  </p>
                </Surface>
              )}

              {showWaist && (
                <Surface tone="low" className="ghl-chart-card">
                  <h2 className="ghl-chart-card__title">{t('body.waistTitle')}</h2>
                  <ReferenceRangeGauge value={waistNow!} domain={getWaistDomain(sex as BodySex)} zones={getWaistZones(sex as BodySex, language)} unit="cm" />
                  <p className="ghl-chart-card__hint">
                    {t('body.waistHint')}{' '}
                    <a href={WAIST_SOURCE_URL} target="_blank" rel="noreferrer">
                      {WAIST_SOURCE}
                    </a>
                  </p>
                </Surface>
              )}

              {showWaistHip && (
                <Surface tone="low" className="ghl-chart-card">
                  <h2 className="ghl-chart-card__title">{t('body.waistHipTitle')}</h2>
                  <ReferenceRangeGauge
                    value={waistNow! / hipNow!}
                    domain={WAIST_HIP_DOMAIN}
                    zones={getWaistHipZones(sex as BodySex, language)}
                    unit=""
                    valueLabel={(waistNow! / hipNow!).toFixed(2)}
                  />
                  <p className="ghl-chart-card__hint">
                    {t('body.waistHipHint')}{' '}
                    <a href={WAIST_HIP_SOURCE_URL} target="_blank" rel="noreferrer">
                      {WAIST_HIP_SOURCE}
                    </a>
                  </p>
                </Surface>
              )}

              {showBodyFat && (
                <Surface tone="low" className="ghl-chart-card">
                  <h2 className="ghl-chart-card__title">{t('body.bodyFatTitle')}</h2>
                  <ReferenceRangeGauge value={bodyFatNow!} domain={BODY_FAT_DOMAIN} zones={getBodyFatZones(sex as BodySex, language)} unit="%" />
                  <p className="ghl-chart-card__hint">
                    {t('body.bodyFatHint')}{' '}
                    <a href={BODY_FAT_SOURCE_URL} target="_blank" rel="noreferrer">
                      {BODY_FAT_SOURCE}
                    </a>
                  </p>
                </Surface>
              )}

              {navyEstimate != null && (
                <Surface tone="low" className="ghl-chart-card">
                  <h2 className="ghl-chart-card__title">{t('body.navyBfTitle')}</h2>
                  <ReferenceRangeGauge value={navyEstimate} domain={BODY_FAT_DOMAIN} zones={getBodyFatZones(sex as BodySex, language)} unit="%" />
                  <p className="ghl-chart-card__hint">
                    {t('body.navyBfHint')}{' '}
                    <a href={NAVY_BF_SOURCE_URL} target="_blank" rel="noreferrer">
                      {NAVY_BF_SOURCE}
                    </a>
                  </p>
                </Surface>
              )}
            </div>
          </section>
        )}

        {!loading && [...trackedTypes].length > 0 && (
          <section>
            <h2 className="ghl-section-title">{t('body.trendsTitle')}</h2>
            <div className="ghl-body-gauges">
              {[...trackedTypes].map((type) => {
                if (isPaired(type)) {
                  const left = seriesByKey.get(key(type, 'Left')) ?? []
                  const right = seriesByKey.get(key(type, 'Right')) ?? []
                  if (left.length + right.length < 2) return null

                  const dates = [...new Set([...left.map((p) => p.date), ...right.map((p) => p.date)])].sort()
                  const merged = dates.map((date) => ({
                    date,
                    left: left.find((p) => p.date === date)?.value,
                    right: right.find((p) => p.date === date)?.value,
                  }))

                  return (
                    <Surface key={type} tone="low" className="ghl-chart-card">
                      <h2 className="ghl-chart-card__title">{t(TYPE_META[type].labelKey)}</h2>
                      <ResponsiveContainer width="100%" height={200}>
                        <LineChart data={merged}>
                          <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5)} stroke="var(--md-sys-color-outline)" />
                          <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={40} domain={['dataMin - 1', 'dataMax + 1']} />
                          <Tooltip contentStyle={tooltipStyle} formatter={(v) => `${v} ${TYPE_META[type].unit}`} />
                          <Line type="monotone" dataKey="left" name={t('body.sideLeft')} stroke="#1e88e5" dot={{ r: 3 }} strokeWidth={2} connectNulls />
                          <Line type="monotone" dataKey="right" name={t('body.sideRight')} stroke="#e53935" dot={{ r: 3 }} strokeWidth={2} connectNulls />
                        </LineChart>
                      </ResponsiveContainer>
                    </Surface>
                  )
                }

                const series = seriesByKey.get(key(type, 'None'))
                if (!series || series.length < 2) return null

                return (
                  <Surface key={type} tone="low" className="ghl-chart-card">
                    <h2 className="ghl-chart-card__title">{t(TYPE_META[type].labelKey)}</h2>
                    <ResponsiveContainer width="100%" height={200}>
                      <AreaChart data={series}>
                        <defs>
                          <linearGradient id={`bodyGrad-${type}`} x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor="var(--md-sys-color-primary)" stopOpacity={0.35} />
                            <stop offset="100%" stopColor="var(--md-sys-color-primary)" stopOpacity={0} />
                          </linearGradient>
                        </defs>
                        <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5)} stroke="var(--md-sys-color-outline)" />
                        <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={40} domain={['dataMin - 1', 'dataMax + 1']} />
                        <Tooltip contentStyle={tooltipStyle} formatter={(v) => `${v} ${TYPE_META[type].unit}`} />
                        <Area type="monotone" dataKey="value" stroke="var(--md-sys-color-primary)" fill={`url(#bodyGrad-${type})`} strokeWidth={2} />
                      </AreaChart>
                    </ResponsiveContainer>
                  </Surface>
                )
              })}
            </div>
          </section>
        )}

        <Surface tone="low">
          <h2 className="ghl-section-title">{t('body.historyTitle')}</h2>
          {historyRows.length === 0 ? (
            <p className="ghl-more-text">{t('body.historyEmpty')}</p>
          ) : (
            <div className="ghl-table-scroll">
              <table className="ghl-splits-table">
                <thead>
                  <tr>
                    <th>{t('body.dateLabel')}</th>
                    <th>{t('body.historyMeasurementCol')}</th>
                    <th>{t('body.historyValueCol')}</th>
                  </tr>
                </thead>
                <tbody>
                  {historyRows.map((row) => (
                    <tr key={`${row.date}|${row.type}|${row.side}`}>
                      <td>{row.date}</td>
                      <td>{rowLabel(row.type, row.side)}</td>
                      <td>
                        {row.value} {TYPE_META[row.type].unit}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Surface>

        <Surface tone="low">
          <h2 className="ghl-section-title">{t('body.profileTitle')}</h2>
          <p className="ghl-chart-card__hint">{t('body.profileHint')}</p>
          <div className="ghl-body-profile">
            <label className="ghl-body-entry__field">
              <span>{t('body.heightLabel')}</span>
              <div className="ghl-body-entry__input-wrap">
                <input type="number" inputMode="decimal" step="0.5" value={heightInput} onChange={(e) => setHeightInput(e.target.value)} />
                <span className="ghl-body-entry__unit">cm</span>
              </div>
            </label>
            <div className="ghl-body-entry__field">
              <span>{t('body.sexLabel')}</span>
              <SegmentedButton
                options={[
                  { value: '' as const, label: t('body.sexUnset') },
                  { value: 'female' as const, label: t('body.sexFemale') },
                  { value: 'male' as const, label: t('body.sexMale') },
                ]}
                value={sex}
                onChange={setSex}
              />
            </div>
          </div>
          <md-outlined-button onClick={saveProfile}>{t('body.saveProfile')}</md-outlined-button>

          <h2 className="ghl-section-title ghl-body-tracked-title">{t('body.trackedTypesTitle')}</h2>
          <div className="ghl-body-type-checklist">
            {BODY_MEASUREMENT_TYPES.map((type) => (
              <label key={type} className="ghl-body-type-checklist__item">
                <input type="checkbox" checked={trackedTypes.has(type)} onChange={(e) => setTypeTracked(type, e.target.checked)} />
                {t(TYPE_META[type].labelKey)}
                {isPaired(type) && <span className="ghl-body-type-checklist__paired-hint">{t('body.pairedHint')}</span>}
              </label>
            ))}
          </div>
        </Surface>
      </div>
    </div>
  )
}
