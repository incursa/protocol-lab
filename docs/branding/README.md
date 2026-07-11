---
title: "ProtocolLab Brand Identity And Production Package"
---

# ProtocolLab Brand Identity And Production Package

This folder records the approved ProtocolLab identity and the brief that
produced it. The Measurement Gate mark translates ProtocolLab's declared
measurement boundaries and evidence-oriented product story into a distinct
Incursa-family identity.

The production exports live in [`assets/brand/`](../../assets/brand/). They do
not change the public contract. The source brief, production specification,
handoff prompt, and acceptance checklist remain here as design rationale and
review guidance.

## Approved Production Assets

Use the full Measurement Gate mark at 64 px and above. At 48 px and below,
use a `mark-small` export; it intentionally removes the thin inset calibration
line so the mark remains clear.

| Need | Asset |
| --- | --- |
| Primary mark | [`protocol-lab-mark-color.svg`](../../assets/brand/protocol-lab-mark-color.svg) |
| Small mark | [`protocol-lab-mark-small-color.svg`](../../assets/brand/protocol-lab-mark-small-color.svg) |
| Horizontal identity | [`protocol-lab-logo-horizontal-color.svg`](../../assets/brand/protocol-lab-logo-horizontal-color.svg) |
| Incursa-endorsed identity | [`protocol-lab-logo-endorsed-color.svg`](../../assets/brand/protocol-lab-logo-endorsed-color.svg) |
| Repository header | [`protocol-lab-readme-header.svg`](../../assets/brand/protocol-lab-readme-header.svg) and its white variant |
| Repository avatar | [`protocol-lab-github-avatar.png`](../../assets/brand/protocol-lab-github-avatar.png) |
| Social card | [`protocol-lab-social-card.png`](../../assets/brand/protocol-lab-social-card.png) |
| Browser assets | [`protocol-lab-favicon.svg`](../../assets/brand/protocol-lab-favicon.svg), `protocol-lab-favicon.ico`, and `protocol-lab-apple-touch-icon.png` |
| Design tokens | [`protocol-lab-brand-tokens.json`](../../assets/brand/protocol-lab-brand-tokens.json) and its CSS equivalent |

Black and white variants sit beside the color files. Finished exports are
governed by [`BRAND-ASSET-LICENSE.md`](../../BRAND-ASSET-LICENSE.md) and
[`TRADEMARKS.md`](../../TRADEMARKS.md), not by Apache-2.0.

## Give These Files To The Producer

1. [`creative-brief.md`](creative-brief.md) defines the audience, positioning,
   brand architecture, recommended visual direction, palette, typography, and
   exclusions.
2. [`asset-production-spec.md`](asset-production-spec.md) defines the required
   files, dimensions, variants, technical constraints, and handoff structure.
3. [`handoff-prompt.md`](handoff-prompt.md) is a self-contained brief that can
   be pasted into a design task or given to a human designer.
4. [`acceptance-checklist.md`](acceptance-checklist.md) is the review and
   release gate.

The recommended sequence is concept approval, lockup approval, production
exports, and then repository/site integration. Do not produce every raster
variant before the core mark and wordmark have been approved.

## Reference Findings

The package is based on these observed surfaces:

- Incursa presents itself as practical, evidence-oriented, and focused on
  inspectable outputs rather than abstract platform claims. Its public site
  describes Protocol Lab as evidence-driven technical work with repeatable
  checks and public documentation.
- The Incursa UI Kit establishes the corporate `n` symbol, indigo logo color,
  IBM Plex Sans/Mono typography, restrained surfaces, compact data-oriented
  layouts, and a complete light/dark token system.
- ProtocolLab is an implementation-neutral public contract and evidence
  surface. Its identity must communicate declared boundaries, measurement,
  provenance, and honest comparability without implying certification or
  benchmark authority. See the [overview](../overview.md) and
  [product boundaries](../protocol-lab/product-boundaries.md).
- The public site already has a useful product palette built around teal,
  slate/ink, pale measurement surfaces, and dense evidence presentation.
  Its current files named as ProtocolLab logo assets contain the Incursa
  corporate mark and wordmark; treat those as placeholders, not a product logo
  to refine.
- The SpecTrace production package is the breadth precedent: distinct product
  mark, horizontal color and white lockups, README headers, GitHub avatar,
  favicon, social card, accessible SVG metadata, and an explicit brand-asset
  license boundary.

## Brand Relationship

ProtocolLab should look like an Incursa product without looking like the
Incursa company logo or the SpecTrace product.

- Product name and wordmark: `ProtocolLab`.
- Corporate endorsement: `by Incursa` or a separately approved Incursa
  endorsement lockup.
- Shared family traits: precision, restrained geometry, IBM Plex typography,
  strong dark/light behavior, and evidence-oriented composition.
- Product distinction: teal-led color, a measurement-gate symbol, and
  protocol-lane imagery.

Use `Protocol Lab` only as a human-readable navigation label on an Incursa
corporate surface that already uses that spelling. Do not mix `Protocol Lab`
and `ProtocolLab` inside one asset. The formal product lockup is
`ProtocolLab`.

## Production Status

The project owner approved the Measurement Gate concept and authorized public
repository integration on 2026-07-11. The repository uses the same
conservative licensing boundary as SpecTrace: code and documentation remain
Apache-2.0, while official ProtocolLab name and logo assets are governed by
separate brand terms. This records the repository policy; it is not a claim of
trademark registration or legal clearance.

The Incursa corporate site is the public organization-positioning reference:
[incursa.com](https://incursa.com/). The UI Kit remains the source for Incursa
visual-family details, and this repository remains the source for ProtocolLab
product claims.
