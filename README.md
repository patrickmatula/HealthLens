# HealthLens

A self-hosted dashboard for your Google Takeout health export (Fitbit/Google Health data). You import a Takeout zip once, and HealthLens turns it into workout, sleep, heart, and recovery views with long-term trends and personal records. All data stays on your own server.

> [!NOTE]
> HealthLens is a private, personal project. Claude wrote nearly all of the code from prompts, commonly called "vibe coding." It exists to explore one person's own Takeout export, not as a maintained open-source product — expect rough edges and no support guarantees.

## Features

- **Dashboard** — cross-metric overview with auto-generated insight callouts (new PRs, unusual resting-HR, sleep-debt streaks).
- **Workouts** — personal-record board, pace/HR charts, GPS route maps, per-km splits.
- **Sleep** — nightly hypnogram, sleep-score trends, weekday-vs-weekend comparison.
- **Heart & recovery** — resting heart rate, HRV, stress, SpO2, temperature, readiness, all with medically sourced reference ranges.
- **Shoe tracking** (optional) — assign runs to a shoe, track mileage per pair.
- **Body measurements** (optional) — weight, body fat, and circumference tracking with BMI/WHtR/body-fat assessments.
- **Google Health sync** (optional) — pulls recent activity and weight data from the Google Health API between Takeout exports. Setup steps are in the app under "More."

Every feature beyond the core dashboard is off by default and lives under "More" in the app.

> [!TIP]
> Google requires https for the sync feature's OAuth redirect. The container generates a self-signed certificate on first run and serves it on port 8443 for that one redirect only — the rest of the app keeps using plain http, and your browser will show a one-time certificate warning inside the sign-in popup, which is expected.

## Run it with Docker

You need Docker and Docker Compose. From the repository root:

```bash
docker compose up -d
```

This pulls the published image (`ghcr.io/patrickmatula/healthlens`) — no local build, no .NET or Node SDK needed. Open http://localhost:8080.

To build from source instead, edit `docker-compose.yml`: comment out `image:`, uncomment `build: .`, then run `docker compose up -d --build`.

Podman works too — `podman compose up -d` if you have `podman-compose` installed, or run the published image directly:

```bash
podman volume create healthlens-data
podman run -d --name healthlens -p 8080:8080 -p 8443:8443 -v healthlens-data:/app/App_Data ghcr.io/patrickmatula/healthlens:latest
```

Your data lives in a named Docker volume (`healthlens-data`), independent of the container. Removing or rebuilding the container leaves your data intact; `docker compose down -v` deletes it.

To use different host ports, set `HEALTHLENS_PORT` and/or `HEALTHLENS_HTTPS_PORT` before starting:

```bash
HEALTHLENS_PORT=9000 HEALTHLENS_HTTPS_PORT=9443 docker compose up -d
```

To update to the latest published image:

```bash
docker compose pull && docker compose up -d
```

To back up your data:

```bash
docker run --rm -v healthlens-data:/data -v "$PWD":/backup alpine tar czf /backup/healthlens-backup.tar.gz -C /data .
```

## Automated builds

> [!NOTE]
> The published image updates itself: dependency and base-image patches are checked weekly, safe updates merge and republish automatically, and every code change gets its own version tag (e.g. `v1.4.2`) alongside `latest`. Pin `docker-compose.yml` to a specific tag if you'd rather update on your own schedule. Details for maintainers are in the `.github/workflows/` files.

## First import

Export your data from [Google Takeout](https://takeout.google.com) under "Google Health," unzip it, and upload the zip on HealthLens' first screen. Choose "Curated" for a fast import with 1-minute aggregates outside workouts, or "Full" to keep every raw data point. Both keep full resolution during workouts.

You can also import without saving anything to disk ("session only") to try the app on a one-off export.

## Local development

Requires .NET 10 SDK and Node 20.19+ (or 22.12+).

```bash
dotnet run --project backend/HealthLens.Api    # API on :5172
npm --prefix frontend run dev                  # Vite dev server, proxies /api to :5172
```

## Privacy

HealthLens sends nothing anywhere except what you explicitly connect: your own server stores your imported data, and the optional Google Health sync talks directly to Google's API using OAuth credentials you register yourself.

## License

[CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/) — see [LICENSE](LICENSE). Use, modify, and share this for any non-commercial purpose, as long as you credit this repository. No commercial use.
