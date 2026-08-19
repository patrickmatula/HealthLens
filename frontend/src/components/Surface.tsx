import type { PropsWithChildren } from 'react'
import './Surface.css'

type Tone = 'lowest' | 'low' | 'base' | 'high' | 'highest'

export function Surface({
  children,
  tone = 'base',
  padded = true,
  className = '',
}: PropsWithChildren<{ tone?: Tone; padded?: boolean; className?: string }>) {
  return (
    <div className={`ghl-surface ghl-surface--${tone} ${padded ? 'ghl-surface--padded' : ''} ${className}`}>
      {children}
    </div>
  )
}
