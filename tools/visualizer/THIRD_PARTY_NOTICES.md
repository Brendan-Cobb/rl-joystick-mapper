# Third-party notices

## DS4Windows mapping model

The DS4Windows mapping functions in `comparison.fragment.html` and its generated
copy in `index.html` are JavaScript adaptations of:

- `DS4Windows/DS4Control/Mapping.cs`
- `DS4Windows/DS4Control/Xbox360OutDevice.cs`

Source: [ds4windowsapp/DS4Windows](https://github.com/ds4windowsapp/DS4Windows),
commit `43c4aed36110f0f81d9f61c772804aecf0a54f68`.

Copyright (C) 2023 Travis Nickles.

These portions are free software: you can redistribute them and/or modify them
under the terms of the GNU General Public License as published by the Free
Software Foundation, either version 3 of the License, or (at your option) any
later version. They are distributed without any warranty; without even the
implied warranty of merchantability or fitness for a particular purpose.

The full license is included at
[`third_party/DS4Windows-LICENSE.txt`](../../third_party/DS4Windows-LICENSE.txt).

Adaptation dated 2026-10-11: isolated the axial/radial deadzone, output-response,
square-stick, output-curve, byte-normalization, and virtual-axis scaling code for
an interactive comparison. Unrelated DS4Windows transforms and device handling
are omitted. The readable JavaScript source is included beside the export.

## D3

D3 7.9.0 is loaded from jsDelivr, rather than bundled in this repository.
Copyright 2010–2023 Mike Bostock. Licensed under the ISC license; see the
[D3 source license](https://github.com/d3/d3/blob/v7.9.0/LICENSE).
