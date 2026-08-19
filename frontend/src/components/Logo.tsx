export function Logo({ size = 32, className }: { size?: number; className?: string }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      className={className}
      fill="none"
      aria-hidden="true"
    >
      <circle cx="10" cy="10" r="6.5" stroke="currentColor" strokeWidth="2" />
      <line x1="14.6" y1="14.6" x2="20.2" y2="20.2" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" />
      <polyline
        points="6,10 8.4,10 9.6,6.8 10.8,13.2 12,10 14,10"
        stroke="currentColor"
        strokeWidth="1.3"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}
