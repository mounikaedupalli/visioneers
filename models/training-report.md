# PhishGuard training report — 2026-10-23 15:43 UTC

Test set: newest 1,50,000 URLs (3,782 phishing). Daily figures scale the test set to 15,00,000 URLs/day.
Thresholds chosen on a separate validation slice (phishing from domains not seen in fitting + all benign) for ≥98% recall (Clear, capped at 0.5) and ≥99.5% precision (Confirmed).
"New domains" = test phishing whose registered domain never appeared as phishing in fitting or validation (3,027 URLs).

| Model | PR-AUC | Recall | Recall on new domains | Flagged (Gray+Confirmed) | False positives in flagged | Auto-confirmed | Sent to review (Gray) | Review / day |
|---|---|---|---|---|---|---|---|---|
| Baseline (URL structure only) | 0.9957 | 97.4% | 97.6% | 3,690 | 8 (0.2%) | 3,673 (precision 99.8%) | 17 | 170 |
| PhishGuard (structure + brand + char n-grams) | 0.9954 | 95.8% | 95.6% | 3,657 | 33 (0.9%) | 3,548 (precision 100.0%) | 109 | 1,090 |

## Same recall, fewer false alarms
Flagged URLs and false positives at a fixed recall on the test set (single threshold, as the sponsor runs today):

| Model | Recall 90% | Recall 95% | Recall 98% |
|---|---|---|---|
| Baseline (URL structure only) | 3,406 flagged, 0% FP | 3,593 flagged, 0% FP | 4,079 flagged, 9% FP |
| PhishGuard (structure + brand + char n-grams) | 3,404 flagged, 0% FP | 3,598 flagged, 0% FP | 3,980 flagged, 7% FP |

## Per category — PhishGuard (structure + brand + char n-grams)
Phishing rows: share caught (not Clear). Benign rows: share wrongly flagged.

| Category | Rows in test | Caught / wrongly flagged | Auto-confirmed | Gray |
|---|---|---|---|---|
| BenignPlain | 80,262 | 0.0% | 0 | 0 |
| BenignLongSso | 14,602 | 0.0% | 0 | 0 |
| BenignSharePoint | 14,719 | 0.0% | 0 | 0 |
| BenignTracking | 17,491 | 0.2% | 1 | 32 |
| BenignCdn | 11,955 | 0.0% | 0 | 0 |
| BenignBrandSubdomain | 7,189 | 0.0% | 0 | 0 |
| Typosquat | 200 | 100.0% | 200 | 0 |
| Homoglyph (emerging) | 559 | 93.2% | 504 | 17 |
| Combosquat | 337 | 100.0% | 337 | 0 |
| SubdomainBrand | 306 | 100.0% | 306 | 0 |
| PathBrand | 500 | 100.0% | 499 | 1 |
| FreeHosting | 221 | 100.0% | 221 | 0 |
| OpenRedirect | 413 | 100.0% | 401 | 12 |
| UserInfoTrick | 49 | 100.0% | 49 | 0 |
| IpHost | 123 | 100.0% | 123 | 0 |
| CompromisedSite | 481 | 100.0% | 481 | 0 |
| EncodedLongUrl (emerging) | 566 | 83.4% | 426 | 46 |
| Shortener | 27 | 3.7% | 0 | 1 |
