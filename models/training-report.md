# PhishGuard training report — 2026-10-27 15:27 UTC

Test set: newest 1,50,000 URLs (3,782 phishing). Daily figures scale the test set to 15,00,000 URLs/day.
Thresholds chosen on a separate validation slice (phishing from domains not seen in fitting + all benign) for ≥98% recall (Clear, capped at 0.5) and ≥99.5% precision (Confirmed, at least 0.9).
"New domains" = test phishing whose registered domain never appeared as phishing in fitting or validation (3,051 URLs).

| Model | PR-AUC | Recall | Recall on new domains | Flagged (Gray+Confirmed) | False positives in flagged | Auto-confirmed | Sent to review (Gray) | Review / day |
|---|---|---|---|---|---|---|---|---|
| Baseline (URL structure only) | 0.9994 | 96.6% | 95.8% | 3,655 | 1 (0.0%) | 3,566 (precision 100.0%) | 89 | 890 |
| PhishGuard URL-only (structure + brand + char n-grams) | 0.9998 | 96.1% | 95.2% | 3,636 | 0 (0.0%) | 3,228 (precision 100.0%) | 408 | 4,080 |
| PhishGuard + evidence (domain age, certificate, page snapshot) | 0.9999 | 96.1% | 95.2% | 3,636 | 0 (0.0%) | 3,193 (precision 100.0%) | 443 | 4,430 |

## Same recall, fewer false alarms
Flagged URLs and false positives at a fixed recall on the test set (single threshold, as the sponsor runs today):

| Model | Recall 90% | Recall 95% | Recall 98% |
|---|---|---|---|
| Baseline (URL structure only) | 3,409 flagged, 0% FP | 3,594 flagged, 0% FP | 3,708 flagged, 0% FP |
| PhishGuard URL-only (structure + brand + char n-grams) | 3,423 flagged, 0% FP | 3,628 flagged, 0% FP | 3,728 flagged, 1% FP |
| PhishGuard + evidence (domain age, certificate, page snapshot) | 3,619 flagged, 0% FP | 3,619 flagged, 0% FP | 3,717 flagged, 0% FP |

## Per category
Phishing rows: share caught (not Clear). Benign rows: share wrongly flagged. One column per model, same order as above.

| Category | Rows in test | Baseline | PhishGuard URL-only | PhishGuard + evidence | Auto-confirmed (PhishGuard + evidence) |
|---|---|---|---|---|---|
| BenignPlain | 80,262 | 0.0% | 0.0% | 0.0% | 0 |
| BenignLongSso | 14,602 | 0.0% | 0.0% | 0.0% | 0 |
| BenignSharePoint | 14,719 | 0.0% | 0.0% | 0.0% | 0 |
| BenignTracking | 17,491 | 0.0% | 0.0% | 0.0% | 0 |
| BenignCdn | 11,955 | 0.0% | 0.0% | 0.0% | 0 |
| BenignBrandSubdomain | 7,189 | 0.0% | 0.0% | 0.0% | 0 |
| Typosquat | 200 | 97.0% | 100.0% | 100.0% | 200 |
| Homoglyph (emerging) | 559 | 100.0% | 99.8% | 100.0% | 558 |
| Combosquat | 337 | 99.1% | 100.0% | 100.0% | 337 |
| SubdomainBrand | 306 | 100.0% | 100.0% | 100.0% | 306 |
| PathBrand | 500 | 98.2% | 100.0% | 100.0% | 500 |
| FreeHosting | 221 | 99.5% | 100.0% | 100.0% | 221 |
| OpenRedirect | 413 | 94.9% | 100.0% | 94.9% | 375 |
| UserInfoTrick | 49 | 100.0% | 100.0% | 100.0% | 49 |
| IpHost | 123 | 100.0% | 100.0% | 100.0% | 123 |
| CompromisedSite | 481 | 99.2% | 100.0% | 100.0% | 481 |
| EncodedLongUrl (emerging) | 566 | 85.2% | 74.4% | 77.9% | 16 |
| Shortener | 27 | 100.0% | 100.0% | 100.0% | 27 |
