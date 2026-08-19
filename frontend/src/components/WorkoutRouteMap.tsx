import { MapContainer, Polyline, TileLayer, CircleMarker } from 'react-leaflet'
import type { WorkoutSampleDto } from '../api/types'
import './WorkoutRouteMap.css'

export function WorkoutRouteMap({ samples }: { samples: WorkoutSampleDto[] }) {
  const points = samples.filter((s) => s.latitude != null && s.longitude != null).map((s) => [s.latitude!, s.longitude!] as [number, number])

  if (points.length < 2) {
    return null
  }

  const lats = points.map((p) => p[0])
  const lngs = points.map((p) => p[1])
  const bounds: [[number, number], [number, number]] = [
    [Math.min(...lats), Math.min(...lngs)],
    [Math.max(...lats), Math.max(...lngs)],
  ]

  return (
    <div className="ghl-route-map">
      <MapContainer bounds={bounds} boundsOptions={{ padding: [24, 24] }} scrollWheelZoom={false} style={{ height: '100%', width: '100%' }}>
        <TileLayer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" attribution="&copy; OpenStreetMap contributors" />
        <Polyline positions={points} pathOptions={{ color: '#2962ff', weight: 4 }} />
        <CircleMarker center={points[0]} radius={6} pathOptions={{ color: '#fff', fillColor: '#2e7d32', fillOpacity: 1, weight: 2 }} />
        <CircleMarker center={points[points.length - 1]} radius={6} pathOptions={{ color: '#fff', fillColor: '#c62828', fillOpacity: 1, weight: 2 }} />
      </MapContainer>
    </div>
  )
}
