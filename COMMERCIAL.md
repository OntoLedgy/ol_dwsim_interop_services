# Commercial Licensing

Copyright (c) 2018-2026 OntoLedgy Ltd. All rights reserved.

`ol_dwsim_interop_services` is offered under a dual-license model **for the
portions of this repository authored by OntoLedgy Ltd.**:

1. **Open source** — the GNU Affero General Public License, version 3 or later
   (AGPLv3+), as stated in the [LICENSE](LICENSE) file.
2. **Commercial** — a separate proprietary license offered by OntoLedgy Ltd.
   for OntoLedgy's contributions only. See "Scope" below.

## Scope of the commercial license — important

This repository is the DWSIM adapter for the OntoLedgy thermodynamics
architecture. It links to and combines with **DWSIM**, an upstream open-source
chemical process simulator licensed under **GPL-3.0**. DWSIM is **not** owned
by OntoLedgy Ltd. and is **not** relicensed by this commercial offer.

The commercial license offered here covers **only** OntoLedgy's own
contributions in this repository, including but not limited to:

- The Python MCP server (`mcp_service/server/`) and its `SimulatorAdapter`
  implementation against `ol_simulator_interop_services`.
- The `DwsimWorker` C# project source code authored by OntoLedgy (i.e. the
  glue between the Python server and DWSIM .NET assemblies).
- Build scripts, tests, configuration, documentation, and other
  OntoLedgy-authored material in this repository.

The commercial license **does not** cover and **does not** purport to
relicense:

- DWSIM itself (https://github.com/DanWBR/dwsim) — licensed under GPL-3.0 by
  its authors.
- Any compiled artifact (e.g. `DwsimWorker.dll`) to the extent it embeds or
  statically/dynamically links DWSIM .NET assemblies — such artifacts are
  combined works subject to GPL-3.0 obligations.

If you require a fully proprietary deployment, you have three practical
paths:

1. **Obtain commercial terms for DWSIM separately** from the DWSIM project
   maintainers, in addition to OntoLedgy's commercial license for this
   repository's contributions.
2. **Comply with DWSIM's GPL-3.0 obligations** for the DWSIM-linked
   components while using OntoLedgy's commercial license for the rest.
3. **Use a non-DWSIM backend** behind the `SimulatorAdapter` protocol from
   `ol_simulator_interop_services` (for example a future native Rust thermo
   kernel) and license that adapter independently.

## When you need a commercial license

You likely need a commercial license for OntoLedgy's contributions if you
want to do any of the following without releasing your modifications under
the AGPLv3:

- Embed OntoLedgy's contributions, or a derivative work of them, in a
  proprietary product (subject to the DWSIM scope note above).
- Offer OntoLedgy's contributions as a hosted or SaaS service without
  making the full source publicly available under AGPLv3.
- Distribute OntoLedgy's contributions bundled with proprietary code that
  constitutes a combined or derivative work under AGPLv3.
- Use OntoLedgy's contributions in a way that would otherwise require
  compliance with AGPLv3 obligations that are incompatible with your
  internal policies, regulatory requirements, or third-party contractual
  commitments.

If your usage does not trigger the AGPLv3 distribution or network-use
obligations — for example, purely internal evaluation, or contributions
back to this project under AGPLv3 — you do not need a commercial license.

## Warranty and liability

Neither license grants any warranty. The commercial license may, at
OntoLedgy Ltd's discretion, include separately negotiated warranty,
indemnification, and support terms — limited in any case to OntoLedgy's
contributions and excluding DWSIM itself.

## Contact

To request a commercial license, discuss custom terms, or clarify whether
your use case requires one, contact:

**OntoLedgy Ltd** — licensing@ontoledgy.io

Please include a brief description of your intended use, your organisation,
and the approximate deployment scale so we can respond with relevant terms.
