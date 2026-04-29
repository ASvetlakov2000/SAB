(function () {
    "use strict";

    var state = {
        data: null,
        sourceProfile: "",
        rawColumns: [],
        displayColumns: [],
        rows: [],
        filteredRows: [],
        visibleColumnIndexes: [],
        filterColumnIndex: -1,
        sortColumnIndex: -1,
        sortDirection: "asc",
        columnWidthsByIndex: {}
    };

    var hiddenColumns = {
        recordtype: true,
        sourcetype: true,
        sourcefile: true,
        include: true,
        "включить": true
    };

    var profileColumnOrders = {
        systemfamilies: ["rownumber", "миниатюра", "категория", "семейство", "типоразмер", "структура", "толщина типа, мм"],
        loadablefamilies: ["rownumber", "миниатюра", "категория", "семейство", "типоразмер"],
        lines: ["rownumber", "наименование", "миниатюра", "цвет", "категория", "вес линии", "образец"],
        fillpatterns: ["rownumber", "наименование", "миниатюра", "тип штриховки"]
    };

    var fillPatternsHiddenColumns = {
        "штриховка переднего плана": true,
        foregroundpattern: true,
        "штриховка заднего плана": true,
        backgroundpattern: true,
        маскирование: true,
        ismasking: true
    };

    var columnLabelMap = {
        rownumber: "№",
        category: "Категория",
        family: "Семейство",
        typename: "Типоразмер",
        include: "Включить",
        thumbnailpath: "Миниатюра",
        thumbnail: "Миниатюра",
        iconpath: "Миниатюра",
        totalthicknessmm: "Толщина типа, мм",
        name: "Наименование",
        "цвет": "Код цвета",
        color: "Код цвета",
        codecolor: "Код цвета",
        colorswatch: "Цвет"
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

    function toLowerSafe(value) {
        return String(value || "").toLowerCase();
    }

    function getColumnLabel(columnName) {
        var lowered = toLowerSafe(columnName);

        if (columnLabelMap.hasOwnProperty(lowered)) {
            return columnLabelMap[lowered];
        }

        return String(columnName || "");
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

    function normalizeColumnName(name) {
        return toLowerSafe(name).trim();
    }

    function getProfileOrder() {
        var key = normalizeColumnName(state.sourceProfile);

        if (profileColumnOrders.hasOwnProperty(key)) {
            return profileColumnOrders[key];
        }

        return [];
    }

    function isCurrentProfile(profileKey) {
        return normalizeColumnName(state.sourceProfile) === profileKey;
    }

    function shouldHideColumnForProfile(loweredColumnName) {
        if (!loweredColumnName) {
            return false;
        }

        if (isCurrentProfile("fillpatterns") && fillPatternsHiddenColumns[loweredColumnName]) {
            return true;
        }

        return false;
    }

    function getSyntheticLineColorColumnIndex(sourceColorColumnIndex) {
        return -100000 - sourceColorColumnIndex;
    }

    function isLineProfile() {
        return isCurrentProfile("lines");
    }

    function buildDisplayColumns(columns) {
        var displayColumns = [];

        for (var i = 0; i < columns.length; i++) {
            var columnName = String(columns[i] || "");
            var lowered = normalizeColumnName(columnName);

            if (hiddenColumns[lowered]) {
                continue;
            }

            if (shouldHideColumnForProfile(lowered)) {
                continue;
            }

            displayColumns.push({
                index: i,
                name: columnName,
                loweredName: lowered,
                label: getColumnLabel(columnName)
            });
        }

        var order = getProfileOrder();

        if (order.length === 0) {
            return displayColumns;
        }

        var ordered = [];
        var used = {};

        for (var orderIndex = 0; orderIndex < order.length; orderIndex++) {
            var target = order[orderIndex];

            for (var columnIndex = 0; columnIndex < displayColumns.length; columnIndex++) {
                var column = displayColumns[columnIndex];

                if (column.loweredName === target || normalizeColumnName(column.label) === target) {
                    if (!used[column.index]) {
                        used[column.index] = true;
                        ordered.push(column);
                    }

                    break;
                }
            }
        }

        for (var i2 = 0; i2 < displayColumns.length; i2++) {
            var fallbackColumn = displayColumns[i2];

            if (!used[fallbackColumn.index]) {
                ordered.push(fallbackColumn);
            }
        }

        if (isLineProfile()) {
            ordered = injectLineColorPreviewColumn(ordered);
        }

        return ordered;
    }

    function injectLineColorPreviewColumn(columns) {
        if (!Array.isArray(columns) || columns.length === 0) {
            return columns;
        }

        var colorColumnIndex = -1;

        for (var i = 0; i < columns.length; i++) {
            var lowered = columns[i].loweredName;

            if (lowered === "цвет" || lowered === "color" || lowered === "код цвета" || lowered === "codecolor") {
                colorColumnIndex = i;
                break;
            }
        }

        if (colorColumnIndex < 0) {
            return columns;
        }

        var colorColumn = columns[colorColumnIndex];
        var syntheticIndex = getSyntheticLineColorColumnIndex(colorColumn.index);
        var syntheticColumn = {
            index: syntheticIndex,
            name: "ColorSwatch",
            loweredName: "colorswatch",
            label: "Цвет",
            isSyntheticColorPreview: true,
            sourceColorColumnIndex: colorColumn.index
        };

        var result = [];

        for (var j = 0; j < columns.length; j++) {
            if (j === colorColumnIndex) {
                result.push(syntheticColumn);
            }

            result.push(columns[j]);
        }

        return result;
    }

    function isColumnVisible(columnIndex) {
        for (var i = 0; i < state.visibleColumnIndexes.length; i++) {
            if (state.visibleColumnIndexes[i] === columnIndex) {
                return true;
            }
        }

        return false;
    }

    function findDisplayColumnMetaByIndex(columnIndex) {
        for (var i = 0; i < state.displayColumns.length; i++) {
            if (state.displayColumns[i].index === columnIndex) {
                return state.displayColumns[i];
            }
        }

        return null;
    }

    function getRowCellValueByColumnIndex(row, columnIndex) {
        var columnMeta = findDisplayColumnMetaByIndex(columnIndex);

        if (columnMeta && columnMeta.isSyntheticColorPreview) {
            return row[columnMeta.sourceColorColumnIndex] || "";
        }

        return row[columnIndex] || "";
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

        var name = normalizeColumnName(columnMeta.name);
        return name === "миниатюра" || name === "thumbnailpath" || name === "thumbnail" || name === "iconpath";
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

    function clampColorChannel(value) {
        var number = Number(value);

        if (isNaN(number)) {
            return 0;
        }

        if (number < 0) {
            return 0;
        }

        if (number > 255) {
            return 255;
        }

        return Math.round(number);
    }

    function tryParseRgb(rawValue) {
        var value = String(rawValue || "").trim();

        if (!value) {
            return null;
        }

        var parts = value.match(/\d+/g);

        if (!parts || parts.length < 3) {
            return null;
        }

        return {
            r: clampColorChannel(parts[0]),
            g: clampColorChannel(parts[1]),
            b: clampColorChannel(parts[2])
        };
    }

    function renderLineColorPreviewCell(cell, rawValue) {
        cell.className = "line-color-preview-td";

        var wrapper = document.createElement("div");
        wrapper.className = "line-color-cell";
        cell.appendChild(wrapper);

        var swatch = document.createElement("span");
        swatch.className = "line-color-swatch";
        wrapper.appendChild(swatch);

        var parsed = tryParseRgb(rawValue);

        if (parsed) {
            swatch.style.backgroundColor = "rgb(" + parsed.r + ", " + parsed.g + ", " + parsed.b + ")";
            swatch.title = parsed.r + ", " + parsed.g + ", " + parsed.b;
            return;
        }

        var text = document.createElement("span");
        text.className = "line-color-text";
        text.textContent = "—";
        wrapper.appendChild(text);
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
            if (checkboxes[i].checked) {
                selected.push(Number(checkboxes[i].getAttribute("data-column-index")));
            }
        }

        if (selected.length === 0 && state.displayColumns.length > 0) {
            selected.push(state.displayColumns[0].index);
        }

        state.visibleColumnIndexes = selected;
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
            filterColumnSelect.appendChild(createOption(String(visibleColumns[i].index), visibleColumns[i].label, false));
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
                    var searchCell = toLowerSafe(getRowCellValueByColumnIndex(row, visibleColumns[i].index));

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
                    var filterCell = toLowerSafe(getRowCellValueByColumnIndex(row, visibleColumns[j].index));

                    if (filterCell.indexOf(filter) >= 0) {
                        foundInFilter = true;
                        break;
                    }
                }

                if (!foundInFilter) {
                    return false;
                }
            } else {
                var targetCell = toLowerSafe(getRowCellValueByColumnIndex(row, state.filterColumnIndex));

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

        var left = getRowCellValueByColumnIndex(leftRow, index);
        var right = getRowCellValueByColumnIndex(rightRow, index);

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

    function getMinimumColumnWidth(columnMeta) {
        if (columnMeta && isThumbnailColumn(columnMeta)) {
            return 120;
        }

        if (columnMeta && columnMeta.isSyntheticColorPreview) {
            return 80;
        }

        return 70;
    }

    function startColumnResize(mouseDownEvent, columnMeta, colElement) {
        mouseDownEvent.preventDefault();
        mouseDownEvent.stopPropagation();

        if (!colElement || !columnMeta) {
            return;
        }

        var startX = mouseDownEvent.clientX;
        var currentWidth = colElement.getBoundingClientRect().width;

        if (!currentWidth || currentWidth <= 0) {
            currentWidth = state.columnWidthsByIndex[columnMeta.index] || 120;
        }

        var minWidth = getMinimumColumnWidth(columnMeta);

        function onMouseMove(moveEvent) {
            var delta = moveEvent.clientX - startX;
            var nextWidth = Math.max(minWidth, Math.round(currentWidth + delta));
            colElement.style.width = nextWidth + "px";
            state.columnWidthsByIndex[columnMeta.index] = nextWidth;
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

        var visibleColumns = getVisibleDisplayColumns();

        if (!visibleColumns.length) {
            container.innerHTML = '<div class="warning">Выберите хотя бы одну колонку.</div>';
            return;
        }

        var table = document.createElement("table");
        table.className = "data-table";

        var colgroup = document.createElement("colgroup");
        var colElements = [];

        for (var cg = 0; cg < visibleColumns.length; cg++) {
            var colMeta = visibleColumns[cg];
            var col = document.createElement("col");
            var storedWidth = state.columnWidthsByIndex[colMeta.index];

            if (storedWidth && storedWidth > 0) {
                col.style.width = storedWidth + "px";
            }

            colgroup.appendChild(col);
            colElements.push(col);
        }

        table.appendChild(colgroup);

        var thead = document.createElement("thead");
        var headRow = document.createElement("tr");

        for (var headerIndex = 0; headerIndex < visibleColumns.length; headerIndex++) {
            (function (columnPosition) {
                var column = visibleColumns[columnPosition];
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
                resizer.addEventListener("mousedown", function (event) {
                    startColumnResize(event, column, colElements[columnPosition]);
                });

                th.appendChild(resizer);

                headRow.appendChild(th);
            })(headerIndex);
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
                var cellValue = getRowCellValueByColumnIndex(sourceRow, columnMeta.index);

                if (columnMeta.isSyntheticColorPreview) {
                    renderLineColorPreviewCell(td, cellValue);
                } else if (isThumbnailColumn(columnMeta)) {
                    renderThumbnailCell(td, cellValue);
                } else {
                    td.textContent = cellValue;
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
        setText("catalogName", String(data.catalogName || "RevitLibraryBuilder"));
        setText("sourceName", String(data.sourceName || "-"));
        setText("sourceFormat", String(data.sourceFormat || "CSV"));
        setText("generatedAt", new Date(data.generatedAt).toLocaleString("ru-RU"));
        setText("totalElementsHeader", String((data.summary && data.summary.totalElements) || 0));
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
                state.sortDirection = "asc";

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
            state.sourceProfile = String(data.sourceProfile || "");
            state.rawColumns = Array.isArray(data.columns) ? data.columns.slice() : [];
            state.rows = normalizeRows(data.rows, state.rawColumns.length);
            state.displayColumns = buildDisplayColumns(state.rawColumns);
            state.visibleColumnIndexes = [];

            for (var i = 0; i < state.displayColumns.length; i++) {
                state.visibleColumnIndexes.push(state.displayColumns[i].index);
            }

            state.filteredRows = state.rows.slice();

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
