# Tracker: legacy-recipe

One line per item. Status as of 2026-11-19: implemented, local commits; waiting for the owner's review.

## Done

- [x] Brief copied (`brief.md`); no product, customer or company names
- [x] Probe today's failure (macOS SDK 8.0.427, 11.0.100; real `Import.record`)
- [x] Probe sections A, B, D (ablation) on macOS; C on macOS and the Windows VM (SDK 8.0.428, 10.0.404)
- [x] SDK project through the overlay: no change but the overlay as an import input
- [x] Design report with the line budget to the orchestrator

## Open

- [x] Owner: approve the design (guard checks and message suffix; C's placement)
- [x] `docs/legacy-projects.md` (recipe verbatim, how to pass it, staleness, limits, today's failure)
- [x] Guard in `Import.fs` `evaluate`
- [x] `LegacyProjectTests.fs`: record + build (A–D), no overlay → guard, partial overlay → guard names what is missing, SDK dumps never trip it
- [x] Links (`docs/hermetic/README.md`/`index.md`), release notes "Unreleased"
- [x] `dotnet fsi build.fsx -- -- build test pack`
- [x] Windows acceptance on the VM (`windows/` kit, packed package)
- [ ] Owner: review, push, PR to `dev` (squash)
