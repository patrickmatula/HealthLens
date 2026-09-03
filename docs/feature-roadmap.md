# Feature roadmap (in progress, self-paced)

Tracks the 13 features requested 2026-09-03. Update the checkbox and add a one-line note when a feature
is committed. This file is the resume point across context resets — always read it first when continuing
this work.

## Dashboard / insight group
- [ ] 1. Jahresrückblick ("Year in HealthLens")
- [x] 2. Wochenzusammenfassung (7-day digest card) — committed, deployed to demo container
- [x] 3. Kalender-Heatmap (Trainingskonsistenz) — committed, deployed to demo container
- [x] 4. "An diesem Tag vor X Jahren" Flashback — committed, deployed to demo container
- [x] 5. Witzige Distanzvergleiche — committed, deployed to demo container

## Workout-detail group
- [x] 6. Pacing-Strategie-Analyse (negative/positive/even split) — committed, deployed to demo container
- [x] 7. Aerobe Decoupling pro Lauf — committed, deployed to demo container
- [x] 8. Höhenkorrigierte Pace (Grade Adjusted Pace) — committed, deployed to demo container (Minetti model, aggregated into ~30m segments to tame GPS/altitude noise -- see runningMetrics.ts comment)

## Training-load / recovery group
- [ ] 9. Trainingslast-Ampel (ACWR)
- [ ] 10. Krankheits-/Übertrainings-Frühwarnung (HRV/RHR)
- [ ] 11. Rennzeit-Prognose (Riegel-Rechner)

## Standalone
- [ ] 12. Schuh-Performance-Vergleich
- [ ] 13. Wetter-Kontext pro Workout — **needs explicit user go-ahead first**: requires an external weather-by-GPS-coordinate API call, which is a new external data flow this self-hosted app doesn't currently have. Ask before starting; do not implement silently.

## Working notes
- Demo container only (9080/9443) gets rebuilt/redeployed per feature-group checkpoint, per the
  demo-vs-prod policy. Production (8080/8443) stays untouched unless the user explicitly says so.
- Verify each group with build + lint + (backend) dotnet test before deploying, same bar as the rest of
  this session's work.
- Token-budget pacing: before starting a new feature, check the `<total_tokens>` figure surfaced in the
  system reminder. If it looks too low to safely finish another feature and get it committed cleanly,
  stop, make sure everything so far is committed and this file is up to date, then use ScheduleWakeup
  (~1 hour delay) to resume rather than pushing into a mid-feature context cutoff.
