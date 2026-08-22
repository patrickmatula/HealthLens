# HealthLens

A self-hosted dashboard for your Google Takeout health export (Fitbit/Google Health data). You import a Takeout zip once, and HealthLens turns it into workout, sleep, heart, and recovery views with long-term trends and personal records. All data stays on your own server.

## Features

- **Dashboard** — cross-metric overview with auto-generated insight callouts (new PRs, unusual resting-HR, sleep-debt streaks).
- **Workouts** — personal-record board, pace/HR charts, GPS route maps, per-km splits.
- **Sleep** — nightly hypnogram, sleep-score trends, weekday-vs-weekend comparison.
- **Heart & recovery** — resting heart rate, HRV, stress, SpO2, temperature, readiness, all with medically sourced reference ranges.
- **Shoe tracking** (optional) — assign runs to a shoe, track mileage per pair.
- **Body measurements** (optional) — weight, body fat, and circumference tracking with BMI/WHtR/body-fat assessments.
- **Google Health sync** (optional) — pulls recent activity and weight data from the Google Health API between Takeout exports. See the setup steps under "More" in the app.

Every feature beyond the core dashboard is off by default and lives under "More" in the app.

## Run it with Docker

You need Docker and Docker Compose. From the repository root:

```bash
docker compose up -d
```

Open http://localhost:8080. The container builds on first run, so expect a minute or two before it's ready.

Podman works too — `podman compose up -d` if you have `podman-compose` installed, or build and run it directly:

```bash
podman build -t healthlens .
podman volume create healthlens-data
podman run -d --name healthlens -p 8080:8080 -v healthlens-data:/app/App_Data healthlens
```

Your data lives in a named Docker volume (`healthlens-data`), independent of the container. Removing or rebuilding the container leaves your data intact; `docker compose down -v` deletes it.

To use a different host port, set `HEALTHLENS_PORT` before starting:

```bash
HEALTHLENS_PORT=9000 docker compose up -d
```

To pick up code changes, rebuild the image:

```bash
docker compose up -d --build
```

To back up your data:

```bash
docker run --rm -v healthlens-data:/data -v "$PWD":/backup alpine tar czf /backup/healthlens-backup.tar.gz -C /data .
```

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
