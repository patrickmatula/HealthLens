import { CircleMarker, MapContainer, TileLayer, Tooltip } from 'react-leaflet'
import type { WorkoutLocationDto } from '../api/types'
import './TrainingLocationsMap.css'

// Groups nearby workout starting points into one bubble (~0.2° ≈ 22km, roughly "same metro area") so a
// home location trained at hundreds of times over the years shows as one sized bubble instead of an
// unreadable pile of overlapping dots — trips further away naturally stay as their own small bubbles.
const GRID_DEGREES = 0.2

function clusterLocations(locations: WorkoutLocationDto[]) {
  const buckets = new Map<string, { lat: number; lon: number; count: number }>()
  for (const loc of locations) {
    const key = `${Math.round(loc.latitude / GRID_DEGREES)}:${Math.round(loc.longitude / GRID_DEGREES)}`
    const entry = buckets.get(key) ?? { lat: 0, lon: 0, count: 0 }
    entry.lat += loc.latitude
    entry.lon += loc.longitude
    entry.count += 1
    buckets.set(key, entry)
  }
  return [...buckets.values()].map((b) => ({ lat: b.lat / b.count, lon: b.lon / b.count, count: b.count }))
}

export function TrainingLocationsMap({ locations, countLabel }: { locations: WorkoutLocationDto[]; countLabel: (count: number) => string }) {
  if (locations.length === 0) {
    return null
  }

  const clusters = clusterLocations(locations)
  const lats = clusters.map((c) => c.lat)
  const lons = clusters.map((c) => c.lon)
  const bounds: [[number, number], [number, number]] = [
    [Math.min(...lats), Math.min(...lons)],
    [Math.max(...lats), Math.max(...lons)],
  ]
  const maxCount = Math.max(...clusters.map((c) => c.count))

  return (
    <div className="ghl-locations-map">
      <MapContainer bounds={bounds} boundsOptions={{ padding: [32, 32], maxZoom: 11 }} scrollWheelZoom={false} style={{ height: '100%', width: '100%' }}>
        <TileLayer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" attribution="&copy; OpenStreetMap contributors" />
        {clusters.map((c, i) => (
          <CircleMarker
            key={i}
            center={[c.lat, c.lon]}
            radius={8 + (Math.sqrt(c.count) / Math.sqrt(maxCount)) * 18}
            pathOptions={{ color: '#2962ff', fillColor: '#2962ff', fillOpacity: 0.45, weight: 2 }}
          >
            <Tooltip>{countLabel(c.count)}</Tooltip>
          </CircleMarker>
        ))}
      </MapContainer>
    </div>
  )
}
