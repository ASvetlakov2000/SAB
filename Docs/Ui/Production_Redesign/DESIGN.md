---
name: SAB approved production windows 2026-10
description: Observed native adaptation of the approved SAB working-window design
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
  field-error: "#D92D20"
  field-disabled: "#F2F4F7"
  text-disabled: "#98A2B3"
typography:
  headline:
    fontFamily: Segoe UI
    fontSize: 24px
    fontWeight: 600
  title:
    fontFamily: Segoe UI
    fontSize: 16px
    fontWeight: 600
  body:
    fontFamily: Segoe UI
    fontSize: 13px
    fontWeight: 400
  body-strong:
    fontFamily: Segoe UI
    fontSize: 13px
    fontWeight: 600
  label:
    fontFamily: Segoe UI
    fontSize: 12px
    fontWeight: 600
  hint:
    fontFamily: Segoe UI
    fontSize: 12px
    fontWeight: 400
rounded:
  work-panel: 8px
  legacy-panel: 6px
  input: 5px
  checkbox: 4px
spacing:
  compact: 4px
  related: 8px
  field-gap: 12px
  legacy-inset: 14px
  panel-inset: 16px
  column-gap: 18px
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
  button-neutral:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.input}"
    padding: 0px 14px
    height: 36px
  button-outline:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.accent}"
    typography: "{typography.body}"
    rounded: "{rounded.input}"
    padding: 0px 14px
    height: 36px
  panel:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    rounded: "{rounded.work-panel}"
    padding: "{spacing.panel-inset}"
  input:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.input}"
    padding: 0px 8px
    height: 32px
  input-disabled:
    backgroundColor: "{colors.field-disabled}"
    textColor: "{colors.text-disabled}"
  table-editor:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.input}"
    padding: 0px 4px
    height: 30px
  table-row:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    height: 40px
  table-row-selected:
    backgroundColor: "{colors.accent-light}"
  table-header:
    backgroundColor: "{colors.table-header-background}"
    textColor: "{colors.text}"
    typography: "{typography.label}"
    padding: 0px 8px
    height: 38px
  checkbox:
    backgroundColor: "{colors.panel-background}"
    rounded: "{rounded.checkbox}"
    size: 20px
  checkbox-selected:
    backgroundColor: "{colors.accent}"
    rounded: "{rounded.checkbox}"
    size: 20px
  settings-tab:
    textColor: "{colors.text-secondary}"
    typography: "{typography.hint}"
    rounded: "{rounded.input}"
    padding: 9px 9px
  settings-tab-selected:
    backgroundColor: "{colors.accent-light}"
    textColor: "{colors.accent}"
    typography: "{typography.label}"
---

# Design System: SAB approved production windows 2026-10

## Overview

**Creative North Star: "SAB working window"**

The approved SAB UI Lab direction carries into three native working windows: Interior elevations, Create views and sheets, and Parameterization settings. Light surfaces, restrained blue state feedback and readable tables support Russian-speaking architects operating dense tools in Revit. This is an observed production adaptation of the existing world, not a new visual identity.

Authority is the finished source: `SAB/UI/Styles/SABRedesignStyles.xaml`, its merged `SABWindowStyles.xaml`, `SabRedesignLayout.cs`, the two working XAML windows, and ParameterTools `SettingsWindow.cs` / `CategorySelectionControl.cs`. Paths here are relative to the SAB repository root. Scope is these approved surfaces and their reused visual rules. The isolated prototype's `Tools/UiReview/DESIGN.md` and sidecar remain its own record; this document does not refresh or replace them. Other SAB modules retain their existing system.

**Key Characteristics:**
- White native work panels against a quiet gray shell.
- Fixed settings sidebars and readable working tables.
- One rounded field border, centered vector checkbox marks and separate scrollbar gutters.
- Textual selected-result previews and short optional motion from SAB's existing service.

## Colors

The frontmatter records observed reusable brushes and explicit WPF UI accent overrides. Sidecar ramps are panel-only visualizations, not additional WPF resources.

### Primary
- **SAB blue** (`accent`): task actions, input focus, checked state and selected tab text.
- **Hover blue / pressed blue** (`accent-hover`, `accent-pressed`): primary-button state overrides; the neutral SAB template does not inherit a fabricated pressed state.
- **Selection wash** (`accent-light`): selected and hovered table rows, selected tabs and contextual blue information surfaces.

### Neutral
- **Window gray / work white** (`window-background`, `panel-background`): shell and work surfaces.
- **Header gray** (`table-header-background`): table headers and ComboBox arrow compartments.
- **Boundary / weak boundary** (`border`, `border-weak`): panel frames, grid separators and footer boundaries.
- **Ink / supporting ink** (`text`, `text-secondary`): content and supporting descriptions.
- **Disabled field gray / disabled ink** (`field-disabled`, `text-disabled`): unavailable fields and commands.

`field-error` changes the field's existing border on validation failure. Legacy warning, success, scrollbar and formula-syntax colors are not promoted into this scoped palette merely because the merged dictionary declares or uses them locally.

**The Shared Brush Rule.** Reuse SAB brushes and scoped accent overrides; a WPF UI default or a one-off status color does not become a new project token.

## Typography

**Body Font:** Segoe UI. Portable frontmatter `px` values represent WPF device-independent pixels (DIP). Native text metrics apply; no line-height or tracking tokens were introduced.

### Hierarchy
- **Headline:** task headings in elevations and parameter settings; the views/sheets window keeps its existing native title and section heading.
- **Title:** section headings, work-table titles and selected-result preview headings.
- **Body / body-strong:** fields, rows, buttons and checkbox labels; semibold content identifies rule names and selected modes.
- **Label:** table headers and field labels.
- **Hint:** descriptions, status and metadata.

Parameter identity details use local smaller text within the existing parameter template; they are supporting data, not a new general type tier. The prototype's prominent selected-name role is not carried into the working previews: the production windows render the selected names as body text.

**The Text Preview Rule.** Show result names and contextual metadata as text; retain the user's exclusion of elevation diagrams and sheet pictures from these previews.

## Layout

All three main windows open at 1400×900 DIP with a 1100×700-DIP minimum. Elevations and views/sheets use a 340-DIP left settings column; parameter rules use 320 DIP. The shared column gap separates settings from a flexible right work area. Sidebars reserve a 12-DIP gutter before their vertical scrollbar. Elevation sidebar field groups explicitly stack label and input by column using `AdaptiveSettingsGrid.ForceCompact`; this is not a new mobile layout.

Outer insets remain native per-window adaptations: elevations and parameter settings generally use 18 DIP; views/sheets uses 20 DIP horizontally. New work panels use the work-panel token and standard inset; sidebars use 12-DIP padding. Views/sheets retains 6-DIP legacy framed groups and 14-DIP padding alongside new panels. Footer rows are content-sized, preserving actions while settings and tables scroll. The prototype's fixed toolbar/footer and compact-height breakpoint are not production invariants.

Elevations keep source selection above the split. Their table is a read-only naming projection of selected contours; the selected row's name and title appear below. Views/sheets preserves editable working rows, row actions, grouping and extended selection. Its validation area is a separate 128-DIP row above the textual selected-row preview. Parameter rules preserve a 74-DIP minimum row with automatic height for parameter identity details. Selected-rule source controls live at left; the full explanation below the table wraps inside a local vertical ScrollViewer capped at 60 DIP. It remains readable through scrolling rather than truncation.

**The Table Priority Rule.** Keep task actions and selected-result text reachable while settings and working data scroll independently; retain each table's existing editing and selection semantics.

Actual-class WPF captures are in `outputs/redesign-live` and `outputs/redesign-parameterization`, including minimum-size states. Existing native tests passed for layout, focus, validation, centered marks, gutters, repeated motion and reduced motion; the finish reviewer gave `ship` after the final explanation-scroll fix. See `Docs/Ui/Redesign_2026-10-10_Audit.md` for the verification scope. These checks instantiate working UI with test data outside Revit and do not prove model creation or physical monitor-DPI transitions.

## Elevation & Depth

Owned work surfaces are flat. White against window gray, thin boundaries, grid lines and selection fills establish hierarchy. No decorative shadow or gradient is introduced. Native window chrome and WPF UI template effects remain platform/library responsibilities.

**The Quiet Surface Rule.** Use tonal separation and thin boundaries to group work; do not add decorative depth to these panels.

## Shapes

New panels, inherited subpanels, fields and checkboxes retain distinct frontmatter radii; do not flatten the observed 8/6-DIP panel distinction into a universal corner token. A text field has one rounded one-DIP frame. Focus changes its color; validation changes the same frame. DataGrid cell focus is hidden while the text editor owns editing. ComboBox keeps the shared SAB outer rounded border and a separate arrow compartment; its editable inner TextBox is borderless.

The custom checkbox is a 20-DIP box with a normalized 12×8-DIP vector tick centered horizontally and vertically. Indeterminate state uses a centered 10×2-DIP bar. A checkbox's two-DIP keyboard focus border is its own accessibility state, separate from the one-frame text-field rule.

## Components

### Buttons

The primary task action uses WPF UI Light with scoped accent overrides, body type and a 34-DIP minimum height. Explicit label binding retains readable primary/disabled text. Existing SAB neutral and outline buttons keep their 36-DIP baseline, native custom template and compact radii; local row buttons remain smaller. Neutral hover uses header gray; outline hover uses selection wash. The primary WPF UI template's geometry and internal effects remain library-owned.

### Cards / Containers

New white work panels use one-DIP boundaries and the work-panel radius. Sidebars reduce padding, while existing views/sheets expanders retain the legacy panel form. They are durable coexisting native patterns, not a claim that every SAB window was migrated. Context/status frames report selection or validation; their shape is not an invitation to add ornamental badges.

### Inputs / Fields

Native TextBox uses the frontmatter height and horizontal inset, blue caret/selection, a white background and one rounded frame. Focus/error templates suppress extra overlays. Disabled fields use disabled resources. The DataGrid text editor uses its own height, inset and 10×4-DIP margin. Views/sheets inline editors preserve their current edit behavior. Formula editors and multiline fields use local content-specific sizing rather than a fabricated global field height.

### Checkboxes

The scoped dictionary supplies the centered vector mark, checked fill and indeterminate bar. Pointer state changes the existing border to accent; keyboard focus uses a dark two-DIP border. Disabled opacity is 0.55. Labels have an eight-DIP gap; contentless table checkboxes remove that gap and center the control.

### Settings navigation

Tabs use rounded, transparent resting surfaces and supporting text; selected tabs use selection wash with accent semibold labels. Hover uses disabled-field gray. Keyboard focus outlines the existing tab border in ink. This is local settings navigation, not the prototype's instrument switcher.

### Work Table and Subject Preview

Shared rows use the frontmatter baseline and headers, thin horizontal/vertical separators, ink text and selection wash for both pointer and selection. Parameter rule rows expand to fit their existing details. Elevations, views/sheets and rules retain their distinct selection/edit semantics. `SabRedesignLayout.WatchPreview` pulses the textual preview on selected-row changes; parameter explanation updates call the same service.

The categories surface keeps searchable extended selection, 38×22-DIP toggles and a six-DIP list frame. The selected-only filter shows enabled categories; it intersects with search without changing the profile. This is a domain control, not a replacement for the common checkbox.

### Motion

Production reuses `SabWindowAnimationService`, rather than copying prototype timings. Entrance is a 150-ms fade from 0.85 to 1. Tabs, disclosures and selected textual previews use the existing 170-ms pulse from 0.86 of base opacity and +2 DIP to their base values. Button hover scales to 1.012 over 90 ms, returns over 110 ms, and press scales to 0.985 over 70 ms with a 90-ms return. These are render-transform/opacity effects; layout dimensions are not animated.

The service requires both `Enabled` and Windows `ClientAreaAnimation`, uses CubicEase/EaseOut, `SnapshotAndReplace` and `FillBehavior.Stop`. Reduced-motion state leaves content available. Native WPF UI internal effects are outside this service's ownership. No animation calls the Revit API.

## Do's and Don'ts

### Do:
- **Do** preserve the approved SAB palette, Segoe UI hierarchy and each working window's task semantics.
- **Do** reuse one rounded field frame and keep a separate form scrollbar gutter.
- **Do** center the normalized vector check independently of its original path bounds.
- **Do** keep selected-result previews textual and make complete rule explanations reachable by local scrolling.
- **Do** reuse SAB's existing motion service and honor its reduced-motion gates.

### Don't:
- **Don't** apply this scoped system to unrelated SAB windows without their own authorization.
- **Don't** return elevation diagrams or sheet illustrations to the textual previews.
- **Don't** infer prototype timing, preview-name sizing or fixed chrome dimensions as production tokens.
- **Don't** claim that native UI tests or a `ship` design verdict verify Revit model operations.
- **Don't** promote inherited Unicode icon substitutions or one-off formula/status colors into reusable visual rules.
