# Stick profile comparison

Open `index.html` to compare **Radial**, **Axial**, and **Angular** side by side.
Each plot has its own configuration directly underneath. Angular includes Max zone and an independent Square output toggle. Axial output settings
are grouped into X and Y subsections. Changes affect only the visualizer.

Drag on any plot or use the shared input radius and angle sliders. Compare
mapped rings or deadzone/cardinal regions, zoom into the center, and inspect
angle and travel response curves for all three profiles. Gray shading marks
zero output; each profile's color marks cardinal-only output. Each profile can
be reset independently. Invalid configurations show an explanation and pause
only the affected profile's plots. The mapping plots require no external library.

The app retains shared inner-deadzone and square-stick settings for existing
profiles; visualizer settings are independent for comparison.

## Editing and verification

Edit `comparison.fragment.html`, then run:

```powershell
python tools/visualizer/update-export.py
dotnet run --project tests/TransformChecks -- --export-visualizer-cases artifacts/visualizer-reference-cases.json
node tests/VisualizerChecks.cjs
```

The 3,302 reference cases across 26 configurations cover all three production
mappings. Maximum floating error is 1.56e-15; 22 cases differ by one signed
16-bit encoding count at floating-point truncation boundaries. Numeric saved
app modes retain their existing IDs: Angular = 0, Axial = 1, Radial = 2.

See `THIRD_PARTY_NOTICES.md` for attribution and licensing of derived math.
