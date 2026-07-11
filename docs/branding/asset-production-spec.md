---
title: "ProtocolLab Brand Asset Production Specification"
---

# ProtocolLab Brand Asset Production Specification

## Delivery Model

Work in three checkpoints:

1. **Concept:** up to three black-and-white Measurement Gate explorations,
   each shown at 16, 24, 32, 64, and 128 pixels.
2. **System:** one approved mark developed into color, reverse, wordmark,
   horizontal lockup, and Incursa-endorsed lockup.
3. **Production:** final vectors, raster exports, favicon family, social
   imagery, proofs, and usage notes.

If approval is unavailable during production, fully develop only the
recommended Measurement Gate route. Keep the other routes as concept sketches
rather than producing three complete asset families.

## Required Exports

All names below are required unless the owner explicitly removes an item.

| File | Format / size | Background | Purpose |
| --- | --- | --- | --- |
| `protocol-lab-mark-color.svg` | SVG, square viewBox | transparent | Primary standalone product mark. |
| `protocol-lab-mark-white.svg` | SVG, square viewBox | transparent | Mark on dark surfaces. |
| `protocol-lab-mark-mono.svg` | SVG, square viewBox | transparent | One-color print and constrained uses. |
| `protocol-lab-logo-horizontal-color.svg` | SVG | transparent | Mark plus `ProtocolLab` wordmark. |
| `protocol-lab-logo-horizontal-white.svg` | SVG | transparent | Reversed horizontal lockup. |
| `protocol-lab-lockup-endorsed-color.svg` | SVG | transparent | Product lockup with `BY INCURSA`. |
| `protocol-lab-lockup-endorsed-white.svg` | SVG | transparent | Reversed endorsed lockup. |
| `protocol-lab-readme-header.svg` | SVG, `430 x 160` viewBox | transparent | Light-mode repository header. |
| `protocol-lab-readme-header-white.svg` | SVG, `430 x 160` viewBox | transparent | Dark-mode repository header. |
| `protocol-lab-github-avatar.png` | PNG, `512 x 512` | opaque Measurement Mist | Repository/avatar crop. |
| `protocol-lab-github-social-preview.png` | PNG, `1280 x 640`, under 1 MB | opaque Lab Ink | GitHub repository social preview. |
| `protocol-lab-social-card.png` | PNG, `1200 x 630` | opaque Lab Ink | Open Graph and general social card. |
| `protocol-lab-favicon.svg` | SVG | adaptive or neutral | Modern browser favicon. |
| `protocol-lab-favicon-light.svg` | SVG | transparent | Light browser chrome. |
| `protocol-lab-favicon-dark.svg` | SVG | transparent | Dark browser chrome. |
| `protocol-lab-favicon.ico` | ICO with `16`, `32`, and `48` px frames | transparent | Legacy/browser compatibility. |
| `protocol-lab-favicon-16.png` | PNG, `16 x 16` | transparent | Explicit small favicon. |
| `protocol-lab-favicon-32.png` | PNG, `32 x 32` | transparent | Explicit standard favicon. |
| `protocol-lab-icon-192.png` | PNG, `192 x 192` | transparent | Future web-app manifest use. |
| `protocol-lab-icon-512.png` | PNG, `512 x 512` | transparent | High-resolution application icon. |
| `protocol-lab-apple-touch-icon.png` | PNG, `180 x 180` | opaque Measurement Mist | Apple touch icon. |

[GitHub recommends](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/customizing-your-repositorys-social-media-preview)
a repository social preview of at least `640 x 320` and `1280 x 640` for best
display, in PNG/JPG/GIF under 1 MB. The separate `1200 x 630` card preserves
the common Open Graph aspect ratio.

## Social Image Composition

Use the same message on both social formats, reflowed for each crop:

- eyebrow: `CONTRACT -> RUN -> EVIDENCE`
- product: `ProtocolLab`
- headline: `Protocol measurement with claims tied to evidence.`
- footer: `PROTOCOLLAB / BY INCURSA`

Place the lockup and headline in the left 55-60 percent. Use an abstract,
low-contrast Measurement Gate or protocol-lane composition on the right. Keep
all essential text inside a 7 percent safe margin. Do not place essential
details close to the center crop, and do not use invented metrics.

## Avatar And Icon Composition

- Center the mark optically, not merely mathematically.
- Keep at least 14 percent padding around the visible mark in the 512-pixel
  avatar.
- Test square, rounded-square, and circular crops.
- Use a deliberately simplified favicon drawing at 16 pixels if the normal
  mark loses its gate or lane structure. The simplified drawing must preserve
  the same silhouette and must not become a different logo.
- Do not add a wordmark to any icon at or below 192 pixels.

## SVG Requirements

- Include a valid `viewBox` and no off-canvas artwork.
- Use paths and basic shapes only. Do not embed raster images, scripts,
  external stylesheets, external links, or font files.
- Convert public wordmarks to outlines. Retain live text only in the private
  editable master.
- Use explicit colors in production variants; do not depend on a host page's
  CSS variables.
- Add a unique `<title>` and `<desc>` to standalone identity assets. Site
  integrations may treat the image as decorative when adjacent text already
  names ProtocolLab.
- Remove editor metadata, hidden layers, unused definitions, and clipping
  paths that do not affect the visible result.
- Preserve rounded joins and strokes after outline expansion.
- Ensure the white variant uses an actual white fill/stroke and remains visible
  on Lab Ink.

## Geometry And Minimum Use

- Define `H` as the visible height of the mark.
- Keep clear space of at least `0.25H` around standalone marks and lockups.
- Use the full mark at 24 pixels or larger.
- Use the simplified favicon drawing at 16 pixels when necessary.
- Use the horizontal lockup at 140 pixels wide or larger in digital contexts.
- Do not stretch, skew, rotate, add drop shadows to, recolor, or place the mark
  inside an unrelated badge shape.

The final usage notes must document the producer's actual grid, stroke/shape
ratios, optical corrections, clear-space unit, and minimum sizes. Do not leave
those values implicit in an editable design file.

## Accessibility And Contrast Proofs

Provide a contact sheet that demonstrates:

- color mark on white and Measurement Mist;
- white mark on Lab Ink and Deep Teal;
- monochrome black and white;
- lockup legibility at minimum size;
- 16, 24, and 32 pixel icon rendering;
- square, rounded-square, and circular avatar crops;
- grayscale appearance;
- common color-vision-deficiency simulations.

The supplied palette has these relevant contrast ratios:

- Protocol Teal on white: approximately `5.47:1`;
- Deep Teal on white: approximately `7.58:1`;
- Lab Ink on white: approximately `17.85:1`;
- Incursa Indigo on white: approximately `12.37:1`;
- Signal Cyan on white: approximately `4.02:1`, so it is not approved for
  small body text on white.

Logo graphics are not evaluated like body text, but adjacent labels, taglines,
and social-card copy must meet appropriate text contrast.

## Handoff Structure

Deliver a package with this structure:

```text
protocol-lab-brand/
  README.md
  source-private/
    protocol-lab-brand-master.<native-format>
    protocol-lab-brand-master-live-type.<native-format>
  exports-public/
    svg/
    png/
    favicon/
  proofs/
    concept-sheet.pdf
    size-and-crop-proof.png
    light-dark-proof.png
    social-preview-proof.png
  metadata/
    palette.json
    asset-inventory.csv
    font-and-license-notes.md
```

The private source directory is an owner handoff, not an automatic public
repository deliverable. Public exports must work without the editable masters
or bundled fonts.

The inventory must record each filename, pixel dimensions where applicable,
SVG viewBox, color variant, intended use, source-master version, and SHA-256
hash.

## Repository Integration Map

After owner approval:

- the public contract repository receives the approved public exports and a
  README header;
- the public site replaces the corporate placeholder mark, logo, favicon, and
  social metadata with the approved ProtocolLab assets;
- the central documentation mirror consumes the source-repository assets or
  approved mirrored copies;
- internal repositories may reuse approved exports but must not become the
  public source of truth;
- the Incursa corporate site may use the endorsed lockup or a text link
  according to its existing navigation pattern.

Do not perform these integrations during concept production.
