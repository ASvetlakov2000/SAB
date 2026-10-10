---
name: SAB UI Review 2026-10
description: Observed native approval prototypes extending the user-selected SAB UI Lab composition
colors:
  window-background: "#F7F8FA"
  panel-background: "#FFFFFF"
  table-header-background: "#F5F7FA"
  border: "#D8DEE8"
  border-weak: "#E6EAF0"
  text: "#1F2937"
  text-secondary: "#667085"
  accent: "#0F6CBD"
  accent-hover: "#0B5EA8"
  accent-pressed: "#084B86"
  accent-light: "#EAF3FF"
  row-hover: "#F5F9FE"
  field-error: "#D92D20"
  feedback-error: "#B42318"
  field-disabled: "#F2F4F7"
typography:
  headline:
    fontFamily: Segoe UI
    fontSize: 24px
    fontWeight: 600
  title:
    fontFamily: Segoe UI
    fontSize: 16px
    fontWeight: 600
  preview:
    fontFamily: Segoe UI
    fontSize: 18px
    fontWeight: 600
  body:
    fontFamily: Segoe UI
    fontSize: 13px
    fontWeight: 400
  label:
    fontFamily: Segoe UI
    fontSize: 12px
    fontWeight: 600
  hint:
    fontFamily: Segoe UI
    fontSize: 12px
    fontWeight: 400
rounded:
  panel: 8px
  input: 5px
  checkbox: 4px
spacing:
  compact: 4px
  related: 8px
  table-inset: 10px
  field-gap: 12px
  work-gap: 14px
  panel-inset: 16px
  column-gap: 18px
  shell-inset: 24px
components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.panel-background}"
    typography: "{typography.body}"
    padding: 6px 12px
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
    textColor: "{colors.panel-background}"
  button-primary-active:
    backgroundColor: "{colors.accent-pressed}"
    textColor: "{colors.panel-background}"
  panel:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    rounded: "{rounded.panel}"
    padding: "{spacing.panel-inset}"
  input:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.input}"
    padding: 5px 8px
  input-disabled:
    backgroundColor: "{colors.field-disabled}"
  table-editor:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.input}"
    padding: 0px 5px
  table-row:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    height: 42px
  table-row-hover:
    backgroundColor: "{colors.row-hover}"
  table-row-selected:
    backgroundColor: "{colors.accent-light}"
  table-header:
    backgroundColor: "{colors.table-header-background}"
    textColor: "{colors.text-secondary}"
    typography: "{typography.label}"
    padding: 0px 10px
    height: 38px
  checkbox:
    backgroundColor: "{colors.panel-background}"
    rounded: "{rounded.checkbox}"
    size: 20px
  checkbox-selected:
    backgroundColor: "{colors.accent}"
    rounded: "{rounded.checkbox}"
    size: 20px
---

# Design System: SAB UI Review 2026-10

## Overview

**Creative North Star: "SAB working window"**

This inherited descriptive name comes from SAB UI Lab; it records observed authority rather than a new identity or a newly approved metaphor. Russian-speaking architects work with dense editable data in light native windows. White work surfaces, thin boundaries, Segoe UI and restrained blue states retain the user-selected LAB composition.

This document records the isolated programmatic WPF prototypes in `src/ReviewWindow.cs`, their shared `src/ReviewStyles.xaml` resources and `src/Motion.cs`. They target .NET Framework 4.8, x64, with WPF UI 4.3.0 Light templates. The incumbent `../SAB-UI-Lab/DESIGN.md` and `../SAB-UI-Lab/.impeccable/design.json` were read as reference and remain unchanged. Local tokens govern this prototype only; production SAB XAML and identity are outside the write boundary. Each window awaits separate user approval in `APPROVAL.md`.

**Key Characteristics:**
- Light native work surfaces with editable tables taking priority.
- Blue actions, selection and one rounded field focus border.
- Cached settings, selected-row text previews and explicit test feedback.
- Short optional opacity and render-transform motion with visible base states.

## Colors

The frontmatter records literal brushes and state values observed in this build. Its source values remain authoritative; sidecar tonal ramps serve the design panel only.

### Primary
- **SAB blue** (`accent`): primary task action, active navigation and input focus.
- **Hover blue / pressed blue** (`accent-hover`, `accent-pressed`): overridden WPF UI primary-button state resources and checkbox pointer border.
- **Selection wash** (`accent-light`): selected rows and the approval-prototype badge.

### Neutral
- **Window gray / work white** (`window-background`, `panel-background`): shell and working panels.
- **Header gray / row hover wash** (`table-header-background`, `row-hover`): table structure and pointer feedback.
- **Boundary / weak boundary** (`border`, `border-weak`): panel outlines and table separators.
- **Ink / supporting ink** (`text`, `text-secondary`): main content and secondary text.
- **Disabled field gray** (`field-disabled`): custom input disabled background.

Field validation uses `field-error` on its existing border; validation feedback uses `feedback-error` text. These are implemented state colors, not an additional brand accent. The declared success brush is unused and is not promoted into the extracted palette.

**The Shared Brush Rule.** Extend the observed SAB palette and shared resources; do not treat library defaults or unused resources as newly approved tokens.

## Typography

**Body Font:** Segoe UI throughout. The frontmatter's portable `px` units represent WPF device-independent pixels (DIP), at 96 DIP per logical inch. Native line metrics are retained; no line-height, tracking or decorative display tokens are inferred.

### Hierarchy
- **Headline:** task heading.
- **Title:** section headings and table names.
- **Preview:** selected row name or parameter assignment.
- **Body:** fields, table cells, buttons and checkbox labels.
- **Label:** semibold field labels and table headers.
- **Hint:** wrapping descriptions, context, status and preview metadata.

**The Text Preview Rule.** Show the selected row's name and contextual metadata as text. The user excluded elevation diagrams and sheet pictures from these prototypes.

## Layout

Initial native window is 1400×900 DIP; minimum is 1100×700 DIP. The shell toolbar is 62 DIP and the footer 68 DIP. Content uses the shell inset, a fixed 320-DIP settings column, the column gap, and a flexible right work area. Context sits above that split; local section navigation keeps settings within their work surface. Form scrolling is vertical and reserves a separate 12-DIP scrollbar gutter.

The right side allocates remaining height to the table, followed by a work gap and a content-sized textual preview. The preview has a 120-DIP minimum and standard panel inset. When window Height is below 790 DIP, the task description and auxiliary context/preview labels hide; preview minimum becomes 104 DIP and inset becomes 12 DIP. The name and both metadata lines remain present. This is a native compact-height adaptation, not a web/mobile breakpoint.

The LAB reference used a 280-DIP column, 20-DIP gap, 60/70-DIP toolbar/footer and fixed preview heights; this extension's code uses the measurements above. They describe a local approval prototype and do not amend the incumbent files or production guide. Pages and settings are cached in memory. Switching preserves entered values and form scroll state during the session; restarting resets demonstration data.

**The Table Priority Rule.** Preserve editable rows, readable selected results and fixed task actions while secondary settings scroll or disclose locally.

Evidence includes `outputs/verification.txt`: 24 passing checks, including compact preview bounds, focus/error states, hidden cached numeric validation, source persistence, table validation, interrupted motion and virtualization of 2,006 rules with 7 row containers realized. `REVIEW.md` records all four named fixes resolved with disposition `ship` at those fixes' scope; it does not establish whole-surface or production approval. The 192-DPI bitmap is render evidence only. Physical monitor-DPI transitions and Revit execution were not tested.

## Elevation & Depth

Owned surfaces are flat: work white against window gray, one-DIP outlines and selection fills establish grouping. No custom shadows or gradients are implemented. Native window chrome and WPF UI templates remain platform/library responsibilities and do not supply new project shadow tokens.

**The Quiet Surface Rule.** Use tonal separation and thin boundaries for work panels without adding decorative depth.

## Shapes

Panel, input and checkbox corners follow the frontmatter. Fields use one rounded border whose color changes with state. DataGrid cells provide a rounded keyboard focus border only while not editing; the cell frame hides when the editor owns focus. Standard button and ComboBox geometry remains owned by WPF UI Light templates, not a fabricated custom radius.

## Components

### Buttons and navigation

Compact WPF UI commands have a 34-DIP minimum height, body type and primary padding from the frontmatter. Icon buttons are 30×30 DIP with 5-DIP padding. Primary and active navigation labels are explicitly white; their generated TextBlock foreground binds to the owning button. Inactive instrument and settings navigation uses Transparent appearance. WPF UI supplies native templates and states. Custom pointer motion changes opacity and scale, rather than animating native button colors.

### Cards / Containers

White outlined panels use the shared panel radius and inset. Table panels remove inner padding so their toolbar, header and rows own their insets. Context panels use a 12-DIP inset. The prototype badge is a local disclosure of test status, not a new general identity component. Feedback is wrapping text in the shell status row; it is not the incumbent LAB's blue feedback card.

### Inputs / Fields

Custom native TextBox and WPF UI TextBox templates use the same rounded root border, 34-DIP minimum, body type, white background, blue caret and one-DIP outline. Focus changes that outline to blue; validation changes it to the field-error color. Default WPF focus/error overlays are suppressed to keep one frame. Labels sit above fields; multiline fields have a 72-DIP minimum. ComboBox uses WPF UI styling, a 34-DIP minimum and selected-value tooltip. Table editors add 6×4-DIP margins, horizontal editor padding and a single blue border.

### Checkboxes

Custom 20-DIP square with supporting-ink outline, blue checked fill and white vector tick. Pointer state darkens the border; keyboard focus increases its thickness from one to two DIP. Disabled opacity is 0.55. These native checkbox rules remain separate from the single-border text-field rule.

### Work Table and Subject Preview

Rows and headers use frontmatter heights. Tables use single full-row selection, editable cells, recycling and row/column virtualization. Search, selection, add/delete and selected-row textual previews operate on synthetic scalar data. Parameterization reconstructs source controls from the selected rule's stored room/element fields; its preview derives the example value and source description from that state.

The parameter editor's rules, sources, values/mappings and categories represent separate settings sections. Manual room selection remains the default. Settings configure a profile; filling model elements belongs to a separate production command. Some controls are selectable for layout review without full business behavior. Import, export, table loading and model-pick actions display placeholder feedback. Test creation validates rows and simulates completion; test saving validates the profile without persisting it to SAB. No Revit transaction or all-functions integration is claimed.

### Motion

`Motion.cs` uses CubicEase/EaseOut, `FillBehavior.Stop` and `SnapshotAndReplace`. Reveal opacity starts at 0.65 and finishes at 1. Opening/replay: 180 ms with 6-DIP vertical translation. Instrument and settings changes: 140 ms with 4 DIP. Disclosure: 150 ms with 3 DIP. Selected preview: 160-ms fade. Feedback: 140-ms fade. Button hover: opacity 1↔0.86 over 100 ms. Press: scale 0.985 in 70 ms, return to 1 in 100 ms; capture loss and enabled-state reset return in 70 ms.

Only opacity and RenderTransform properties animate; layout dimensions do not. Custom effects require both the in-app switch and Windows `ClientAreaAnimation`. Stopping or closing clears owned animations and restores opacity 1, translation 0 and scale 1, including cached pages. This governs custom motion and does not claim control of every WPF UI internal effect. The 320-ms simulated action delay is test feedback timing, not a motion token.

## Do's and Don'ts

### Do:
- **Do** preserve the user-selected LAB composition, SAB palette and Segoe UI hierarchy.
- **Do** keep one rounded field focus border and a separate form scrollbar gutter.
- **Do** derive readable textual previews from the selected demonstration data.
- **Do** retain cached in-session edits and visible base states after interrupted motion.
- **Do** disclose synthetic data, simulated actions and external-operation placeholders.

### Don't:
- **Don't** treat this document or the scoped fix verdict as user approval of any window.
- **Don't** amend production SAB or the incumbent LAB system through these local measurements.
- **Don't** add elevation diagrams or sheet pictures to these textual previews.
- **Don't** claim all selectable settings execute production business logic.
- **Don't** interpret bitmap DPI rendering as physical monitor testing or Revit integration.
