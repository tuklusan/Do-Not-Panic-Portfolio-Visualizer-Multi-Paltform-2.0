<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.

DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
Based on original work by Supratim Sanyal of SANYALnet Labs.
-->

# CR-010C Upstream Behavior Inventory

## Authority

This reopened inventory supersedes the stale completion claim recorded for
CR-010C. It was manually rescanned on 2026-08-29 against upstream commit
`2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5` before Avalonia product changes.

## Functional Inventory

| ID | Upstream source | Required behavior | Avalonia acceptance evidence |
| --- | --- | --- | --- |
| TC-01 | `src/PortfolioSaver.Core/Constants/Defaults.cs`, `src/PortfolioSaver.Core/Models/TickerGroup.cs` | The four default groups each retain a configurable default row height of 56 pixels. | Unit test and source test show a 56-pixel default and binding to the configured row height. |
| TC-02 | `src/PortfolioSaver.Render/Controls/TickerTapeControl.xaml` | Every tape has `Margin="8,3"`, `Padding="9,4"`, a 7-pixel corner radius, and vertical centering. | Source test asserts the matching Avalonia template values. |
| TC-03 | `TickerTapeControl.xaml` | The title badge is vertically centered with `Padding="7,2"`, `Margin="0,0,10,0"`, `Consolas` 12-point semibold text, and the upstream foreground/border colors. | Source test and physical screenshots show centered title badges. |
| TC-04 | `TickerTapeControl.xaml` | The clipped ticker viewport is vertically centered, 28 pixels high, and uses `Margin="4,0,4,0"`. | Source test asserts the values; physical screenshot shows no vertical drift. |
| TC-05 | `TickerTapeControl.xaml.cs` | Ticker text uses `Consolas` 15-point fixed-width text: bold symbol and semibold values. The waiting glyph alone uses `Segoe UI Emoji`. | Source test asserts each data template role and exception. |
| TC-06 | `TickerTapeControl.xaml.cs` | Fixed ticker geometry is symbol `62+2`, last value `64+2`, change `72`, separator `1+9`, and item gap `18`, totaling 230 pixels. | Existing motion/geometry tests retain the fixed-width sequence contract. |
| TC-07 | `TickerTapeControl.xaml.cs`, `src/PortfolioSaver.Render/Services/TapeAnimationController.cs` | The duplicated, elapsed-time track moves seamlessly with independent configured speed/direction and retains its position through refresh, resize, and pause/resume. | Existing ticker-motion tests and timed physical captures. |
| TC-08 | `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml(.cs)` | Four tapes start at the source-derived center-scene offset and use responsive top-margin clamping rather than a bottom-anchored layout. | Source and responsive-layout tests; small, wide, and full-screen physical captures. |
| TC-09 | `TickerTapeControl.xaml.cs`, `src/PortfolioSaver.Render/Controls/StatusBarControl.xaml`, `src/PortfolioSaver.Render/Controls/NewsFlasherControl.xaml` | Typography is intentionally mixed: ticker and compact numerical data are monospaced; branding and editorial/news faces are not globally replaced. | Source test ensures ticker data is monospaced without imposing a global font override. |

## Closure Conditions

CR-010C cannot close until the complete inventory above is mapped to the
Avalonia implementation, the mandatory closure rescan is clean, and settled
physical captures on the local Linux, Windows 10, and Windows 11 machines show
four thinner, vertically centered ticker lanes in the real product.
