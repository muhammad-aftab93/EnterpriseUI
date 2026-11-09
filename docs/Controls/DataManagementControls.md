# Data Management Controls

Fifteen controls for showing, editing, filtering and analysing data in WinForms. They are drawn with GDI+ from one shared visual language, so a grid, a list, a tree, an inspector or a pivot look like parts of the same product.

| Control | Class | Use it for |
| --- | --- | --- |
| GridView | `EnterpriseDataGrid<T>` | Tabular data with sorting, grouping, filtering and editing |
| VirtualGrid | `EnterpriseVirtualGridControl` | Computed or remote data with a million rows or more |
| ListControl | `EnterpriseListControl` | A virtualized list bound to any collection |
| CheckedListBox | `EnterpriseCheckedListBoxControl` | A list with check boxes |
| ListView | `EnterpriseListViewControl` | Details, list and tile views with columns and groups |
| TreeView | `EnterpriseTreeViewControl` | Hierarchies with tri-state checks and lazy loading |
| PropertyGrid | `EnterprisePropertyGridControl` | Inspecting and editing the properties of an object |
| DataEntry | `EnterpriseDataEntryControl` | A validated edit form generated from a class |
| DataLayout | `EnterpriseDataLayoutControl` | Laying out any controls in groups and columns |
| CardView | `EnterpriseCardViewControl` | Cards for people, products or records |
| FilterView | `EnterpriseFilterViewControl` | Faceted filtering with counts |
| DataFilter | `EnterpriseDataFilterControl` | Building nested AND / OR / NOT filters |
| PivotGrid | `EnterprisePivotGridControl` | Cross-tabs with subtotals and collapsible groups |
| TreeMap | `EnterpriseTreeMapControl` | Area-proportional hierarchies with drill-down |
| HeatMap | `EnterpriseHeatMapControl` | Matrices of values on a color scale |

All of them live in `EnterpriseUI.WinForms` and can be dropped on a form from the toolbox. The WinFormsDemo application has one page for each control under **Data Management Controls**.

## Shared behaviour

- **Look**: 1 px borders, 4 px corners, a 2 px focus ring, a tinted hover and selection with a left accent bar, drawn chevrons, check boxes and scroll bars. `Chrome` holds the drawing helpers and `ControlColors` the palette.
- **Theming**: colors come from the active theme (`ThemeManager`), update when the theme changes and switch to system colors in Windows high contrast.
- **DPI**: all sizes scale with the control's DPI.
- **Virtualization**: scrolling controls paint and hit-test only what is visible.
- **Keyboard**: arrow keys, Home / End, PageUp / PageDown, Space and Enter work in every control; the sections below list what is specific.
- **Models**: the logic of several controls is available without WinForms in `EnterpriseUI.Grids`: `PivotEngine`, `TreeMapLayout`, `FacetModel<T>`, `DataFilterModel<T>`, `PropertyGridModel` and `EntityFormModel`.

## GridView and VirtualGrid

`EnterpriseDataGrid<T>` is described in the [grid guides](../Grid/ArchitectureGuide.md). `EnterpriseVirtualGridControl` never materializes rows:

```csharp
var grid = new EnterpriseVirtualGridControl { UseSampleData = false, RowCount = 5_000_000, ColumnCount = 8 };
grid.ColumnTitles.AddRange(new[] { "Id", "Name" });
grid.CellValueNeeded += (s, e) => e.Value = Compute(e.RowIndex, e.ColumnIndex);
grid.CellValuePushed += (s, e) => Store(e.RowIndex, e.ColumnIndex, e.Value);
```

F2, Enter or typing starts editing; Enter commits and moves down, Escape cancels. `SelectCell` jumps anywhere. Content height is limited to about two billion pixels, which is roughly 70 million rows at the default row height.

## ListControl and CheckedListBox

```csharp
var list = new EnterpriseListControl { DisplayMember = "Name", ValueMember = "Id", SelectionMode = ListSelectionMode.MultiExtended };
list.DataSource = employees;                    // lists, binding lists and observable collections
list.Filter = item => ((Employee)item).Active;
list.SortComparison = (a, b) => string.Compare(((Employee)a).Name, ((Employee)b).Name);
```

Typing selects the next item that starts with the typed text. `ItemFormatting` changes the text, colors and weight of an item. `EnterpriseCheckedListBoxControl` adds `CheckOnClick`, `GetItemChecked`, `SetItemChecked`, `CheckAll`, `UncheckAll`, `InvertChecks`, `CheckedItems` and the `ItemCheck` event.

## ListView

Columns bind to properties through their field name; header clicks sort (numbers and dates sort by value), the edge of a header resizes and a double-click on the edge fits the column.

```csharp
view.AddColumn("Customer", 180, "Customer");
view.AddColumn("Total", 110, "Total", ContentAlignment.MiddleRight);
view.DataSource = orders;
view.View = ListViewMode.Tiles;                // Details, List or Tiles
view.GroupSelector = item => item.SubItems[0];
```

## TreeView

```csharp
var node = tree.Nodes.Add("Archive");
node.HasChildrenHint = true;                    // shows the expander before any child exists
tree.NodeExpanding += (s, e) => { if (e.Node.Nodes.Count == 0) Load(e.Node); };
tree.CheckBoxes = true;                         // checks cascade down and show a mixed state on parents
tree.LoadHierarchy(roots, r => r.Name, r => r.Children);
```

Right and Left expand, collapse and move between parent and child; `*` expands everything below the selected node.

## PropertyGrid

```csharp
grid.SelectedObject = settings;
grid.PropertyValueChanged += (s, entry) => Save();
```

Browsable properties appear by category or alphabetically; `SearchText` filters by name, description and value. Booleans toggle with a click or Space, enums and standard-value properties open a drop-down, text and numbers use an inline editor that rejects invalid input and shows the reason in the description pane, nested objects expand, and Delete resets a property to its `[DefaultValue]`.

## DataEntry and DataLayout

`EnterpriseDataEntryControl` generates one editor per public property from `DataAnnotations` (`Display`, `Required`, `StringLength`, `Range`, `EmailAddress`, `DataType`). Edits stay pending: `Apply` validates and writes them to the object, `ResetChanges` discards them.

```csharp
entry.Entity = profile;
entry.Applied += (s, e) => repository.Save(profile);
entry.ValidationFailed += (s, errors) => ShowErrors(errors);
```

`EnterpriseDataLayoutControl` positions any controls:

```csharp
layout.AddItem("Name", new EnterpriseInputControl(), group: "Contact", required: true);
layout.AddItem("Notes", new EnterpriseInputControl { Multiline = true }, "Contact", columnSpan: 2, height: 90);
layout.LabelPosition = DataLayoutLabelPosition.Left;
```

The editors the two controls use are available on their own: `EnterpriseInputControl`, `EnterpriseDropDownControl` and `EnterpriseCheckControl`.

## CardView

```csharp
cards.TitleField = "Name";
cards.SubtitleField = "Department";
cards.BadgeField = "Status";
cards.Fields.Add(new CardField("Salary", format: "C0"));
cards.DataSource = employees;
cards.GroupField = "Department";
cards.FilterText = "eng";
```

`CardFormatting` changes the title, badge, accent color and image of a card. Check boxes, multi-selection and arrow-key navigation work like in the list controls.

## FilterView and DataFilter

```csharp
filter.AddListFacet("Region");
filter.AddRangeFacet("Price");
filter.DataSource = products;
filter.FilterChanged += (s, e) => grid.DataSource = filter.Apply(products).ToList();
```

Values inside one facet are OR-ed, facets are AND-ed, and the count next to a value shows how many items remain when it is selected. `ToDescriptors()` converts the selection to filter descriptors for the grid or a server query.

```csharp
var builder = new EnterpriseDataFilterControl { ItemType = typeof(Order) };
builder.AddCondition();
var descriptor = builder.ToDescriptor();        // NOT is pushed down with De Morgan's laws
var text = builder.FilterText;                  // (Region is equal to EMEA AND Total is greater than 100)
var edited = EnterpriseDataFilter.Edit(owner, typeof(Order), descriptor);   // modal dialog
```

## PivotGrid

```csharp
pivot.RowFields.Add("Region");
pivot.RowFields.Add("Product");
pivot.ColumnFields.Add("Status");
pivot.Values.Add(new PivotValueField("Total", AggregateFunction.Sum, "Revenue", "C0"));
pivot.DataSource = orders;
```

Headers stay in place while the values scroll. A chevron in a row label collapses the group to its subtotal; `CollapseAll` and `ExpandAll` do it for the outermost groups.

## TreeMap and HeatMap

```csharp
map.Bind(orders, nameField: "Customer", valueField: "Total", colorField: "Quantity", "Region", "Product");
map.ColorMode = TreeMapColorMode.Sequential;    // Category, Sequential or Diverging
map.DrillInto(item); map.DrillUp();             // double-click and Backspace do the same

heat.Bind(orders, rowField: "Region", columnField: "Product", valueField: "Total", HeatMapAggregate.Sum);
heat.Scale = HeatMapScale.Diverging;
```

Tooltips show the path, value and share of the parent (tree map) or the exact value (heat map). Clicking a heat map column header sorts the rows by that column.

## Accessibility

Every control is keyboard operable, exposes an accessible role and draws a visible focus indicator. The colors keep the contrast of the theme and switch to system colors in high contrast. Pointer-only gestures (header resizing, splitter dragging, drill-down by double-click) have keyboard or API equivalents.
