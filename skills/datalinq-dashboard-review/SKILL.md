---
name: datalinq-dashboard-review
description: Check a saved DataLinq dashboard/site view and show it to the user - compile, preview link, open in a browser tab/side panel, run dashboard-check.js, screenshots (desktop + narrow), list issues. Sub-skill of datalinq-dashboard, used in every iteration of the review loop.
---

# Dashboard review

Order (as agreed): **compile -> self-review with screenshots -> show the preview to the user**.

1. **Compile**: `verify_view` passed (or the save returned no errors).
Link is single-use and must be opened within ~2 min (then ~10 min browser session) - open it right away and request a fresh one for the user at the end.
3. **Open** in the browser tool / side panel. If no browser tool exists, give the link to the user and ask for a screenshot.
4. **Check**: run `../datalinq-dashboard/assets/dashboard-check.js` in the page. It reports JS errors, failed requests, empty tables/charts, `undefined`/`null`/`NaN` texts, horizontal overflow, broken images, Razor error output.
5. **Screenshots** at ~1400 px and ~420 px width. Look for: overlapping/clipped content, alignment, contrast, empty areas, wrong numbers/formats vs. the data sheet.
6. **Fix** obvious issues via `datalinq-dashboard-edit` (max ~3 rounds), then repeat.
7. **Report** to the user: fresh preview link, what changed, what was checked, open points. Ask "Does this look right, or what should I change?"

Never declare success from a compile alone.
