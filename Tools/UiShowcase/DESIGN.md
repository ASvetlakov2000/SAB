---
name: SAB Fluent Light — production1.1.0 «Редизайн»
description: Native WPF production system including the approved Elevations suite; installation not authorized.
colors:
  accent: "#0F6CBD"
  accent-hover: "#095B9F"
  accent-light: "#E8F2FF"
  window-background: "#F6F7FA"
  panel-background: "#FFFFFF"
  table-header-background: "#F5F7FA"
  border: "#D6DEED"
  border-weak: "#E4E9F0"
  button-border: "#E4E7ED"
  text: "#202B3C"
  text-secondary: "#5A6780"
  hover: "#F2F6FC"
  error: "#B42318"
  success: "#067647"
  warning: "#8A4B08"
  disabled-text: "#667085"
  disabled-background: "#F2F4F7"
  disabled-primary: "#A1B5CD"
  checkbox-border: "#A8B5C7"
  scrollbar-thumb: "#ABB5C4"
  scrollbar-thumb-hover: "#7B8BA1"
typography:
  title:
    fontFamily: "Segoe UI"
    fontSize: "24 DIP"
    fontWeight: 600
  section:
    fontFamily: "Segoe UI"
    fontSize: "16 DIP"
    fontWeight: 600
  body:
    fontFamily: "Segoe UI"
    fontSize: "13 DIP"
    fontWeight: 400
  field-label:
    fontFamily: "Segoe UI"
    fontSize: "13 DIP"
    fontWeight: 600
  hint:
    fontFamily: "Segoe UI"
    fontSize: "12 DIP"
    fontWeight: 400
  table-header:
    fontFamily: "Segoe UI"
    fontSize: "12 DIP"
    fontWeight: 600
  metadata:
    fontFamily: "Segoe UI"
    fontSize: "11 DIP"
    fontWeight: 400
rounded:
  control: "4 DIP"
  container: "6 DIP"
  scrollbar: "3 DIP"
  switch-track: "11 DIP"
spacing:
  cell: "10 DIP"
  inline: "8 DIP"
  section: "12 DIP"
  panel: "16 DIP"
  columns: "20 DIP"
  page: "24 DIP"
components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.panel-background}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "12,0 DIP"
    height: "34 DIP"
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
  button-neutral:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "12,0 DIP"
    height: "34 DIP"
  button-neutral-hover:
    backgroundColor: "{colors.hover}"
  text-field:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "10,0 DIP"
    height: "34 DIP"
  combo-field:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    height: "34 DIP"
  workspace-tab:
    textColor: "{colors.text-secondary}"
    fontFamily: "Segoe UI"
    fontSize: "14 DIP"
    padding: "20,8 DIP"
    minHeight: "40 DIP"
    height: "Auto"
  workspace-tab-selected:
    textColor: "{colors.panel-background}"
  panel:
    backgroundColor: "{colors.panel-background}"
    rounded: "{rounded.container}"
    padding: "{spacing.panel}"
  collection:
    backgroundColor: "{colors.panel-background}"
    rounded: "{rounded.container}"
    padding: "0 DIP"
  items-collection:
    backgroundColor: "{colors.panel-background}"
    rounded: "{rounded.container}"
    padding: "{spacing.inline}"
  table-header:
    backgroundColor: "{colors.table-header-background}"
    textColor: "{colors.text-secondary}"
    typography: "{typography.table-header}"
    padding: "10,0 DIP"
    height: "38 DIP"
  table-row:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    height: "44 DIP"
  table-row-selected:
    backgroundColor: "{colors.accent-light}"
  checkbox:
    backgroundColor: "{colors.panel-background}"
    rounded: "{rounded.control}"
    width: "18 DIP"
    height: "18 DIP"
  checkbox-checked:
    backgroundColor: "{colors.accent}"
  row-action:
    backgroundColor: "{colors.panel-background}"
    rounded: "{rounded.control}"
    padding: "0 DIP"
    width: "28 DIP"
    height: "28 DIP"
  information:
    backgroundColor: "{colors.table-header-background}"
    textColor: "{colors.text-secondary}"
    rounded: "{rounded.control}"
    padding: "{spacing.section}"
  mode-capsule:
    backgroundColor: "{colors.table-header-background}"
    textColor: "{colors.text-secondary}"
    padding: "2 DIP"
  mode-capsule-selected:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.panel-background}"
  category-switch:
    backgroundColor: "{colors.border}"
    rounded: "{rounded.switch-track}"
    width: "38 DIP"
    height: "22 DIP"
  category-switch-checked:
    backgroundColor: "{colors.accent}"
  workspace-tab-capsule:
    backgroundColor: "{colors.table-header-background}"
    padding: "2 DIP"
  workspace-tab-indicator:
    backgroundColor: "{colors.accent}"
  notification-card:
    backgroundColor: "{colors.panel-background}"
    textColor: "{colors.text}"
    rounded: "{rounded.container}"
    padding: "16 DIP"
    borderWidth: "1 DIP"
    width: "up to 416 DIP; owner monitor work area bounded"
  notification-progress:
    backgroundColor: "{colors.border-weak}"
    indicatorColor: "{colors.accent}"
    height: "2 DIP"
---

# Design System: SAB Fluent Light — production1.1.0 «Редизайн»

## Overview

**Creative North Star: "Fluent Light для настоящих функций SAB"**

The implemented direction is a light Windows engineering workspace: cool background, white working surfaces, blue actions and selection, quiet table separators, and native Segoe UI. The user's image supplies visual grammar; actual plugin fields, metadata, commands and validation supply the content. Parameterization and creation of views/sheets share components while retaining their different working layouts. The 0.5 refinement sizes mode capsules from their actual text, leaves a shared gap below tabs, aligns vertical form fields and gives each table/list its own outline. Moving native capsule tabs, grouped actions and bounded outer scroll frames remain. Individual settings cards, compact tables and framed working metadata/status remain. The latest shared requirement removes inline module descriptions and instructional prose from every current and subsequent window/template; one footer button «Инструкция» opens the module's local HTML.

The user directly authorized production migration of the accepted 0.5 layouts on 2026-10-11. [STATUS.json](../../Docs/Ui/DesignSystem/STATUS.json) is `approved_for_production_migration`, version `0.5.0-source-review`, approvedConcept `FluentLightSource`, productionMigrationAuthorized true and installationAuthorized false. Scope is exactly views/sheets main plus secondary settings, and parameterization's four tabs with actual ParameterPicker/CategorySelectionControl. This is a new authorization, not retroactive approval of older review packets. This paragraph describes the11October authorization. On12October the user additionally approved the entire shown Elevations suite and shared notification416/status16/tab40-text14 changes, production migration, normal MSI and commit+push/release1.1.0 «Редизайн». Installation remains unauthorized.

The direct request of 2026-10-12 additionally authorizes the accepted parameterization notification, extraction of the shared SAB notification template, then a correctly packaged Build-Msi.bat installer. Installation remains unauthorized. [Notification-Layout](../../Docs/Ui/DesignSystem/Notification-Layout.md) records the accepted design and current brief; mandatory checks SAB-UI-059–064 apply. The 0.5 layout and palette are retained.

The single visual standard is [Specification.md](../../Docs/Ui/DesignSystem/Specification.md); mandatory checks are [ErrorRegistry.md](../../Docs/Ui/DesignSystem/ErrorRegistry.md), execution stages [Workflow.md](../../Docs/Ui/DesignSystem/Workflow.md), and role contracts [ReviewRoles.md](../../Docs/Ui/DesignSystem/ReviewRoles.md). Apply SAB-UI-003/006/007/048 to common ownership, authority, actual-window fidelity and honest coverage.

Current resource owners are [SABFluentLight.xaml](../../SAB/UI/Styles/SABFluentLight.xaml), [Presentation.cs](../../SAB/UI/FluentLight/Presentation.cs), [FluentResources.cs](../../SAB/UI/FluentLight/FluentResources.cs) and parameter [FluentTheme.cs](../../SAB/Cls_ParameterTools/UI/FluentTheme.cs). Scoped dictionaries supply the accepted geometry directly to production windows and the test host. Legacy SmallWindows.cs Theme and global SABWindowStyles.xaml remain unchanged; Revit Application resources are not replaced. The old seven preview owners and generated overrides are historical provenance, not the active build chain. Build.ps1 now builds production SAB sources without SourcePreview.targets or Compile substitution; GenerateSourceViews.py now delegates to successor VerifyNotificationSource.py and does not generate UI.

Notifications add [NotificationMessage](../../SAB/UI/Notifications/NotificationMessage.cs), [NotificationCard](../../SAB/UI/Notifications/NotificationCard.cs), [NotificationStack](../../SAB/UI/Notifications/NotificationStack.cs) and module [ParameterNotifications](../../SAB/Cls_ParameterTools/UI/ParameterNotifications.cs). The common component uses strings and tone, not Revit DB objects or API callbacks; notification-only styles use the same scoped dictionary. SmallWindows.Toast forwards to the adapter; its legacy Theme and ReportWindow remain. This is not automatic migration of other SAB notification systems.

Fluent Light, the four initial feedback images plus fifth scrollbar image and [Refinement-0.5.md](Refinement-0.5.md) supply the accepted world and composition. No new separately rolled five-block direction contract or quality-bar artifact was supplied for refinement 0.5; historical [Redesign-0.2.md](Redesign-0.2.md) does contain a Direction contract. This is a native code-led implementation of the accepted system, without new visual variants.

[Coverage CSV](../../Docs/Ui/DesignSystem/component-coverage.csv) and [CoveragePlan.md](../../Docs/Ui/DesignSystem/CoveragePlan.md) preserve51 audited types:31 current examples,11 resource/template-only,9 missing. The previous28/11/12 count predates the captured determinate notification ProgressBar. Current-example is not all-states or in-Revit verification. The host compiles SourcePreview.cs, NotificationPreview.cs, actual-production RedesignProduction.cs and separate isolated ElevationPreview.cs/generated copies; Gallery/ReferenceView/CompositeControls0.1 remain excluded. Historical pre-migration evidence is27PNG/75PASS in screenshots-v0.5; completed migration evidence is separately27PNG/81native PASS/25source checks with scoped review ship. Notification captures/log are separate in screenshots-notifications and do not inherit those PASS. This documenter does not run or invent runtime results.

**Key Characteristics:**
- Native Windows WPF on .NET Framework 4.8; geometry uses DIP and WPF Thickness.
- Flat white working surfaces, restrained blue interaction cues and title-width capsule tabs.
- Content-sized capsule geometry, shared tab/body gap, aligned form edges, independent collection outlines, bounded scrolling and separate section cards.
- Actual production SAB windows, models, ViewModels, bindings and commands; the WPF host alone supplies synthetic DTOs without a Revit Document.
- Complete parameter metadata and a user-authorized category selection file workflow.
- One local module instruction entry in each fixed footer; field labels, metadata, validation, errors, results and factual status remain visible.

## Colors

The frontmatter records accepted SABFluentLight.xaml values; prose describes their roles without introducing another palette.

### Primary

**SAB blue:** `accent` identifies primary actions, checks, switches, focus and moving mode/tab indicators. `accent-hover` deepens primary buttons; `accent-light` fills selected table rows and dropdown items. Mode and native tab selection use the stronger accent with white text.

### Neutral

`window-background` separates the workspace from white `panel-background`; `table-header-background` also supplies working metadata/status frames and capsule tracks. `border` frames fields, cards and metadata/status, while `border-weak` separates rows. `button-border` is the lighter neutral-button outline. `text` carries data, `text-secondary` carries secondary working text, and `hover` provides a neutral interaction wash. Disabled surfaces/text and checkbox/scrollbar affordances retain their distinct template values; several are literals rather than shared brush resources.

### Semantic feedback

`error`, `success` and `warning` are declared semantic brushes. Parameter status and category import errors resolve shared brushes. Views/sheets validation still binds to actual VM-owned colors; ParameterPicker retains local metadata brushes. The sidecar records those differences as source drift, without promoting them to common semantic tokens.

**The Meaning Alongside Color Rule.** Keep validation summaries, counts and file-operation results alongside color; color alone does not explain the outcome.

## Typography

**Display and body font:** native Segoe UI. Native font metrics determine line height; there is no custom tracking or line-height scale. The hierarchy is title, section, body/field label, hint/table header, then parameter metadata, using the frontmatter roles. Titles, section headings, field labels and table headers use Semibold; ordinary data uses Normal.

The shared notification component has its own text layout: Segoe UI16 Semibold title,16 Bold outcome,12 countdown,13 selectable summary with line height20; copy blocks use Consolas12 with line height19.2. These local shared-component settings do not replace the workspace typography scale.

ParameterPicker displays the selected parameter name in a table. Its type, group behavior and GUID/ID remain in the dropdown, tooltip and selected-rule explanation. The shell badge letter is not a display-type role. Source validation uses Segoe UI Symbol; it is not the action-icon system.

**The Metadata Completeness Rule.** Keep compact table names and complete metadata together: retain details in the dropdown, tooltip and selected-rule explanation rather than deleting them.

## Layout

This is desktop geometry, not web breakpoints. Frontmatter spacing records repeated values, not named XAML spacing resources. The working rhythm is inline controls, information, cards, columns and page padding. Heading and footer remain outside bounded scrollable work areas. [Refinement-0.5.md](Refinement-0.5.md) describes the two example compositions; one layout is not imposed on every SAB window.

Parameterization preserves its four source tabs. The normal window is 1400×920 and minimum 980×820 DIP. At runtime all table rows, including rules, are 44 DIP through Presentation.Decorate; the earlier Auto/minimum configuration is superseded. ParameterPicker and its selected editor use 34 DIP. Within grids the metadata detail block is hidden; the selected-rule information frame retains it. Rule name, parameter and value-source columns have actual minimum widths of 150, 220 and 180 DIP. Rules use Presentation.TableCard: separate Grid rows for heading, toolbar, independently outlined bounded DataGrid and selected-rule details; the former instructional row has no visible content. The table itself has zero margin inside its own collection frame; that frame is inset from the card. Presentation.TableCard preserves the collection frame margin 16,0,16,12; generated main views/sheets tables use 16,0,16,16. Non-table content retains its own insets. Categories use a bounded two-column Grid without an outer ScrollViewer; the right options card is 280 DIP with a 16-DIP gap. Logical action groups come first and search is last immediately before the list. The keyboard tutorial is removed. Whole groups wrap together; the list receives remaining height and scrolls independently.

Views/sheets uses a separately scrolling 380-DIP settings sidebar, a 20-DIP column gap, bounded table and original validation beneath it. The normal window is 1480×940 and minimum 1100×760 DIP. Standard Windows chrome reduces the client area. Every semantic settings section is a separate card with a visible title and no header icon or chevron; source conditional Visibility remains intact. All ten source column definitions and dynamic browser columns survive. Compact ordinary column widths fit the initial normal viewport without horizontal overflow; minimum sizes and additional browser columns retain native horizontal scrolling. Extended full-row selection remains native source behavior; all ten business columns are preserved.

The actual secondary views/sheets SettingsWindow is also adapted: normal 1200×900, minimum 1040×760 DIP, with two bounded scroll areas and separate semantic cards. Its original conditional Visibility and bindings remain intact.

A fixed outer border surrounds each long scrolling working area; inner cards move with scrolling content, rather than each card being frozen. ScrollEdges applies a LinearGradientBrush opacity mask only where content continues: nominally 12 DIP, capped at 6% of viewport height per edge.

All DataGrids inherit 44-DIP rows and 38-DIP column headers. The source views/sheets group heading is a separate 36-DIP group row, not a column-header override. Row actions are 28×28 DIP, centered vertically, with an 8-DIP inter-button gap and outer spacing; the actions column is 88 DIP with the explicit title «Действия». Text headers/editors share the 10-DIP text edge; inline editors have zero extra margin and padding 9 beside their one-DIP border. Numeric headers and values share a centered axis: SABFluentLight templates handle both TemplateColumn and TextColumn headers and centers the visible PART_EditableTextBox with Margin 0, not just its hidden selected-item presenter.

Nested zone/floor rows put labels in Grid row 0 and fields 34/actions 28 in row 1 with a shared vertical center. In vertical StackPanel forms, Presentation.Decorate normalizes horizontal margins of ComboBox, TextBox and CheckBox to zero while retaining top/bottom spacing. ParameterPicker receives the same horizontal normalization when hosted by Grid. The production SettingsWindow Combo factory also assigns dynamically created ParameterPicker margin 0,4,0,4, so mode changes after decoration retain the common axis. Table editors and horizontal action/count rows keep their own spacing. The common TabControl SelectedBody presenter supplies a single 12-DIP visible gap below the headers; the former conditional tab-body padding is removed.

**The Bounded Table Rule.** Keep the DataGrid in a bounded viewport with its own scrollbars so the footer and operations remain reachable.

## Elevation & Depth

The templates use no shadows, blur or decorative elevation. Depth comes from cool workspace, white cards, thin borders and selection fill. Settings sections use flat independent cards; actual metadata and status have a quieter framed surface. Unified 0.1 shadows and accent strips remain superseded. Capsule movement is functional state feedback, described with the component below; the host still disables the existing SAB window animation service.

## Shapes

Controls and information frames use the control radius; panels/table containers use the container radius. Mode tracks use Auto height with a minimum of 34 DIP, inner segments a minimum of 30 and inset 2; wrapped text with vertical padding 6 increases the group height. Presentation.Capsule computes track radius as actual outer height/2 and indicator height as max(30, outer height-4), with radius indicator height/2. Initial template radius literals are implementation fallbacks, not fixed system tokens. Tab tracks share the minimum/inset and measured-radius helper; native TabItems use Auto/min40DIP,14DIP text and padding20,8; the track and indicator follow measured content. Category switches use a compact pill track and white circular knob. Every visible table/list has one independent structural outline, one DIP thick with container radius 6; existing category-list frames are reused and repeated decoration does not add another frame. Primary table frames have horizontal inset 16 within their cards. Bound ItemsControl collections, including controls inside ItemTemplate, use the same outline family with internal padding 8. The shared CollectionChrome.Enabled behavior reads the actual ItemsControl on Loaded and LayoutUpdated: when HasItems is true and ActualHeight is positive it applies padding 8 and border thickness 1; otherwise both become zero. It does not collapse the child, so later content growth remains measurable. This describes actual collection chrome, not a blanket empty-state visibility rule. Floor item rows use only a bottom separator, with no per-row rounded outline. Most other boundaries are one DIP; tables use horizontal separators without vertical grid lines or zebra fills. Native tabs use a moving capsule indicator. Table containers apply RoundedClip.Radius: actual child RectangleGeometry with radius outer-minus-one (6→5), updated on load/size/layout. Rectangular ClipToBounds alone does not provide rounded clipping. Checkmarks, dropdown arrows, add/delete actions and drag handles use native vectors. The source validation panel retains a two-DIP colored border and circular status symbol; these source-specific shapes do not define all cards.

## Components

### Result notifications

The actual NotificationCard presents «SAB · Параметризация», a textual outcome and selectable read-only summary. Optional ID and support sections have separate neutral code frames and copy actions. ID copies only the positive unique IDs prepared by the module adapter; support copies its full diagnostic/context string. Clipboard success shows «Скопировано»; failure shows «Не скопировано» plus the exception tooltip, with two-second feedback. Close28 and Escape affect one card. The transient card has no module instruction footer; SAB-UI-043 is N/A for this surface.

White surface, outline1/radius6/padding16; owner stack width416DIP bounded to work area. Code frames use radius4 and padding10,12. Header/footer stay outside the bounded scroll body; countdown12 and actual determinate ProgressBar2 show the same lifetime. Each card receives10 active seconds from monotonic time. Hover/focus/copy, incomplete visibility and unavailable owner pause; resume continues the remainder without reset. Footer displays remaining seconds; it does not introduce a new user timer setting.

One owner-bound Window keeps newest cards last (at the bottom), gap12, individual close/lifetimes and ShowActivated=false. Overflow retains every card in a bounded scrolling stack; partially/fully hidden cards do not expire. Owner monitor work area and HWND DPI conversion place it bottom-right with inset16, recomputed every250ms. Actual multi-monitor/OS DPI transitions are NOT RUN; code presence is not runtime proof. Closing the final card or disposing the owner releases resources.

The adapter reads existing command result counters, commit state and Issue data synchronously, then supplies text DTOs. The detailed ReportWindow remains, including its existing report/save/copy/selection/show functions; modeless notification callbacks do not call Revit API or transactions. Source integration is not proof that these operations ran in Revit.

Nine native captures and the current COMPLETED [log](../../Docs/Ui/DesignSystem/screenshots-notifications/verification.txt) demonstrate actual SAB.dll WPF with synthetic results: success/warning/cancelled, selection/copy/failure, long Russian content,144dpi render, stack and overflow.144dpi is offscreen rendering. Physical hover/full keyboard/accessibility, actual Revit/API22 and OS monitor/DPI transitions remain NOT RUN. The existing log contains35 PASS/COMPLETED and [independent Verdict Pass](../../Docs/Ui/DesignSystem/Notification-Review.md) gives scoped ship to the integration fix and shown WPF scenarios; successor notification-source-contract.json records47source checks. Task applicability is recorded separately by the builder; historical migration checks remain historical. Build-Msi.bat Release2022/2023/2024 and automatic extracted-CAB SHA256 payload verification passed; installationPerformed=false. Package verification is not installed Revit execution.

### Buttons and row actions

Base Button is neutral and outlined; primary uses `SabPrimaryButtonStyle`. Buttons are vertically centered, 34 DIP high with horizontal padding 12 and ordinary gap 8. Hover changes surface; pressing reduces frame opacity; disabled state changes foreground/opacity or primary fill. Compatibility names `SabOutlineButtonStyle`, `SabNeutralButtonStyle` and `SabDangerOutlineButtonStyle` still alias the fresh neutral base; their names do not establish separate danger visuals.

Row add/delete styles use compact vector actions and retain original commands. When a populated table already exposes a row plus, an extra toolbar add is hidden. An empty table retains one named plus connected to the original handler. Tables without a row plus retain their required Add action.

### Fields and dropdowns

TextBox and ComboBox use white surfaces and the structural border; focus colors the existing border blue. TextBox errors change it to the error brush and suppress the default error adorner. Disabled fields use disabled surface/text. ComboBox preserves the editable host and native popup, selected-item fill and neutral highlight. Vertical form fields align with labels and neighboring frames through zero horizontal margin; this normalization does not rewrite horizontal rows or table editors. ParameterPicker also aligns inside Grid, and the production factory preserves that alignment for dynamic replacements. In DataGrids, resting ComboBoxes and inline TextBoxes have transparent borders/backgrounds; focused editing restores one input outline.

`Focus` is a native dashed blue rounded Rectangle, margin −3, stroke 1 and dash array `2 1`, used by buttons, checks, tabs and list items. A keyboard-focused cell shows an inset border and hides it while editing. These implemented states do not establish complete keyboard traversal or accessibility verification.

### Tables, lists and independent selection

Tables and lists own a SabCollectionBorderStyle frame, or reuse their existing outlined frame. Bound ItemsControl collections use SabItemsCollectionBorderStyle, derived from that frame with padding 8 and the measured CollectionChrome behavior described in Shapes: shared SabBrush.Border, one-DIP outline, container radius and real rounded clipping. The outer section card does not substitute for a collection boundary. Tables share header/row tokens, horizontal separators, neutral hover and pale-blue selection. Cells have no permanent outlines. Virtualization is enabled and columns are explicit. Parameter checkbox headings remain locally centered. List items use bottom separators, hover and selection fills, rather than individual row cards.

TableScroll.Enabled is installed on the common DataGrid style. It retains native ScrollViewer, ScrollBar and scroll bindings, extends the column-header presenter across the scrollbar gutter, and starts the vertical bar below the visible top GroupToggle row. The uncovered group gutter receives matching table-header fill and a lower structural line. Layout updates recompute the remaining visible group height as content scrolls; the bar occupies the current row area. Native scrolling and group collapse remain functional. This does not establish sticky group rows or headers.

CheckBox preserves the source Enabled/Required meanings and mixed state. Category switches use `SabSwitchTemplate`: a 38×22-DIP track and 16-DIP white knob, moved to the right when selected. Independent choices remain independent; capsule grouping is reserved for mutually exclusive modes.

### Capsule modes

Original RadioButtons, GroupName, bindings, checked events and native automation remain intact inside `Capsule.IsGroup` grids. Shared outer/inner minima are 34/30 DIP with inset 2. Radio labels use Auto height and padding 8,6; actual wrapped content determines group height and all segments share it. The indicator and track radii follow the measured group height, so a two-line mode is not clipped or assigned a fixed enlarged height. Radio UniformGrid uses Columns=0 so a collapsed conditional option leaves no phantom segment; a single visible option spans the inner track. A blue `CapsuleIndicator` moves between the selected labels; unselected labels use secondary text. The helper tracks layout and resizing. Checked and TabControl.SelectionChanged update immediately so LayoutUpdated cannot consume the target before the animation starts. Selection changes animate TranslateTransform.X for 160 ms with CubicEase/EaseOut when `SystemParameters.ClientAreaAnimation` is enabled; otherwise the indicator snaps to its target. Structure, placement and category All/Selected use this shared pattern. This movement does not change the underlying choice or multiple-selection semantics.

### Navigation, section cards and information

Workspace tabs remain native TabControl/TabItem and occupy only their title widths. SABFluentLight supplies title-width capsule tabs with track Auto/minimum34, native TabItems Auto/min40,14DIP text, inset2 and padding20,8. Its SelectedBody presenter adds the shared 12-DIP gap; no conditional tab-body padding duplicates it. Capsule.IsGroup tracks selected visible TabItems and animates the accent indicator by the same 160-ms CubicEaseOut/ClientAreaAnimation rule. Selected header text is white; tab content remains its own surface. No strip extends across the remaining window. Shared cards are white, flat, lightly bordered and rounded. Settings sections use `SabSectionCardTemplate`, permanently expose their own header and content, and retain source visibility triggers without header icons or chevrons.

`SabInformationBorderStyle` supplies a light surface, structural border, control radius and padding 12. `Presentation.Information` carries the content Visibility to its frame; `Presentation.Decorate` frames working data tagged `SabDataExplanation`, including selected-rule metadata. Instructional text is removed without replacement copy or empty frames. Footer status occupies its own row above lower actions. `Presentation.Status` displays a proxy of the original parameter status: only ready prose becomes «Настройки готовы.»; original errors, foreground and tooltip binding remain connected, with the same shortening applied to ready tooltip text. Configuration/settings exchange sits in the fixed footer beside instruction/group title, retaining original commands; parameter labels become «Импорт»/«Экспорт», views/sheets retains source action labels. Presentation.ActionGroup supplies 12-DIP Semibold titles; categories group «Показать», «Выборки» and «Выделение» before search. Scrollbars use inset rounded thumbs and native hover/drag states in both orientations.

### Local instruction entry

Each current and subsequent window/template uses one neutral footer button «Инструкция». Parameterization retains its original handler for `SAB_HTML_Zapolnenie_parametrov.html`. Main and secondary views/sheets use attached `Instructions.File` with `SAB_HTML_Sozdat_vidy_i_listy.html`; the common resolver locates the installed local instruction tree, and the click handler uses the default shell. `FluentTheme.Frame` suppresses the subtitle and `Instructions.EnsureFooter` supplies a catalog fallback for shared dialogs without their own help entry. SAB.csproj copies local HTML instructions/assets as Content into the output. Historical preview verified file resolution; current migration checks are recorded separately, and actual external browser launch is not established here.

**The Instructions Outside Windows Rule.** Remove module descriptions and instructional prose, even short instructions, from every current and subsequent window/template. Keep field/action labels, actual metadata, validation, errors, results and factual status; provide one footer «Инструкция» button opening local module HTML.

### ParameterPicker, category files and validation

Actual ParameterPicker retains per-instance search and selection/Enter/Escape behavior; no global search view is substituted. Its selected name remains compact while complete metadata stays accessible.

The user-authorized 0.3 category extension adds All/Selected filtering and real Save/Load selection actions over `CategoryChoice.Selected`, retaining the original profile connection. This was an authorized preview addition and is now included in the approved production module scope; it was not an original pre-migration SAB feature. JSON uses `Format=SAB.CategorySelection`, `Version=1` and stable category IDs. Import replaces selection with matching available IDs, skips unknown IDs with a visible count, and rejects invalid files before changing selection. Cancelling changes nothing. Export writes UTF-8 through a temporary file and replaces the destination; file errors appear inside the control. Original mass-selection actions and search remain available.

Views/sheets validation binds directly to existing VM counts, summaries, colors and symbol. ClearRowsCommand restores one blank row. Production main/secondary code-behind and their Restore/Save geometry calls are unchanged; the host isolates secondary preferences separately. Historical `source-contract.json` stays immutable. Current `production-baseline.json`/`production-source-contract.json` preserve135 original main bindings (136 current with StatusText),103 secondary bindings,ten columns and ten business-method bodies after the sole FluentTheme.→Theme. normalization. Two raw method hashes differ only by that presentation helper rename, so raw byte identity is not claimed for those two. Category filtering/file actions are separately authorized additions. The host supplies synthetic profiles/DTOs without a Document; transactions, ExternalEvent, point selection and model results remain outside this verification.

## Do's and Don'ts

### Do:
- **Do** read canonical STATUS.json and Specification.md before applying this extraction.
- **Do** change shared visuals in SABFluentLight.xaml, FluentLight/Presentation.cs, FluentResources.cs and the scoped parameter FluentTheme.cs; build the actual production sources and verify the changed surfaces.
- **Do** preserve actual fields, bindings, commands, selection semantics and complete parameter metadata.
- **Do** use the shared table rhythm, centered row actions, native title-width capsule tabs, capsule modes and framed working metadata/status.
- **Do** keep one visible route to add a row when a row plus exists, with a named plus for an empty table.
- **Do** put search immediately before its list, group actions above, and configuration exchange in the fixed footer.
- **Do** size capsule modes from wrapped content with outer/inner minima 34/30, inset 2 and vertical text padding 6; let indicator height and radii follow measurement.
- **Do** keep the common 12-DIP tab/body gap and zero horizontal margin for vertical form fields.
- **Do** give each table/list one independent one-DIP rounded outline, reuse existing category frames and inset main table frames by 16 horizontally; use the shared padded frame for bound ItemsControl collections.
- **Do** preserve outer scroll frames, clip table corners with real geometry, and align visible numeric editors with their headers.
- **Do** keep native table scroll bindings while extending header fill over the gutter and placing the vertical bar below the visible top group row.
- **Do** distinguish native preview captures from production and in-Revit verification.
- **Do** provide one footer «Инструкция» button for local module HTML in current and subsequent window templates.

### Don't:
- **Don't** expand the explicit approved scope (two initial modules plus the12October Elevations suite) to unspecified modules/installation, or rewrite historical pending evidence as retrospectively approved.
- **Don't** inherit old production templates or revive superseded Unified 0.1 tokens.
- **Don't** invent module fields, table search or selection checkboxes from the visual reference.
- **Don't** combine independent selections into mutually exclusive capsule modes.
- **Don't** promote source VM validation colors, local metadata colors or status glyphs into shared tokens or action icons.
- **Don't** turn DIP, WPF Thickness or native triggers into pretend CSS components or web breakpoints.
- **Don't** place module descriptions or instructional prose, even short instructions, inside windows or replace removed prose with empty frames.

The historical27PNG/75PASS packet under `Docs/Ui/DesignSystem/screenshots-v0.5` belongs to the pre-migration isolated source presentation; it remains immutable. The production migration uses a separate packet under `screenshots-production-0.5` and new production baseline/source contract. This documenter checked actual resource owners, production markup/classes, build configuration, authority and existing contracts; it did not run build, WPF, capture, browser or Revit, and does not assign new runtime counts/verdicts. DTO-host captures are not evidence of installed plugin execution, Document transactions, physical input, complete accessibility or OS DPI transitions. Source VM colors, local metadata brushes and status glyphs remain disclosed source-specific drift rather than reusable tokens. CategorySelectionControl is also reused by legacy CategoryCheckWindow: its outer legacy Theme is unchanged, while effects of the migrated shared control in that dependent window require separate evidence.


История до утверждения12.10: промежуточный isolated Elevations packet565PASS/41PNG и visual proposal416DIP тогда ожидали approval; эта запись не описывает текущий production scope.


Исторический финальный approval packet:635PASS/44PNG/70ID,8functional deltas+2optional warning files при85baseline unchanged; тогда suite/proposal ещё pending. После прямого approval12.10 production migration выполнена отдельно, исторические доказательства не переносятся в новый verdict.


## Production1.1.0 extraction —12October2026

The approved9active Elevations views/classes now use the shared production dictionary: main four tabs/horizontal source panel; decoration one scrolling form with three selection cards, source tag fields in a3×2 arrangement and12DIP leader gaps; catalog shared two-column DataGrid; elastic room text columns; spatial dashed crop inputs and compact content-sized choices. RoomPlanSchemeSettingsWindow has no active caller and is excluded as an unreachable standalone surface. Main VM retains Document collectors behind a null-Document host guard. CaptureDraft/RestoreDraft and the catalog command round trip preserve unfinished raw text. Actual Revit operations and persistence remain separate runtime boundaries.

Shared notification template is416DIP work-area bounded, outcome16Bold, selectable summary in a transparent1DIP/radius4/padding10 frame; warnings/ID/support retain their separate filled copy sections. Stack placement is bottom-right/newest-bottom/grows-up with gap12, independent10active seconds and remaining-time pause/resume. Elevation final results use positive-only successful actions with all accumulated warnings in the optional warning block; parameter ReportWindow is retained. Native tabs are Auto/min40DIP with14DIP text/padding20,8; mode capsules keep the established34/30 minima.

[elevations-production-migration.json](elevations-production-migration.json) records actual before/after owners. Fresh COMPLETED logs: actual production109PASS/23PNG, shared notifications35PASS/9PNG, previous modules81PASS/27PNG, isolated sessions635PASS/44PNG under screenshots-redesign-*. They prove the listed native WPF fixture states only. Current independent verdict/MSI/release is recorded by builder separately; installation, Revit/API execution, physical input/accessibility and OS DPI transitions are not established here. DESIGN is extraction of code, not a new concept or approval source.


Текущий bounded hotfix1.1.1: [компоновка/порядокfooter](../../Docs/Ui/DesignSystem/ElevationHotfix-Layout.md), [actual captures](../../Docs/Ui/DesignSystem/ElevationHotfix.html), [verification](../../Docs/Ui/DesignSystem/ElevationHotfix-Verification.md). `Build.ps1 -ElevationHotfix` запускает actual классы с чистыми Application resources;17actualPNG,9shared notificationPNG и44source-derived sessionPNG сохраняются отдельно от1.1.0. Production включает новый read-only ElevationSelectionListWindow и immutable snapshot;2mode capsules/mainMin800, listMin740×420; footer status row0, actions row1. Установка и Revitexecution NOT RUN.


Текущие исправления параметризации1.1.2: [layout](../../Docs/Ui/DesignSystem/ParameterCheck-Layout.md), [verification](../../Docs/Ui/DesignSystem/ParameterCheck-Verification.md), [actualcaptures](../../Docs/Ui/DesignSystem/ParameterCheck.html). `Build.ps1 -ParameterChecks` — actual90/14, separateactualregression81/27 сcleanresources; source71boundedchecks. ParameterPicker gates actualedit events, CheckCategoriesWindow Fluentfooter/noaccentstub/min680×760, fullcatalog with independentCheckCategoryIds. ExistingCheckcommand/ribbonID retained; newToolkitCheckSettingsCommand; highlightingreports removed, onlyactivationnotice. NativeRevit/physical/OS/installation NOTRUN.

Numbering refinement15.10 preserves the accepted Settings Form: two equal AdaptiveSettingsGrid columns inside the naming card, existing scoped TextBox34 and label styles, separate live example wrapping at minimum height. [Actual normal/empty/minimum captures](../../Docs/Ui/DesignSystem/screenshots-elevation-numbering/provenance.json); no change to shared tokens, approval or installation authority.


Direction preview15.10: Settings Form entry stays in Parameters card, existing fixed footer preserved. Compact Dialog uses shared scoped Fluent brushes/buttons; bounded180DIP status scrolling, metadata, no inline instructions. [Layout/evidence](../../Docs/Ui/DesignSystem/ElevationDirectionPreview-Layout.md). No new shared tokens or installation authority.


15.10 runtime successor: fixed five-button footer Instruction/room list/Preview/Pick lines/Create, last3sharedprimaryblue; mainCancel removed, modeless cleanupCancel retained. [Layout](../../Docs/Ui/DesignSystem/ElevationPreviewRuntime-Layout.md). Supersedes earlier Parameters-card preview placement; shared tokens unchanged.
