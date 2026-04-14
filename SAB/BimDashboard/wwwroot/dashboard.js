(function () {
    "use strict";

    var state = {
        data: null,
        sourceType: "-",
        rawColumns: [],
        displayColumns: [],
        rows: [],
        filteredRows: [],
        sortColumnIndex: -1,
        visibleColumnIndexes: [],
        defaultVisibleColumnIndexes: []
    };

    var hiddenColumns = {
        filter: true,
        recordtype: true,
        include: true,
        sourcetype: true
    };

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

    function createOption(value, text) {
        var option = document.createElement("option");
        option.value = value;
        option.textContent = text;
        return option;
    }

    function fillSortSelect() {
        var sortColumnSelect = document.getElementById("sortColumnSelect");

        if (!sortColumnSelect) {
            return;
        }

        sortColumnSelect.innerHTML = "";

        for (var i = 0; i < state.displayColumns.length; i++) {
            var column = state.displayColumns[i];
            sortColumnSelect.appendChild(createOption(String(column.index), column.label));
        }

        if (state.displayColumns.length > 0) {
            var firstIndex = state.displayColumns[0].index;
            state.sortColumnIndex = firstIndex;
            sortColumnSelect.value = String(firstIndex);
        }
    }

    function initializeControls() {
        fillSortSelect();

        var searchInput = document.getElementById("searchInput");
        var sortColumnSelect = document.getElementById("sortColumnSelect");
        var resetFiltersButton = document.getElementById("resetFiltersButton");

        if (searchInput) {
            searchInput.addEventListener("input", applyAndRender);
        }

        if (sortColumnSelect) {
            sortColumnSelect.addEventListener("change", function () {
                state.sortColumnIndex = Number(sortColumnSelect.value || -1);
                applyAndRender();
            });
        }

        if (resetFiltersButton) {
            resetFiltersButton.addEventListener("click", function () {
                if (searchInput) {
                    searchInput.value = "";
                }

                state.visibleColumnIndexes = state.defaultVisibleColumnIndexes.slice();
                syncColumnMenuChecks();

                if (state.displayColumns.length > 0) {
                    state.sortColumnIndex = state.displayColumns[0].index;

                    if (sortColumnSelect) {
                        sortColumnSelect.value = String(state.sortColumnIndex);
                    }
                }

                applyAndRender();
            });
        }
    }

    function resolveSourceType(columns, rows) {
        var sourceTypeIndex = findColumnIndex(columns, "SourceType");

        if (sourceTypeIndex < 0) {
            return "-";
        }

        for (var i = 0; i < rows.length; i++) {
            var value = rows[i][sourceTypeIndex];

            if (String(value || "").trim()) {
                return String(value).trim();
            }
        }

        return "-";
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

        if (result.length > 10) {
            result = result.slice(0, 10);
        }

        return result;
    }

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

    function isColumnVisible(columnIndex) {
        for (var i = 0; i < state.visibleColumnIndexes.length; i++) {
            if (state.visibleColumnIndexes[i] === columnIndex) {
                return true;
            }
        }

        return false;
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

        if (selected.length === 0) {
            checkboxes[0].checked = true;
            selected.push(Number(checkboxes[0].getAttribute("data-column-index")));
        }

        state.visibleColumnIndexes = selected;
        renderTable();
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

    function isRowMatching(row, searchText) {
        var search = toLowerSafe(searchText).trim();

        if (!search) {
            return true;
        }

        for (var i = 0; i < state.displayColumns.length; i++) {
            var column = state.displayColumns[i];
            var cell = toLowerSafe(row[column.index]);

            if (cell.indexOf(search) >= 0) {
                return true;
            }
        }

        return false;
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

        if (leftNumber !== null && rightNumber !== null) {
            return leftNumber - rightNumber;
        }

        return String(left).localeCompare(String(right), "ru", { sensitivity: "base" });
    }

    function applyAndRender() {
        var searchInput = document.getElementById("searchInput");
        var searchText = searchInput ? searchInput.value : "";

        var filteredRows = [];

        for (var i = 0; i < state.rows.length; i++) {
            var row = state.rows[i];

            if (isRowMatching(row, searchText)) {
                filteredRows.push(row.slice());
            }
        }

        filteredRows.sort(compareRows);
        state.filteredRows = filteredRows;

        renderTable();
        updateResultInfo();
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
            container.innerHTML = '<div class="warning">Выберите хотя бы одну колонку в левом меню.</div>';
            return;
        }

        var table = document.createElement("table");
        table.className = "data-table";

        var thead = document.createElement("thead");
        var headRow = document.createElement("tr");

        for (var i = 0; i < visibleColumns.length; i++) {
            (function () {
                var column = visibleColumns[i];
                var th = document.createElement("th");
                th.textContent = column.label;
                th.className = "sortable";

                th.addEventListener("click", function () {
                    state.sortColumnIndex = column.index;

                    var sortColumnSelect = document.getElementById("sortColumnSelect");
                    if (sortColumnSelect) {
                        sortColumnSelect.value = String(column.index);
                    }

                    applyAndRender();
                });

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
                td.textContent = sourceRow[columnMeta.index] || "";
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

    function updateResultInfo() {
        var resultInfo = document.getElementById("resultInfo");

        if (!resultInfo) {
            return;
        }

        resultInfo.textContent = "Показано строк: " + state.filteredRows.length + " из " + state.rows.length;
    }

    function initHeaderAndSummary(data) {
        setText("projectName", "Проект: " + (data.projectName || "Без названия"));
        setText("generatedAt", "Сформирован: " + new Date(data.generatedAt).toLocaleString("ru-RU"));
        setText("sourceTypeValue", "Источник: " + state.sourceType);

        var summary = data.summary || {};
        setText("totalElements", formatNumber(summary.totalElements, 0));
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

    function init() {
        try {
            var data = parseDashboardData();

            state.data = data;
            state.rawColumns = Array.isArray(data.columns) ? data.columns.slice() : [];
            state.rows = normalizeRows(data.rows, state.rawColumns.length);
            state.sourceType = resolveSourceType(state.rawColumns, state.rows);
            state.displayColumns = buildDisplayColumns(state.rawColumns);
            state.defaultVisibleColumnIndexes = buildDefaultVisibleColumnIndexes(state.displayColumns);
            state.visibleColumnIndexes = state.defaultVisibleColumnIndexes.slice();
            state.filteredRows = state.rows.slice();

            initHeaderAndSummary(data);
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
