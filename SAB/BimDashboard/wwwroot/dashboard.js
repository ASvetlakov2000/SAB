(function () {
    "use strict";

    var state = {
        data: null,
        catalogName: "RevitLibraryBuilder",
        sourceName: "-",
        sourceFormat: "Не определен",
        rawColumns: [],
        displayColumns: [],
        rows: [],
        filteredRows: [],
        visibleColumnIndexes: [],
        defaultVisibleColumnIndexes: [],
        filterColumnIndex: -1,
        sortColumnIndex: -1,
        sortDirection: "asc",
        columnWidths: {},
        colorRIndex: -1,
        colorGIndex: -1,
        colorBIndex: -1
    };

    var hiddenColumns = {
        filter: true,
        recordtype: true,
        include: true,
        sourcetype: true,
        sourcefile: true
    };

    var minColumnWidthPx = 90;

    function parseDashboardData() {
        var dataNode = document.getElementById("dashboard-data");

        if (!dataNode) {
            throw new Error("JSON блок dashboard-data не найден.");
        }

        var json = dataNode.textContent || dataNode.innerText || "{}";
        return JSON.parse(json);
    }

    function setText(id, value) {
        var element = document.getElementById(id);

        if (element) {
            element.textContent = value;
        }
    }

    function formatNumber(value, digits) {
        var number = Number(value || 0);

        return number.toLocaleString("ru-RU", {
            minimumFractionDigits: digits,
            maximumFractionDigits: digits
        });
    }

    function toLowerSafe(value) {
        return String(value || "").toLowerCase();
    }

    function tryParseNumber(value) {
        if (value === null || value === undefined) {
            return null;
        }

        var text = String(value).trim();

        if (!text) {
            return null;
        }

        text = text.replace(/\s+/g, "").replace(",", ".");
        var parsed = Number(text);

        if (isNaN(parsed)) {
            return null;
        }

        return parsed;
    }

    function findColumnIndex(columns, columnName) {
        var target = toLowerSafe(columnName);

        for (var i = 0; i < columns.length; i++) {
            if (toLowerSafe(columns[i]) === target) {
                return i;
            }
        }

        return -1;
    }

    function resolveSourceFormat(data) {
        if (data && data.sourceFormat && String(data.sourceFormat).trim()) {
            return String(data.sourceFormat).trim();
        }

        var sourceTypeIndex = findColumnIndex(state.rawColumns, "SourceType");

        if (sourceTypeIndex < 0) {
            return "Не определен";
        }

        for (var i = 0; i < state.rows.length; i++) {
            var value = state.rows[i][sourceTypeIndex];

            if (String(value || "").trim()) {
                return String(value).trim();
            }
        }

        return "Не определен";
    }

    function buildDisplayColumns(columns) {
        var displayColumns = [];

        for (var i = 0; i < columns.length; i++) {
            var columnName = String(columns[i] || "");
            var lowered = toLowerSafe(columnName);

            if (hiddenColumns[lowered]) {
                continue;
            }

            var label = columnName === "RowNumber" ? "№" : columnName;

            displayColumns.push({
                index: i,
                name: columnName,
                label: label
            });
        }

        var rowNumberPosition = -1;

        for (var j = 0; j < displayColumns.length; j++) {
            if (toLowerSafe(displayColumns[j].name) === "rownumber") {
                rowNumberPosition = j;
                break;
            }
        }

        if (rowNumberPosition > 0) {
            var rowNumberColumn = displayColumns.splice(rowNumberPosition, 1)[0];
            displayColumns.unshift(rowNumberColumn);
        }

        return displayColumns;
    }

    function buildDefaultVisibleColumnIndexes(displayColumns) {
        var result = [];
        var seen = {};

        var priority = [
            "№",
            "Category",
            "Family",
            "TypeName",
            "ThumbnailPath",
            "Thumbnail",
            "IconPath",
            "Name",
            "MaterialName_Old",
            "MaterialName_New",
            "Description_Old",
            "Description_New",
            "DeleteMaterial"
        ];

        for (var i = 0; i < priority.length; i++) {
            var target = toLowerSafe(priority[i]);

            for (var j = 0; j < displayColumns.length; j++) {
                var column = displayColumns[j];

                if (toLowerSafe(column.name) === target || toLowerSafe(column.label) === target) {
                    if (!seen[column.index]) {
                        seen[column.index] = true;
                        result.push(column.index);
                    }

                    break;
                }
            }
        }

        if (result.length === 0) {
            var limit = Math.min(8, displayColumns.length);

            for (var k = 0; k < limit; k++) {
                result.push(displayColumns[k].index);
            }
        }

        return result;
    }

    function normalizeRows(rawRows, columnsCount) {
        var rows = [];

        if (!Array.isArray(rawRows)) {
            return rows;
        }

        for (var i = 0; i < rawRows.length; i++) {
            var rawRow = rawRows[i];
            var normalizedRow = [];

            if (Array.isArray(rawRow)) {
                for (var j = 0; j < columnsCount; j++) {
                    normalizedRow.push(rawRow[j] === undefined || rawRow[j] === null ? "" : String(rawRow[j]));
                }
            } else {
                for (var k = 0; k < columnsCount; k++) {
                    normalizedRow.push("");
                }
            }

            rows.push(normalizedRow);
        }

        return rows;
    }

    function isColumnVisible(columnIndex) {
        for (var i = 0; i < state.visibleColumnIndexes.length; i++) {
            if (state.visibleColumnIndexes[i] === columnIndex) {
                return true;
            }
        }

        return false;
    }

    function getVisibleDisplayColumns() {
        var visible = [];

        for (var i = 0; i < state.displayColumns.length; i++) {
            var column = state.displayColumns[i];

            if (isColumnVisible(column.index)) {
                visible.push(column);
            }
        }

        return visible;
    }

    function isThumbnailColumn(columnMeta) {
        if (!columnMeta) {
            return false;
        }

        var name = toLowerSafe(columnMeta.name);
        return name === "thumbnailpath" || name === "thumbnail" || name === "iconpath";
    }

    function normalizeThumbnailSource(rawValue) {
        var value = String(rawValue || "").trim();
        var lowerValue = toLowerSafe(value);

        if (!value) {
            return "";
        }

        if (lowerValue.indexOf("data:") === 0 || lowerValue.indexOf("http://") === 0 || lowerValue.indexOf("https://") === 0 || lowerValue.indexOf("file://") === 0) {
            return value;
        }

        // Для локальных абсолютных путей Windows формируем file URI.
        if (/^[a-zA-Z]:\\/.test(value)) {
            return "file:///" + value.replace(/\\/g, "/");
        }

        return value;
    }

    function appendThumbnailFallback(container, text, fullPath) {
        var fallback = document.createElement("span");
        fallback.className = "thumbnail-empty";
        fallback.textContent = text;

        if (fullPath) {
            fallback.title = fullPath;
        }

        container.appendChild(fallback);
    }

    function renderThumbnailCell(cell, rawValue) {
        cell.className = "thumbnail-td";

        var wrapper = document.createElement("div");
        wrapper.className = "thumbnail-cell";
        cell.appendChild(wrapper);

        var sourceValue = String(rawValue || "").trim();

        if (!sourceValue) {
            appendThumbnailFallback(wrapper, "Нет миниатюры", "");
            return;
        }

        var image = document.createElement("img");
        image.className = "thumbnail-image";
        image.alt = "Миниатюра";
        image.loading = "lazy";
        image.src = normalizeThumbnailSource(sourceValue);

        image.addEventListener("error", function () {
            wrapper.innerHTML = "";
            appendThumbnailFallback(wrapper, "Файл не найден", sourceValue);
        });

        wrapper.appendChild(image);
    }

    function isColorRColumn(columnMeta) {
        if (!columnMeta) {
            return false;
        }

        return toLowerSafe(columnMeta.name) === "colorr";
    }

    function parseColorComponent(rawValue) {
        var parsed = Number(rawValue);

        if (isNaN(parsed)) {
            return 0;
        }

        parsed = Math.round(parsed);

        if (parsed < 0) {
            return 0;
        }

        if (parsed > 255) {
            return 255;
        }

        return parsed;
    }

    // Легкий fallback для таблиц LineStyles.csv: цветовая плашка по RGB.
    function renderLineColorCell(cell, row) {
        var red = parseColorComponent(row[state.colorRIndex]);
        var green = parseColorComponent(row[state.colorGIndex]);
        var blue = parseColorComponent(row[state.colorBIndex]);

        var wrapper = document.createElement("div");
        wrapper.className = "line-color-cell";

        var swatch = document.createElement("span");
        swatch.className = "line-color-swatch";
        swatch.style.backgroundColor = "rgb(" + red + "," + green + "," + blue + ")";

        var text = document.createElement("span");
        text.className = "line-color-text";
        text.textContent = red + ", " + green + ", " + blue;

        wrapper.appendChild(swatch);
        wrapper.appendChild(text);
        cell.appendChild(wrapper);
    }

    function ensureVisibleColumnsNotEmpty() {
        if (state.visibleColumnIndexes.length > 0) {
            return;
        }

        if (state.displayColumns.length > 0) {
            state.visibleColumnIndexes = [state.displayColumns[0].index];
        }
    }

    // Блок отвечает за настройку отображения видимых колонок.
    function buildColumnVisibilityMenu() {
        var menu = document.getElementById("columnVisibilityMenu");

        if (!menu) {
            return;
        }

        menu.innerHTML = "";

        for (var i = 0; i < state.displayColumns.length; i++) {
            var column = state.displayColumns[i];
            var item = document.createElement("label");
            item.className = "column-option";

            var checkbox = document.createElement("input");
            checkbox.type = "checkbox";
            checkbox.setAttribute("data-column-index", String(column.index));
            checkbox.checked = isColumnVisible(column.index);
            checkbox.addEventListener("change", onColumnVisibilityChanged);

            var text = document.createElement("span");
            text.textContent = column.label;

            item.appendChild(checkbox);
            item.appendChild(text);
            menu.appendChild(item);
        }
    }

    function syncColumnMenuChecks() {
        var menu = document.getElementById("columnVisibilityMenu");

        if (!menu) {
            return;
        }

        var checkboxes = menu.querySelectorAll("input[type='checkbox']");

        for (var i = 0; i < checkboxes.length; i++) {
            var checkbox = checkboxes[i];
            var index = Number(checkbox.getAttribute("data-column-index"));
            checkbox.checked = isColumnVisible(index);
        }
    }

    function onColumnVisibilityChanged() {
        var menu = document.getElementById("columnVisibilityMenu");

        if (!menu) {
            return;
        }

        var checkboxes = menu.querySelectorAll("input[type='checkbox']");
        var selected = [];

        for (var i = 0; i < checkboxes.length; i++) {
            var checkbox = checkboxes[i];

            if (checkbox.checked) {
                selected.push(Number(checkbox.getAttribute("data-column-index")));
            }
        }

        state.visibleColumnIndexes = selected;
        ensureVisibleColumnsNotEmpty();
        syncColumnMenuChecks();

        updateFilterColumnOptions();
        applyAndRender();
    }

    function createOption(value, text, selected) {
        var option = document.createElement("option");
        option.value = value;
        option.textContent = text;

        if (selected === true) {
            option.selected = true;
        }

        return option;
    }

    function updateFilterColumnOptions() {
        var filterColumnSelect = document.getElementById("filterColumnSelect");

        if (!filterColumnSelect) {
            return;
        }

        var visibleColumns = getVisibleDisplayColumns();
        var existingValue = state.filterColumnIndex;
        filterColumnSelect.innerHTML = "";

        filterColumnSelect.appendChild(createOption("-1", "Все видимые колонки", false));

        for (var i = 0; i < visibleColumns.length; i++) {
            var column = visibleColumns[i];
            filterColumnSelect.appendChild(createOption(String(column.index), column.label, false));
        }

        var shouldKeep = false;

        if (existingValue >= 0) {
            for (var j = 0; j < visibleColumns.length; j++) {
                if (visibleColumns[j].index === existingValue) {
                    shouldKeep = true;
                    break;
                }
            }
        }

        state.filterColumnIndex = shouldKeep ? existingValue : -1;
        filterColumnSelect.value = String(state.filterColumnIndex);
    }

    function isRowMatching(row, searchText, filterText) {
        var search = toLowerSafe(searchText).trim();
        var filter = toLowerSafe(filterText).trim();
        var visibleColumns = getVisibleDisplayColumns();

        if (search) {
            var foundInSearch = false;

            for (var i = 0; i < visibleColumns.length; i++) {
                var searchColumn = visibleColumns[i];
                var searchCell = toLowerSafe(row[searchColumn.index]);

                if (searchCell.indexOf(search) >= 0) {
                    foundInSearch = true;
                    break;
                }
            }

            if (!foundInSearch) {
                return false;
            }
        }

        if (filter) {
            if (state.filterColumnIndex < 0) {
                var foundInFilter = false;

                for (var j = 0; j < visibleColumns.length; j++) {
                    var filterColumn = visibleColumns[j];
                    var filterCell = toLowerSafe(row[filterColumn.index]);

                    if (filterCell.indexOf(filter) >= 0) {
                        foundInFilter = true;
                        break;
                    }
                }

                if (!foundInFilter) {
                    return false;
                }
            } else {
                var targetCell = toLowerSafe(row[state.filterColumnIndex]);

                if (targetCell.indexOf(filter) < 0) {
                    return false;
                }
            }
        }

        return true;
    }

    function compareRows(leftRow, rightRow) {
        var index = state.sortColumnIndex;

        if (index < 0) {
            return 0;
        }

        var left = leftRow[index] || "";
        var right = rightRow[index] || "";

        var leftNumber = tryParseNumber(left);
        var rightNumber = tryParseNumber(right);
        var result;

        if (leftNumber !== null && rightNumber !== null) {
            result = leftNumber - rightNumber;
        } else {
            result = String(left).localeCompare(String(right), "ru", { sensitivity: "base" });
        }

        return state.sortDirection === "desc" ? -result : result;
    }

    function applyAndRender() {
        var searchInput = document.getElementById("searchInput");
        var filterValueInput = document.getElementById("filterValueInput");

        var searchText = searchInput ? searchInput.value : "";
        var filterText = filterValueInput ? filterValueInput.value : "";

        var filteredRows = [];

        for (var i = 0; i < state.rows.length; i++) {
            var row = state.rows[i];

            if (isRowMatching(row, searchText, filterText)) {
                filteredRows.push(row.slice());
            }
        }

        filteredRows.sort(compareRows);
        state.filteredRows = filteredRows;

        renderTable();
        updateResultInfo();
    }

    function updateResultInfo() {
        var resultInfo = document.getElementById("resultInfo");

        if (!resultInfo) {
            return;
        }

        resultInfo.textContent = "Показано строк: " + state.filteredRows.length + " из " + state.rows.length;
    }

    function getSavedColumnWidth(columnIndex) {
        if (state.columnWidths.hasOwnProperty(columnIndex)) {
            return state.columnWidths[columnIndex];
        }

        return null;
    }

    function beginColumnResize(event, columnIndex, colElement) {
        event.preventDefault();
        event.stopPropagation();

        var startX = event.clientX;
        var initialWidth = colElement.getBoundingClientRect().width;

        function onMouseMove(moveEvent) {
            var delta = moveEvent.clientX - startX;
            var newWidth = Math.max(minColumnWidthPx, initialWidth + delta);

            state.columnWidths[columnIndex] = newWidth;
            colElement.style.width = newWidth + "px";
        }

        function onMouseUp() {
            document.removeEventListener("mousemove", onMouseMove);
            document.removeEventListener("mouseup", onMouseUp);
        }

        document.addEventListener("mousemove", onMouseMove);
        document.addEventListener("mouseup", onMouseUp);
    }

    function renderTable() {
        var container = document.getElementById("tableContainer");

        if (!container) {
            return;
        }

        container.innerHTML = "";

        if (!state.displayColumns.length) {
            container.innerHTML = '<div class="warning">В источнике нет доступных колонок.</div>';
            return;
        }

        var visibleColumns = getVisibleDisplayColumns();

        if (!visibleColumns.length) {
            container.innerHTML = '<div class="warning">Выберите хотя бы одну колонку в блоке "Видимые колонки".</div>';
            return;
        }

        var table = document.createElement("table");
        table.className = "data-table";

        var colGroup = document.createElement("colgroup");
        var colElements = [];

        for (var i = 0; i < visibleColumns.length; i++) {
            var visibleColumn = visibleColumns[i];
            var col = document.createElement("col");
            var savedWidth = getSavedColumnWidth(visibleColumn.index);

            if (savedWidth !== null) {
                col.style.width = savedWidth + "px";
            }

            colGroup.appendChild(col);
            colElements.push(col);
        }

        table.appendChild(colGroup);

        var thead = document.createElement("thead");
        var headRow = document.createElement("tr");

        for (var headerIndex = 0; headerIndex < visibleColumns.length; headerIndex++) {
            (function () {
                var column = visibleColumns[headerIndex];
                var colElement = colElements[headerIndex];

                var th = document.createElement("th");
                th.textContent = column.label;
                th.className = "sortable";

                if (state.sortColumnIndex === column.index) {
                    th.className += state.sortDirection === "asc" ? " sorted-asc" : " sorted-desc";
                }

                th.addEventListener("click", function () {
                    if (state.sortColumnIndex === column.index) {
                        state.sortDirection = state.sortDirection === "asc" ? "desc" : "asc";
                    } else {
                        state.sortColumnIndex = column.index;
                        state.sortDirection = "asc";
                    }

                    applyAndRender();
                });

                var resizer = document.createElement("div");
                resizer.className = "col-resizer";
                resizer.addEventListener("mousedown", function (resizeEvent) {
                    beginColumnResize(resizeEvent, column.index, colElement);
                });

                th.appendChild(resizer);
                headRow.appendChild(th);
            })();
        }

        thead.appendChild(headRow);
        table.appendChild(thead);

        var tbody = document.createElement("tbody");

        for (var rowIndex = 0; rowIndex < state.filteredRows.length; rowIndex++) {
            var sourceRow = state.filteredRows[rowIndex];
            var tr = document.createElement("tr");

            for (var colIndex = 0; colIndex < visibleColumns.length; colIndex++) {
                var columnMeta = visibleColumns[colIndex];
                var td = document.createElement("td");

                if (isThumbnailColumn(columnMeta)) {
                    renderThumbnailCell(td, sourceRow[columnMeta.index] || "");
                } else if (isColorRColumn(columnMeta) && state.colorRIndex >= 0 && state.colorGIndex >= 0 && state.colorBIndex >= 0) {
                    renderLineColorCell(td, sourceRow);
                } else {
                    td.textContent = sourceRow[columnMeta.index] || "";
                }

                tr.appendChild(td);
            }

            tbody.appendChild(tr);
        }

        table.appendChild(tbody);
        container.appendChild(table);

        if (state.filteredRows.length === 0) {
            var warning = document.createElement("div");
            warning.className = "warning";
            warning.textContent = "По текущим настройкам нет данных.";
            container.appendChild(warning);
        }
    }

    function initHeader(data) {
        setText("catalogName", state.catalogName);
        setText("sourceName", state.sourceName);
        setText("sourceFormat", state.sourceFormat);
        setText("generatedAt", new Date(data.generatedAt).toLocaleString("ru-RU"));

        var summary = data.summary || {};
        setText("totalElementsHeader", formatNumber(summary.totalElements, 0));
    }

    function initializeControls() {
        var searchInput = document.getElementById("searchInput");
        var filterColumnSelect = document.getElementById("filterColumnSelect");
        var filterValueInput = document.getElementById("filterValueInput");
        var resetFiltersButton = document.getElementById("resetFiltersButton");

        updateFilterColumnOptions();

        if (searchInput) {
            searchInput.addEventListener("input", applyAndRender);
        }

        if (filterColumnSelect) {
            filterColumnSelect.addEventListener("change", function () {
                state.filterColumnIndex = Number(filterColumnSelect.value || -1);
                applyAndRender();
            });
        }

        if (filterValueInput) {
            filterValueInput.addEventListener("input", applyAndRender);
        }

        if (resetFiltersButton) {
            resetFiltersButton.addEventListener("click", function () {
                if (searchInput) {
                    searchInput.value = "";
                }

                if (filterValueInput) {
                    filterValueInput.value = "";
                }

                state.filterColumnIndex = -1;
                state.visibleColumnIndexes = state.defaultVisibleColumnIndexes.slice();
                state.sortDirection = "asc";
                state.columnWidths = {};

                syncColumnMenuChecks();
                updateFilterColumnOptions();
                applyAndRender();
            });
        }
    }

    function init() {
        try {
            var data = parseDashboardData();

            state.data = data;
            state.catalogName = String(data.catalogName || "RevitLibraryBuilder");
            state.sourceName = String(data.sourceName || data.projectName || "Не указан");
            state.rawColumns = Array.isArray(data.columns) ? data.columns.slice() : [];
            state.colorRIndex = findColumnIndex(state.rawColumns, "ColorR");
            state.colorGIndex = findColumnIndex(state.rawColumns, "ColorG");
            state.colorBIndex = findColumnIndex(state.rawColumns, "ColorB");
            state.rows = normalizeRows(data.rows, state.rawColumns.length);
            state.sourceFormat = resolveSourceFormat(data);
            state.displayColumns = buildDisplayColumns(state.rawColumns);
            state.defaultVisibleColumnIndexes = buildDefaultVisibleColumnIndexes(state.displayColumns);
            state.visibleColumnIndexes = state.defaultVisibleColumnIndexes.slice();
            state.filteredRows = state.rows.slice();
            state.columnWidths = {};

            if (state.displayColumns.length > 0) {
                state.sortColumnIndex = state.displayColumns[0].index;
            }

            initHeader(data);
            initializeControls();
            buildColumnVisibilityMenu();
            applyAndRender();
        } catch (error) {
            var container = document.getElementById("tableContainer");

            if (container) {
                container.innerHTML = '<div class="warning">Ошибка инициализации dashboard: ' + error.message + '</div>';
            }
        }
    }

    init();
})();
