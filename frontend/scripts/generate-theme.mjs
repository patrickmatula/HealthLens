// Regenerates src/theme/colors.generated.css from a set of named seed colors, using Google's own
// material-color-utilities (the HCT color-science library behind Material Theme Builder) per the
// official Material 3 color system (m3.material.io/styles/color/system/overview).
// Run with: node scripts/generate-theme.mjs
import { argbFromHex, hexFromArgb, Hct, SchemeExpressive, SchemeFidelity } from '@material/material-color-utilities'
import { writeFileSync } from 'node:fs'

// Each theme gets a full light+dark M3 palette (primary/secondary/tertiary/error, each with its own
// container and "on" pairs, plus the neutral/neutral-variant surface roles). The default theme
// (isDefault: true) also applies when no [data-color-theme] attribute is set yet (first paint, before
// ThemeContext mounts).
//
// The 4 named color themes use the "Fidelity" scheme variant, not "Expressive": Fidelity keeps the
// primary color close to the literal seed hue (M3 docs: "the resulting color palettes match the seed
// color, even if the seed color is very bright"), whereas Expressive deliberately rotates the primary
// hue away from the seed "for variety" -- which is exactly backwards for a theme picker where the name
// ("Orange") needs to visibly match what you get. Verified empirically: the same 4 seeds under
// Expressive previously produced an orange-brown teal, a purple-shifted sand, etc.; under Fidelity every
// theme's primary now falls in the same hue family as its name.
//
// "white" is the deliberately neutral theme: light mode should read as white/near-white, dark mode as
// black/near-black. It uses an achromatic seed (#787878, R=G=B) with Expressive (not Fidelity -- Fidelity
// would keep accents as muted/gray as the seed itself, defeating the point) so primary/secondary/tertiary
// stay clearly colorful, combined with trueNeutralSurfaces (see below) so the surfaces stay genuinely
// gray rather than picking up a faint tint.
const THEMES = [
  { key: 'teal', label: 'Teal', seed: '#12876F', isDefault: true, variant: 'fidelity' },
  { key: 'blue', label: 'Blau', seed: '#1565C0', variant: 'fidelity' },
  { key: 'violet', label: 'Violett', seed: '#7B1FA2', variant: 'fidelity' },
  { key: 'orange', label: 'Orange', seed: '#E65100', variant: 'fidelity' },
  { key: 'white', label: 'Weiß', seed: '#787878', trueNeutralSurfaces: true },
]

// M3's "neutral" and "neutral-variant" tonal palettes (which back these roles) always carry a small
// fixed chroma no matter how gray the seed is -- a plain achromatic seed under Expressive still produced
// a visibly cyan-tinted #f3fbff background. For "white", these roles are recomputed at literal chroma 0
// via Hct.from(0, 0, tone) instead, reusing each role's own tone from the normal scheme -- genuinely
// R=G=B this time.
const NEUTRAL_ROLES = new Set([
  'background', 'onBackground',
  'surface', 'onSurface', 'surfaceVariant', 'onSurfaceVariant',
  'surfaceDim', 'surfaceBright',
  'surfaceContainerLowest', 'surfaceContainerLow', 'surfaceContainer', 'surfaceContainerHigh', 'surfaceContainerHighest',
  'outline', 'outlineVariant',
  'inverseSurface', 'inverseOnSurface',
])

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

function block(scheme, { trueNeutralSurfaces } = {}) {
  return ROLES.map((role) => {
    let argb = scheme[role]
    if (trueNeutralSurfaces && NEUTRAL_ROLES.has(role)) {
      argb = Hct.from(0, 0, Hct.fromInt(argb).tone).toInt()
    }
    return `  --md-sys-color-${kebab(role)}: ${hexFromArgb(argb)};`
  }).join('\n')
}

let css = `/* GENERATED FILE — do not hand-edit. Regenerate with: node scripts/generate-theme.mjs */
/* Material 3 color schemes via @material/material-color-utilities, one per named color theme. */
`

for (const { key, seed, isDefault, variant, trueNeutralSurfaces } of THEMES) {
  const sourceHct = Hct.fromInt(argbFromHex(seed))
  const SchemeClass = variant === 'fidelity' ? SchemeFidelity : SchemeExpressive
  const light = new SchemeClass(sourceHct, false, 0)
  const dark = new SchemeClass(sourceHct, true, 0)
  const blockOpts = { trueNeutralSurfaces }

  const lightSelector = isDefault ? `:root, :root[data-color-theme='${key}']` : `:root[data-color-theme='${key}']`
  const darkMediaSelector = isDefault
    ? `:root:not([data-theme='light']), :root[data-color-theme='${key}']:not([data-theme='light'])`
    : `:root[data-color-theme='${key}']:not([data-theme='light'])`
  const darkExplicitSelector = isDefault
    ? `:root[data-theme='dark'], :root[data-color-theme='${key}'][data-theme='dark']`
    : `:root[data-color-theme='${key}'][data-theme='dark']`

  css += `
/* Theme: ${key} (seed ${seed}) */
${lightSelector} {
${block(light, blockOpts)}
}

@media (prefers-color-scheme: dark) {
  ${darkMediaSelector} {
${block(dark, blockOpts)}
  }
}

${darkExplicitSelector} {
${block(dark, blockOpts)}
}
`
}

writeFileSync(new URL('../src/theme/colors.generated.css', import.meta.url), css)
writeFileSync(
  new URL('../src/theme/themes.generated.ts', import.meta.url),
  `// GENERATED FILE — do not hand-edit. Regenerate with: node scripts/generate-theme.mjs\nexport const COLOR_THEMES = ${JSON.stringify(
    THEMES.map(({ key, label, seed }) => ({ key, label, seed })),
  )} as const\nexport type ColorThemeKey = (typeof COLOR_THEMES)[number]['key']\n`,
)
console.log(`Wrote src/theme/colors.generated.css and themes.generated.ts (${THEMES.length} themes)`)
