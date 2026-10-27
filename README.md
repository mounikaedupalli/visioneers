# PhishGuard

Hackathon solution for CSC DBS Problem 3 (phishing takedowns). It identifies real phishing URLs among a
high volume of incoming URLs while cutting the false positives analysts review by hand.

## Projects

| Project | Status | Purpose |
|---|---|---|
| `PhishGuard.Core` | ✅ | Domain entities, phishing case taxonomy (`UrlCategory`), protected brands, offline `UrlParser`, `UrlFeatureExtractor` (40 lexical + brand-aware features), `CampaignKey` grouping |
| `PhishGuard.Data` | ✅ | EF Core `PhishGuardDbContext` (SQL Server), `InitialSchema` migration, `BulkLoader` (SqlBulkCopy) |
| `PhishGuard.DataGen` | ✅ | Builds the labelled 500K dataset + simulated WHOIS/cert/page evidence, optional public feeds |
| `PhishGuard.Ml` | ✅ | Shared ML.NET model input, pipelines (baseline + full), band thresholds, model storage |
| `PhishGuard.Training` | ✅ | Trains baseline vs PhishGuard LightGBM models, writes `models/model.zip`, `model.json`, `training-report.md` |
| `PhishGuard.Scoring` | ✅ | Worker: Tier 0 rules + model → Clear / Gray / Confirmed → campaigns → cases (about 40 s for 500K) |
| `PhishGuard.Api` | ✅ | Minimal API: dataset (`/api/stats`, `/api/urls`, `/api/brands`, `/api/evidence/{domain}`) and results (see below) |
| `PhishGuard.Web` | 🟡 | Blazor Server console (dataset overview page) |
| `PhishGuard.Agents` | ⬜ stub | Agent Framework agents (Evidence, Brand, Verdict, Enforcement, Monitor) on Azure OpenAI |
| `PhishGuard.AppHost` | ✅ | Aspire: runs Api + Scoring + Web against the `phishguard` connection string |
| `PhishGuard.Tests` | ✅ | xUnit tests for the parser and feature extractor |

## Prerequisites

- .NET 9 SDK (moving to .NET 10 + Aspire 13 later just means bumping the versions)
- SQL Server LocalDB (comes with Visual Studio / SQL Server Express), or any SQL Server; edit `ConnectionStrings:phishguard`
- VS Code + C# Dev Kit
- **Keep the repo at a short path** such as `C:\PhishGuard`. Windows has a 260-character path limit
  unless long paths are enabled; deep folders break the build with "file not found" copy errors.

## Run

```bash
dotnet tool restore
dotnet build
dotnet test
dotnet run --project src/PhishGuard.DataGen -- --reset
dotnet run --project src/PhishGuard.Training
dotnet run --project src/PhishGuard.AppHost   # Scoring scores everything on start-up
```

DataGen options: `--count 500000 --phish-rate 0.02 --seed 42 --days 14 --reset --csv data/urls.csv`
`--download-feeds` (OpenPhish, PhishStats, Tranco → `data/feeds/`) `--feeds-dir <dir>` `--feed-share 0.3`.
The 500K run takes about 30 seconds on LocalDB.

Training options: `--iterations 300 --target-recall 0.98 --target-precision 0.995 --max-clear 0.5 --out models --daily-volume 1500000`.
It takes about 3 minutes and splits by time: oldest 60% to fit, next 10% to choose thresholds, newest 30% to test.
Thresholds are chosen on phishing from domains **not** seen during fitting (repeat campaign domains are easy and would
push the Clear threshold close to 1.0), and the Clear threshold is capped at `--max-clear`.
The Scoring worker picks up a retrained model automatically and scores every URL again under the new version.

## Dataset design

- 98% benign / 2% phishing by default. Every row carries a `Category` so results are reported per attack type.
- Phishing comes from **attacker campaigns**: one host + kit path reused for many victims over a few hours to days.
  `--new-campaign-rate 0.15` gives about 1.5K campaigns for 10K phishing URLs, a few large and many single-URL.
- Benign data deliberately includes **look-alike-but-legitimate** URLs (long SSO redirects, SharePoint/OneDrive
  shares, click-tracking, CDN hashes, the brands' own login subdomains). These drive today's false positives.
- Phishing types: typosquat, homoglyph (punycode), combosquat, brand-in-subdomain, brand-in-path, free hosting,
  open redirect, `user@host` trick, raw IP, compromised WordPress, long encoded URLs (≤256 chars), shorteners,
  plus real feed samples.
- **Emerging tactics:** homoglyph and encoded-long URLs only appear in the last third of the time window, so a
  time-based train/test split measures how the model handles a tactic it has not seen.
- `DomainEvidence` simulates RDAP/WHOIS, certificate transparency and a sandboxed page snapshot, so agents gather
  evidence without opening any suspected site. Feed URLs are only ever read as text.

## Schema (SQL Server)

`Brands` · `Urls` · `DomainEvidence` · `UrlScores` · `Campaigns` · `Cases` · `AgentRuns` · `AnalystDecisions` ·
`EnforcementNotices` · `MonitorChecks`. Enums are stored as strings.

New migration: `dotnet ef migrations add <Name> --project src/PhishGuard.Data --output-dir Migrations`

## Getting the results

| Endpoint | Returns |
|---|---|
| `GET /api/scoring/summary` | Model version, counts per band, campaigns, cases by status, plus an evaluation block using the synthetic labels |
| `GET /api/phishing?includeGray=false&brandId=&since=&format=csv` | Detected phishing URLs (Confirmed, optionally Gray). Add `format=csv&pageSize=500000` to download all |
| `GET /api/campaigns?status=AgentReview` | Gray queue for agents/analysts (`status=Confirmed` gives the takedown queue) |
| `GET /api/campaigns/{id}` | One campaign: case, domain evidence and up to 200 of its URLs |

Or query SQL directly: `UrlScores` (one row per URL per model version), `Campaigns`, `Cases`.

## Next phases

1. **Agents:** Gray cases (`Status = AgentReview`) → Evidence / Brand / Verdict agents → Enforcement notice drafts (never sent).
2. **Web:** funnel dashboard from `/api/scoring/summary`, analyst queue from `/api/campaigns`, decisions written back as training labels.
3. **Data realism:** the synthetic URLs are still too easy (the lexical baseline scores ~97% recall). Add harder cases:
   benign URLs with login/verify words, phishing kits without lure words in the path, and kits shared across domains.
4. **Azure:** Azure SQL + Container Apps via `azd up`, Azure OpenAI with Managed Identity, Application Insights.
