# Joystick mapping visualizer

Open [`index.html`](index.html) in a browser to compare raw input, DS4Windows, and
RLRemapper's `DecoupledAxial` left-stick mapping. No build, server, controller, or
Codex installation is needed. The charts load D3 7.9.0 from jsDelivr, so internet
access to `cdn.jsdelivr.net` is required; this is not an offline bundle.

The initial **Center recovery** preset highlights diagonal movement outside the
remapper's circular inner deadzone but inside DS4Windows' overlapping axial
strips. Drag the raw-input point or use the radius and angle sliders. Change the
settings independently, inspect the input regions, or compare the angle and
travel response plots. The visualizer does not read or change the installed app's
settings. DS4Windows byte quantization is included; its unrelated transforms are
disabled. The updated remapper uses a linear response and signed 16-bit output.

## Editing

Edit [`comparison.fragment.html`](comparison.fragment.html), then refresh the
standalone document using Python 3:

```powershell
python tools/visualizer/update-export.py
```

`index.html` includes the browser styling and state-storage wrapper. Its initial
export was generated with the bundled Visualize `scripts/render.py`; the refresh
script preserves that wrapper and replaces only the editable fragment. Commit
both the fragment and refreshed document when changing the visualizer.

## Model sources and checks

- RLRemapper: [`Transform.cs`](../../src/RlRemapper/Transform.cs), including
  `ApplyStick`, its center transition, per-axis output response, and squaring.
- DS4Windows: `DS4Windows/DS4Control/Mapping.cs` and `Xbox360OutDevice.cs` at
  [commit 43c4aed](https://github.com/ds4windowsapp/DS4Windows/tree/43c4aed36110f0f81d9f61c772804aecf0a54f68).

The exported model was checked against 23,298 C# `DecoupledAxial` reference cases
across 314 settings combinations: signed 16-bit outputs matched exactly, and
maximum floating-point difference was 1.12e-16. Another 3,972 reference cases
matched DS4Windows byte and virtual-stick outputs exactly. The production
transform checks live in [`tests/TransformChecks`](../../tests/TransformChecks).

See [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) for DS4Windows attribution
and licensing.
