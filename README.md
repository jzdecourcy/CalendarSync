# CalendarSync

Fetches the combined **Oakland Bulldogs 16-18** TeamSnap schedule, filters it down to the
**18u / 18s squad** (plus shared practices and Open Facility sessions), and publishes the
result as a single `.ics` file that Google Calendar can subscribe to.

The 17u-only games are dropped.

## How it works

```
GitHub Actions (cron, every 4h)
        │
        ▼
dotnet run  →  fetch source .ics  →  keep matching VEVENTs  →  docs/filtered.ics
        │
        ▼
commit docs/filtered.ics  →  GitHub Pages serves it  →  Google Calendar subscribes
```

The filter keeps any event whose title or description contains one of the
`IncludePatterns` in [`src/CalendarSync/appsettings.json`](src/CalendarSync/appsettings.json):

```json
"IncludePatterns": [ "18u", "18s", "/18", "Open Facility" ]
```

Each kept event is copied through **byte-for-byte** (UID, timezone, recurrence preserved),
so the published feed is a faithful subset of the source.

## Run locally

```powershell
dotnet run --project src/CalendarSync/CalendarSync.csproj
```

Writes `docs/filtered.ics`. Override the source feed or output without editing config:

```powershell
$env:CALSYNC_SOURCE_URL = "https://.../another.ics"
$env:CALSYNC_OUTPUT_PATH = "C:\temp\test.ics"
dotnet run --project src/CalendarSync/CalendarSync.csproj
```

## One-time cloud setup

1. **Push this repo to GitHub.**
2. **Settings → Pages →** Source: *Deploy from a branch*, Branch: `main`, Folder: `/docs`.
   Your feed URL becomes `https://<you>.github.io/CalendarSync/filtered.ics`.
3. **Settings → Actions → General →** Workflow permissions: *Read and write*
   (lets the scheduled job commit the refreshed file).
4. Run the **Sync filtered calendar** workflow once manually (Actions tab → Run workflow)
   to generate the first `docs/filtered.ics`.
5. In **Google Calendar → Other calendars → + → From URL**, paste the Pages URL above.

Google re-polls subscribed URLs on its own schedule (often several hours), so updates are
near-daily rather than instant. The cron cadence (every 4h) matches the source's CDN cache.
