const PATHS = {
  dashboard: 'M4 4h7v9H4V4zm9 0h7v5h-7V4zm0 7h7v9h-7v-9zM4 15h7v5H4v-5z',
  workouts: 'M3 10h2v4H3v-4zm3-2h2v8H6V8zm3-3h2v14H9V5zm3 3h2v8h-2V8zm3 2h2v4h-2v-4z',
  sleep: 'M12.5 3c-5 .3-9 4.5-9 9.5C3.5 18 7.9 22 13 22c4.2 0 7.8-2.8 8.9-6.7-1.3.6-2.7 1-4.2 1-5 0-9-4-9-9 0-1.6.4-3.1 1.1-4.4-.4-.1-.9-.2-1.3-.2z',
  heart: 'M12 21s-7.5-4.6-10.1-9.1C.4 9 1.4 5.7 4.2 4.4c2.3-1.1 4.9-.3 6.3 1.6l1.5 2 1.5-2c1.4-1.9 4-2.7 6.3-1.6 2.8 1.3 3.8 4.6 2.3 7.5C19.5 16.4 12 21 12 21z',
  recovery: 'M3 17c2-3 4-3 6 0s4 3 6 0 4-3 6 0M3 11c2-3 4-3 6 0s4 3 6 0 4-3 6 0',
  more: 'M6 12a2 2 0 11-4 0 2 2 0 014 0zm8 0a2 2 0 11-4 0 2 2 0 014 0zm8 0a2 2 0 11-4 0 2 2 0 014 0z',
  upload: 'M12 3l5 5h-3v7h-4V8H7l5-5zM5 19h14v2H5v-2z',
  check: 'M9 16.2l-3.5-3.5L4 14.2l5 5 11-11-1.4-1.4z',
  chevronRight: 'M9 6l6 6-6 6',
  trophy: 'M6 3h12v2h2v3a4 4 0 01-4 4v0a4 4 0 01-3 3.87V17h3v2H8v-2h3v-1.13A4 4 0 018 12v0a4 4 0 01-4-4V5h2V3zm0 4H4v1a2 2 0 002 2V7zm12 0v3a2 2 0 002-2V7h-2z',
  route: 'M5 4a3 3 0 100 6 3 3 0 000-6zm14 10a3 3 0 100 6 3 3 0 000-6zM6.5 9.5C9 12 12 12 14 12s5 0 5 3.5',
  moon: 'M12.5 3c-5 .3-9 4.5-9 9.5C3.5 18 7.9 22 13 22c4.2 0 7.8-2.8 8.9-6.7-1.3.6-2.7 1-4.2 1-5 0-9-4-9-9 0-1.6.4-3.1 1.1-4.4-.4-.1-.9-.2-1.3-.2z',
  search: 'M15.5 14h-.79l-.28-.27a6.47 6.47 0 001.48-5.34C15.47 5.1 12.9 3 10 3a6.5 6.5 0 100 13c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 5L21 19l-5-5zm-6 0a4.5 4.5 0 110-9 4.5 4.5 0 010 9z',
  close: 'M18.3 5.71L12 12.01l-6.3-6.3-1.41 1.41L10.59 13.42l-6.3 6.3 1.41 1.41 6.3-6.3 6.3 6.3 1.41-1.41-6.3-6.3 6.3-6.3z',
} as const

const RAY_ANGLES = [0, 45, 90, 135, 180, 225, 270, 315]

export type IconName = keyof typeof PATHS | 'sun' | 'auto'

export function Icon({ name, size = 24 }: { name: IconName; size?: number }) {
  if (name === 'sun') {
    return (
      <svg width={size} height={size} viewBox="0 0 24 24" aria-hidden="true">
        <circle cx="12" cy="12" r="4.5" fill="currentColor" />
        {RAY_ANGLES.map((angle) => (
          <line
            key={angle}
            x1="12"
            y1="3.5"
            x2="12"
            y2="6"
            stroke="currentColor"
            strokeWidth="1.8"
            strokeLinecap="round"
            transform={`rotate(${angle} 12 12)`}
          />
        ))}
      </svg>
    )
  }

  if (name === 'auto') {
    return (
      <svg width={size} height={size} viewBox="0 0 24 24" aria-hidden="true">
        <circle cx="12" cy="12" r="8.5" fill="none" stroke="currentColor" strokeWidth="1.8" />
        <path d="M12 3.5a8.5 8.5 0 010 17z" fill="currentColor" />
      </svg>
    )
  }

  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="none" aria-hidden="true">
      <path d={PATHS[name]} fill="currentColor" />
    </svg>
  )
}
