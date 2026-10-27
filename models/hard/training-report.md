# PhishGuard training report — 2026-10-27 09:18 UTC

Test set: newest 1,50,000 URLs (4,010 phishing). Daily figures scale the test set to 15,00,000 URLs/day.
Thresholds chosen on a separate validation slice (phishing from domains not seen in fitting + all benign) for ≥98% recall (Clear, capped at 0.5) and ≥99.5% precision (Confirmed).
"New domains" = test phishing whose registered domain never appeared as phishing in fitting or validation (3,297 URLs).

| Model | PR-AUC | Recall | Recall on new domains | Flagged (Gray+Confirmed) | False positives in flagged | Auto-confirmed | Sent to review (Gray) | Review / day |
|---|---|---|---|---|---|---|---|---|
| Baseline (URL structure only) | 0.9751 | 99.6% | 99.6% | 20,000 | 16,005 (80.0%) | 3,438 (precision 100.0%) | 16,562 | 1,65,620 |
| PhishGuard (structure + brand + char n-grams) | 0.9895 | 89.9% | 90.3% | 3,666 | 63 (1.7%) | 3,459 (precision 99.9%) | 207 | 2,070 |

## Same recall, fewer false alarms
Flagged URLs and false positives at a fixed recall on the test set (single threshold, as the sponsor runs today):

| Model | Recall 90% | Recall 95% | Recall 98% |
|---|---|---|---|
| Baseline (URL structure only) | 3,621 flagged, 0% FP | 4,453 flagged, 14% FP | 6,586 flagged, 40% FP |
| PhishGuard (structure + brand + char n-grams) | 3,684 flagged, 2% FP | 4,151 flagged, 8% FP | 4,435 flagged, 11% FP |

## Per category — PhishGuard (structure + brand + char n-grams)
Phishing rows: share caught (not Clear). Benign rows: share wrongly flagged.

| Category | Rows in test | Caught / wrongly flagged | Auto-confirmed | Gray |
|---|---|---|---|---|
| BenignPlain | 71,917 | 0.0% | 0 | 1 |
| BenignLongSso | 13,023 | 0.0% | 0 | 0 |
| BenignSharePoint | 12,890 | 0.0% | 0 | 0 |
| BenignTracking | 15,622 | 0.4% | 3 | 58 |
| BenignCdn | 10,527 | 0.0% | 0 | 0 |
| BenignBrandSubdomain | 6,364 | 0.0% | 0 | 0 |
| BenignLoginPortal | 7,900 | 0.0% | 0 | 1 |
| BenignLookalikeName | 3,820 | 0.0% | 0 | 0 |
| BenignFreeHosting | 3,927 | 0.0% | 0 | 0 |
| Typosquat | 284 | 100.0% | 280 | 4 |
| Homoglyph (emerging) | 516 | 48.1% | 245 | 3 |
| Combosquat | 259 | 100.0% | 259 | 0 |
| SubdomainBrand | 136 | 100.0% | 136 | 0 |
| PathBrand | 203 | 100.0% | 203 | 0 |
| FreeHosting | 803 | 100.0% | 803 | 0 |
| OpenRedirect | 383 | 100.0% | 383 | 0 |
| UserInfoTrick | 94 | 100.0% | 94 | 0 |
| IpHost | 335 | 100.0% | 335 | 0 |
| CompromisedSite | 139 | 100.0% | 139 | 0 |
| EncodedLongUrl (emerging) | 523 | 94.6% | 369 | 126 |
| Shortener | 84 | 9.5% | 1 | 7 |
| StealthKit | 251 | 86.1% | 209 | 7 |
