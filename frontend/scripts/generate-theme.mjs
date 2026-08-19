// Regenerates src/theme/colors.generated.css from a single seed color, using Google's own
// material-color-utilities (the HCT color-science library behind Material Theme Builder).
// Run with: node scripts/generate-theme.mjs
import { argbFromHex, hexFromArgb, Hct, SchemeExpressive } from '@material/material-color-utilities'
import { writeFileSync } from 'node:fs'

// Fitness/health-appropriate seed: a fresh emerald teal.
const SEED_HEX = '#12876F'

const ROLES = [
  'primary', 'onPrimary', 'primaryContainer', 'onPrimaryContainer',
  'secondary', 'onSecondary', 'secondaryContainer', 'onSecondaryContainer',
  'tertiary', 'onTertiary', 'tertiaryContainer', 'onTertiaryContainer',
  'error', 'onError', 'errorContainer', 'onErrorContainer',
  'background', 'onBackground',
  'surface', 'onSurface', 'surfaceVariant', 'onSurfaceVariant',
  'surfaceDim', 'surfaceBright',
  'surfaceContainerLowest', 'surfaceContainerLow', 'surfaceContainer', 'surfaceContainerHigh', 'surfaceContainerHighest',
  'outline', 'outlineVariant',
  'inverseSurface', 'inverseOnSurface', 'inversePrimary',
  'scrim', 'shadow', 'surfaceTint',
]

function kebab(role) {
  return role.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase()
}

function block(scheme) {
  return ROLES.map((role) => `  --md-sys-color-${kebab(role)}: ${hexFromArgb(scheme[role])};`).join('\n')
}

const sourceHct = Hct.fromInt(argbFromHex(SEED_HEX))
const light = new SchemeExpressive(sourceHct, false, 0)
const dark = new SchemeExpressive(sourceHct, true, 0)

const css = `/* GENERATED FILE — do not hand-edit. Regenerate with: node scripts/generate-theme.mjs */
/* Seed color: ${SEED_HEX}, Material 3 Expressive scheme, via @material/material-color-utilities. */

:root {
${block(light)}
}

@media (prefers-color-scheme: dark) {
  :root:not([data-theme='light']) {
${block(dark)}
  }
}

:root[data-theme='dark'] {
${block(dark)}
}
`

writeFileSync(new URL('../src/theme/colors.generated.css', import.meta.url), css)
console.log('Wrote src/theme/colors.generated.css')
