# PhishGuard training report — 2026-10-27 15:22 UTC

Test set: newest 1,50,000 URLs (4,773 phishing). Daily figures scale the test set to 15,00,000 URLs/day.
Thresholds chosen on a separate validation slice (phishing from domains not seen in fitting + all benign) for ≥98% recall (Clear, capped at 0.5) and ≥99.5% precision (Confirmed, at least 0.9).
"New domains" = test phishing whose registered domain never appeared as phishing in fitting or validation (4,037 URLs).

| Model | PR-AUC | Recall | Recall on new domains | Flagged (Gray+Confirmed) | False positives in flagged | Auto-confirmed | Sent to review (Gray) | Review / day |
|---|---|---|---|---|---|---|---|---|
| Baseline (URL structure only) | 0.9312 | 95.2% | 94.3% | 14,415 | 9,871 (68.5%) | 3,926 (precision 100.0%) | 10,489 | 1,04,890 |
| PhishGuard URL-only (structure + brand + char n-grams) | 0.9343 | 94.5% | 93.5% | 12,131 | 7,620 (62.8%) | 3,759 (precision 100.0%) | 8,372 | 83,720 |
| PhishGuard + evidence (domain age, certificate, page snapshot) | 0.9976 | 92.2% | 90.9% | 4,407 | 6 (0.1%) | 3,923 (precision 100.0%) | 484 | 4,840 |

## Same recall, fewer false alarms
Flagged URLs and false positives at a fixed recall on the test set (single threshold, as the sponsor runs today):

| Model | Recall 90% | Recall 95% | Recall 98% |
|---|---|---|---|
| Baseline (URL structure only) | 5,000 flagged, 14% FP | 14,062 flagged, 68% FP | 83,564 flagged, 94% FP |
| PhishGuard URL-only (structure + brand + char n-grams) | 6,055 flagged, 29% FP | 13,004 flagged, 65% FP | 18,661 flagged, 75% FP |
| PhishGuard + evidence (domain age, certificate, page snapshot) | 4,299 flagged, 0% FP | 4,558 flagged, 1% FP | 5,037 flagged, 6% FP |

## Per category
Phishing rows: share caught (not Clear). Benign rows: share wrongly flagged. One column per model, same order as above.

| Category | Rows in test | Baseline | PhishGuard URL-only | PhishGuard + evidence | Auto-confirmed (PhishGuard + evidence) |
|---|---|---|---|---|---|
| BenignPlain | 71,400 | 11.6% | 9.3% | 0.0% | 1 |
| BenignLongSso | 12,822 | 0.0% | 0.1% | 0.0% | 0 |
| BenignSharePoint | 13,105 | 0.9% | 0.2% | 0.0% | 0 |
| BenignTracking | 15,425 | 0.7% | 0.7% | 0.0% | 0 |
| BenignCdn | 10,475 | 0.0% | 0.0% | 0.0% | 0 |
| BenignBrandSubdomain | 6,458 | 6.9% | 0.8% | 0.0% | 0 |
| BenignLoginPortal | 7,873 | 6.0% | 1.0% | 0.0% | 0 |
| BenignLookalikeName | 3,764 | 6.7% | 13.0% | 0.0% | 0 |
| BenignFreeHosting | 3,905 | 5.1% | 4.7% | 0.0% | 0 |
| Typosquat | 759 | 99.6% | 100.0% | 94.2% | 705 |
| Homoglyph (emerging) | 503 | 100.0% | 100.0% | 99.8% | 496 |
| Combosquat | 348 | 100.0% | 100.0% | 98.9% | 340 |
| SubdomainBrand | 203 | 100.0% | 100.0% | 94.6% | 170 |
| PathBrand | 528 | 99.4% | 100.0% | 99.4% | 518 |
| FreeHosting | 599 | 100.0% | 100.0% | 100.0% | 334 |
| OpenRedirect | 281 | 100.0% | 100.0% | 100.0% | 257 |
| UserInfoTrick | 93 | 100.0% | 100.0% | 100.0% | 92 |
| IpHost | 43 | 100.0% | 100.0% | 100.0% | 43 |
| CompromisedSite | 312 | 100.0% | 100.0% | 100.0% | 312 |
| EncodedLongUrl (emerging) | 532 | 100.0% | 96.1% | 73.3% | 292 |
| Shortener | 158 | 34.2% | 95.6% | 34.2% | 45 |
| StealthKit | 414 | 71.3% | 43.5% | 84.8% | 318 |
