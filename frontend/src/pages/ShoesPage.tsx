import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { ShoeDto } from '../api/types'
import { Icon } from '../components/Icon'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { useLanguage } from '../i18n/LanguageContext'
import { formatDistanceKm } from '../utils/format'
import './ShoesPage.css'

export function ShoesPage() {
  const { t } = useLanguage()
  const [shoes, setShoes] = useState<ShoeDto[]>([])
  const [loading, setLoading] = useState(true)
  const [newName, setNewName] = useState('')
  const [newBrand, setNewBrand] = useState('')
  const [creating, setCreating] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [editName, setEditName] = useState('')
  const [editBrand, setEditBrand] = useState('')

  function load() {
    setLoading(true)
    api
      .shoes()
      .then(setShoes)
      .finally(() => setLoading(false))
  }

  useEffect(load, [])

  async function handleCreate(e: FormEvent) {
    e.preventDefault()
    if (!newName.trim()) return
    setCreating(true)
    try {
      await api.createShoe(newName.trim(), newBrand.trim() || null)
      setNewName('')
      setNewBrand('')
      load()
    } finally {
      setCreating(false)
    }
  }

  function startEdit(shoe: ShoeDto) {
    setEditingId(shoe.id)
    setEditName(shoe.name)
    setEditBrand(shoe.brand ?? '')
  }

  async function saveEdit(shoe: ShoeDto) {
    if (!editName.trim()) return
    await api.updateShoe(shoe.id, editName.trim(), editBrand.trim() || null, shoe.isRetired)
    setEditingId(null)
    load()
  }

  async function toggleRetired(shoe: ShoeDto) {
    await api.updateShoe(shoe.id, shoe.name, shoe.brand, !shoe.isRetired)
    load()
  }

  async function handleDelete(shoe: ShoeDto) {
    if (!window.confirm(t('shoes.confirmDelete', { name: shoe.name }))) return
    await api.deleteShoe(shoe.id)
    load()
  }

  return (
    <div>
      <TopAppBar title={t('shoes.title')} actions={<Link to="/more" className="ghl-back-link">{t('shoes.backToSettings')}</Link>} />

      <div className="ghl-page-content">
        <Surface tone="low">
          <h2 className="ghl-section-title">{t('shoes.addTitle')}</h2>
          <form className="ghl-shoe-form" onSubmit={handleCreate}>
            <input
              type="text"
              className="ghl-shoe-form__input"
              placeholder={t('shoes.namePlaceholder')}
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
            />
            <input
              type="text"
              className="ghl-shoe-form__input"
              placeholder={t('shoes.brandPlaceholder')}
              value={newBrand}
              onChange={(e) => setNewBrand(e.target.value)}
            />
            <md-filled-button disabled={!newName.trim() || creating || undefined} type="submit">
              {t('shoes.addButton')}
            </md-filled-button>
          </form>
        </Surface>

        {!loading && shoes.length === 0 && (
          <Surface tone="low">
            <p>{t('shoes.empty')}</p>
          </Surface>
        )}

        <div className="ghl-shoe-list">
          {shoes.map((shoe) => (
            <Surface key={shoe.id} tone="low" className={`ghl-shoe-card ${shoe.isRetired ? 'ghl-shoe-card--retired' : ''}`}>
              <div className="ghl-shoe-card__icon">
                <Icon name="shoe" size={28} />
              </div>

              <div className="ghl-shoe-card__main">
                {editingId === shoe.id ? (
                  <div className="ghl-shoe-form ghl-shoe-form--inline">
                    <input className="ghl-shoe-form__input" value={editName} onChange={(e) => setEditName(e.target.value)} />
                    <input
                      className="ghl-shoe-form__input"
                      value={editBrand}
                      onChange={(e) => setEditBrand(e.target.value)}
                      placeholder={t('shoes.brandPlaceholder')}
                    />
                  </div>
                ) : (
                  <>
                    <div className="ghl-shoe-card__name">
                      {shoe.name}
                      {shoe.isRetired && <span className="ghl-shoe-card__badge">{t('shoes.retiredBadge')}</span>}
                    </div>
                    {shoe.brand && <div className="ghl-shoe-card__brand">{shoe.brand}</div>}
                  </>
                )}
                <div className="ghl-shoe-card__stats">
                  {formatDistanceKm(shoe.totalDistanceMeters)} · {t('shoes.workoutCount', { count: shoe.workoutCount })}
                </div>
              </div>

              <div className="ghl-shoe-card__actions">
                {editingId === shoe.id ? (
                  <>
                    <md-text-button onClick={() => saveEdit(shoe)}>{t('shoes.save')}</md-text-button>
                    <md-text-button onClick={() => setEditingId(null)}>{t('shoes.cancel')}</md-text-button>
                  </>
                ) : (
                  <>
                    <md-text-button onClick={() => startEdit(shoe)}>{t('shoes.edit')}</md-text-button>
                    <md-text-button onClick={() => toggleRetired(shoe)}>
                      {shoe.isRetired ? t('shoes.reactivate') : t('shoes.retire')}
                    </md-text-button>
                    <md-text-button onClick={() => handleDelete(shoe)}>{t('shoes.delete')}</md-text-button>
                  </>
                )}
              </div>
            </Surface>
          ))}
        </div>
      </div>
    </div>
  )
}
