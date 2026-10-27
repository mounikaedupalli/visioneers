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
- Access to the shared **Azure SQL** database (see below), or SQL Server LocalDB for a private copy
- VS Code + C# Dev Kit
- **Keep the repo at a short path** such as `C:\PhishGuard`. Windows has a 260-character path limit
  unless long paths are enabled; deep folders break the build with "file not found" copy errors.

## Database

All projects use the `phishguard` connection string, which points at the team's shared **Azure SQL** database
`PhishGuard` on `phishguard-sql-visioneers.database.windows.net`. Sign-in is Microsoft Entra only
(`Authentication=Active Directory Default`): it uses the account you are signed in with in Visual Studio or `az login`.
No passwords are stored in the repo.

To connect you need:
1. A database user: the server's Entra admin runs `CREATE USER [you@evoketechnologies.com] FROM EXTERNAL PROVIDER;`
   plus `db_datareader` / `db_datawriter` role membership (and `db_ddladmin` to run migrations).
2. A firewall rule for your public IP (Portal → SQL servers → phishguard-sql-visioneers → Networking).

Tables were created from `db/schema-azure-portal.sql`. **Re-run it after pulling schema changes** (e.g. the
`ShortLinks` table): it is idempotent and only applies migrations Azure doesn't have yet. Shared-database rules:
- **Never run DataGen with `--reset` against Azure**: it drops the database, and EF could recreate it on a paid tier.
- **Run only one Scoring worker** for the team; others should stop the `scoring` resource in the Aspire dashboard.

To use a private LocalDB copy instead, override the connection string for your user account:

```bash
setx ConnectionStrings__phishguard "Server=(localdb)\MSSQLLocalDB;Database=PhishGuard;Trusted_Connection=True;TrustServerCertificate=True"
```

## Run

```bash
dotnet tool restore
dotnet build
dotnet test
dotnet run --project src/PhishGuard.DataGen -- --reset
dotnet run --project src/PhishGuard.Training
dotnet run --project src/PhishGuard.AppHost   # Scoring scores everything on start-up
```

DataGen options: `--count 500000 --phish-rate 0.02 --seed 42 --days 14 --reset --csv data/urls.csv --difficulty normal|hard`
`--download-feeds` (OpenPhish, PhishStats, Tranco → `data/feeds/`) `--feeds-dir <dir>` `--feed-share 0.3`.
The 500K run takes about 30 seconds on LocalDB.

Training options: `--iterations 300 --target-recall 0.98 --target-precision 0.995 --max-clear 0.5 --min-confirm 0.9 --out models --daily-volume 1500000`.
It takes about 5 minutes and splits by time: oldest 60% to fit, next 10% to choose thresholds, newest 30% to test.
It trains three models and saves the last one: the lexical **baseline**, **PhishGuard URL-only**, and
**PhishGuard + evidence** (42 URL features + 8 domain-evidence features + character n-grams).
Thresholds are chosen on phishing from domains **not** seen during fitting (repeat campaign domains are easy and would
push the Clear threshold close to 1.0). Two safety limits keep a real Gray band: never auto-clear at or above
`--max-clear`, never auto-confirm below `--min-confirm`.
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
- `ShortLinks` simulates a shortener's expand API (e.g. bit.ly `/v4/expand`): short link → destination. Training and
  Scoring judge the **destination**, and campaigns are grouped by where links really lead. Nobody visits the target.

## Hard dataset (`--difficulty hard`)

The normal dataset is too easy: phishing paths use lure words and benign URLs almost never do, so URL structure
alone is enough. Hard mode blurs those signals (the normal dataset is unchanged — same seed, same URLs):

- **Benign look-alikes:** real login / billing / verify pages on ordinary sites (`BenignLoginPortal`), unrelated
  businesses whose name contains a brand word (`BenignLookalikeName`), legitimate projects on free hosting (`BenignFreeHosting`).
- **Quieter phishing:** half of all kits use neutral paths (`index.html`, `view/document`) instead of lure words;
  `StealthKit` campaigns have no brand or lure word in the URL at all; victim tokens look like ordinary query strings.
- **Kit reuse:** some campaigns redeploy the same kit path on fresh domains (path-brand, compromised, encoded, stealth).

Try it without touching the shared database by pointing at a separate LocalDB database:

```bash
setx ConnectionStrings__phishguard "Server=(localdb)\MSSQLLocalDB;Database=PhishGuardHard;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run --project src/PhishGuard.DataGen -- --difficulty hard --reset
dotnet run --project src/PhishGuard.Training -- --out models/hard
```

Results on 500K hard URLs, test set = newest 150K (full report: [models/hard/training-report.md](models/hard/training-report.md)):

| | Baseline (URL structure) | PhishGuard URL-only | PhishGuard + evidence |
|---|---|---|---|
| Recall | 95.2% | 94.5% | 92.2% |
| Harmless share of flagged URLs | 68.5% | 62.8% | **0.1%** |
| Sent to review per day (1.5M/day) | 104,890 | 83,720 | **4,840** |
| False positives at 95% / 98% recall | 68% / 94% | 65% / 75% | **1% / 6%** |

### Weak spots addressed

| Weak spot | Before | After | How |
|---|---|---|---|
| Homoglyphs (appear only late) | 48% | **99.8%** | Confusable-character skeleton (`Confusables`, TR39-style) + mixed-script flag; homoglyph checks now use the registered label, not `www` |
| Shorteners | 9.5% | **95.6%** URL-only / 34.2% with evidence* | Simulated expand API (`ShortLinks`): the model judges the destination, campaigns group by it |
| StealthKit (no brand or lure word) | 86% via a data artefact | **84.8%** for the right reason | Removed the artefact (benign pages also use neutral paths); added domain-evidence features (domain/cert age, free cert, privacy, login form, logo) |

\* 104 of the 158 test-period phishing short links lead to a single stealth page; the evidence model misses that one
campaign and catches the other short-link campaigns. The real remaining gap is stealth kits on **aged** domains,
where neither the URL nor the evidence looks suspicious — the case for agents (page content, kit fingerprints).

Other remaining gaps: long encoded URLs that only appear late (73% with evidence; the lexical baseline catches 100%),
and the evidence model's lower overall recall (92.2%) in exchange for far fewer false positives.
To score with the hard model, set `Scoring:ModelDirectory` to `models/hard`. Scoring refuses a model trained with a
different feature layout (it checks `FeatureCount` in `model.json`) and logs what to do.

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
