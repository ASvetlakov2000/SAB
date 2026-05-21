(function () {
  "use strict";

  var STORAGE_KEY_ROOT = "sab.pluginDocs.mediaRoot";
  var STORAGE_KEY_HISTORY = "sab.pluginDocs.mediaRootHistory";
  var STORAGE_KEY_PLACEHOLDERS = "sab.pluginDocs.mediaPlaceholders:";
  var DEFAULT_ROOT = "Z:\\IN\\Инструкции";

  var COMMAND_LINKS = [
    { title: "Инструкции", href: "index-light.html" },
    { title: "Экспорт системных", href: "system-families-light.html" },
    { title: "Экспорт загружаемых", href: "loadable-families-light.html" },
    { title: "Экспорт линий", href: "dimension-lines-light.html#export-lines" },
    { title: "Экспорт штриховок", href: "dimension-lines-light.html#export-fills" },
    { title: "Экспорт PNG линий", href: "dimension-lines-light.html#png-lines" },
    { title: "Экспорт PNG штриховок", href: "dimension-lines-light.html#png-fills" },
    { title: "Экспорт имен типов", href: "revit-model-names-light.html" },
    { title: "Экспорт имен MTL", href: "materials-light.html" },
    { title: "Экспорт PNG семейств", href: "loadable-families-light.html#png-family" },
    { title: "Экспорт PNG пирогов", href: "system-families-light.html#png-pirogi" },
    { title: "Переименовать типы", href: "revit-model-names-light.html#import-types" },
    { title: "Переименовать материалы", href: "materials-light.html#import-materials" },
    { title: "Загрузить PNG пироги", href: "system-families-light.html#load-pirogi" },
    { title: "Размещение по точке", href: "dimension-lines-light.html#place-point" },
    { title: "Размещение по границе", href: "dimension-lines-light.html#place-boundary" },
    { title: "Размещение по линии", href: "dimension-lines-light.html#place-line" },
    { title: "Расставить комп. легенды", href: "dimension-lines-light.html#place-legend-components" },
    { title: "Размещение линий", href: "dimension-lines-light.html#place-lines" },
    { title: "Размещение штриховок", href: "dimension-lines-light.html#place-fills" },
    { title: "Удаление элементов", href: "dimension-lines-light.html#delete-elements" },
    { title: "HTML просмотр", href: "html-viewer-light.html" },
    { title: "Стандарты Наименования", href: "naming-standards-light.html" },
    { title: "Создать развертки по линии", href: "text-notes-light.html#elev-create" },
    { title: "Разворот развертки 180", href: "text-notes-light.html#elev-flip" },
    { title: "Перенос видов на след. лист", href: "text-notes-light.html#elev-move-sheet" },
    { title: "Выровнять марки углов", href: "text-notes-light.html#elev-align-corners" },
    { title: "Проверка геометрии помещений", href: "room-geometry-light.html" }
  ];

  function getStoredRoot() {
    try {
      var value = localStorage.getItem(STORAGE_KEY_ROOT);

      if (value && value.trim().length > 0) {
        return value.trim();
      }
    } catch (error) {
      // ignore storage errors
    }

    return DEFAULT_ROOT;
  }

  function setStoredRoot(value) {
    try {
      localStorage.setItem(STORAGE_KEY_ROOT, value);
      pushRootHistory(value);
    } catch (error) {
      // ignore storage errors
    }
  }

  function getHistory() {
    try {
      var raw = localStorage.getItem(STORAGE_KEY_HISTORY);

      if (!raw) {
        return [DEFAULT_ROOT];
      }

      var parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) {
        return [DEFAULT_ROOT];
      }

      return parsed.filter(function (item) {
        return typeof item === "string" && item.trim().length > 0;
      });
    } catch (error) {
      return [DEFAULT_ROOT];
    }
  }

  function pushRootHistory(rootPath) {
    if (!rootPath || !rootPath.trim()) {
      return;
    }

    var normalized = rootPath.trim();
    var history = getHistory();
    var merged = [normalized];

    for (var index = 0; index < history.length; index++) {
      var current = history[index];

      if (!current) {
        continue;
      }

      if (current.toLowerCase() === normalized.toLowerCase()) {
        continue;
      }

      merged.push(current);

      if (merged.length >= 12) {
        break;
      }
    }

    try {
      localStorage.setItem(STORAGE_KEY_HISTORY, JSON.stringify(merged));
    } catch (error) {
      // ignore storage errors
    }
  }

  function normalizePath(pathValue) {
    if (!pathValue) {
      return "";
    }

    var value = String(pathValue).trim();
    if (!value) {
      return "";
    }

    value = value.replace(/^"+|"+$/g, "");

    if (/^file:\/\//i.test(value)) {
      return value;
    }

    if (/^[a-zA-Z]:[\\/]/.test(value)) {
      return value.replace(/\//g, "\\");
    }

    if (/^\\\\/.test(value)) {
      return value;
    }

    return value;
  }

  function toFileUri(pathValue) {
    var value = normalizePath(pathValue);

    if (!value) {
      return "";
    }

    if (/^file:\/\//i.test(value)) {
      return value;
    }

    if (/^[a-zA-Z]:[\\/]/.test(value)) {
      return "file:///" + encodeURI(value.replace(/\\/g, "/"));
    }

    if (/^\\\\/.test(value)) {
      return "file://" + encodeURI(value.replace(/^\\\\/, "").replace(/\\/g, "/"));
    }

    return "file:///" + encodeURI(value.replace(/\\/g, "/"));
  }

  function toMsExplorerUri(pathValue) {
    var fileUri = toFileUri(pathValue);

    if (!fileUri) {
      return "";
    }

    return "ms-explorer:" + fileUri;
  }

  function isRelativePath(value) {
    return !!value && !/^file:\/\//i.test(value) && !/^[a-zA-Z]:[\\/]/.test(value) && !/^\\\\/.test(value);
  }

  function combineRootAndRelative(rootPath, relativePath) {
    var base = normalizePath(rootPath);
    var rel = normalizePath(relativePath);

    if (!rel) {
      return "";
    }

    if (!isRelativePath(rel)) {
      return rel;
    }

    if (!base) {
      return rel;
    }

    var separator = /[\\/]$/.test(base) ? "" : "\\";
    return base + separator + rel.replace(/[\\/]+/g, "\\");
  }

  function validateRootPath(pathValue) {
    var value = normalizePath(pathValue);

    if (!value) {
      return { ok: false, text: "Путь не задан." };
    }

    if (/^file:\/\//i.test(value) || /^[a-zA-Z]:[\\/]/.test(value) || /^\\\\/.test(value)) {
      return { ok: true, text: "Путь принят. Для фото/видео используйте кнопки открытия через ОС." };
    }

    return { ok: false, text: "Некорректный формат пути. Используйте путь вида Z:\\IN\\Инструкции." };
  }

  function getCurrentPageStorageKey() {
    return STORAGE_KEY_PLACEHOLDERS + window.location.pathname.toLowerCase();
  }

  function loadPlaceholderState() {
    try {
      var raw = localStorage.getItem(getCurrentPageStorageKey());
      if (!raw) {
        return {};
      }

      var parsed = JSON.parse(raw);
      if (!parsed || typeof parsed !== "object") {
        return {};
      }

      return parsed;
    } catch (error) {
      return {};
    }
  }

  function savePlaceholderState(state) {
    try {
      localStorage.setItem(getCurrentPageStorageKey(), JSON.stringify(state || {}));
    } catch (error) {
      // ignore storage errors
    }
  }

  function openByOperatingSystem(pathValue, statusElement) {
    var normalized = normalizePath(pathValue);
    if (!normalized) {
      if (statusElement) {
        statusElement.textContent = "Путь к файлу не задан.";
      }
      return;
    }

    var msExplorerUri = toMsExplorerUri(normalized);
    var fileUri = toFileUri(normalized);

    try {
      if (typeof navigator !== "undefined" && typeof navigator.msLaunchUri === "function") {
        navigator.msLaunchUri(msExplorerUri, function () {}, function () {
          var fallbackLink = document.createElement("a");
          fallbackLink.href = fileUri;
          fallbackLink.style.display = "none";
          document.body.appendChild(fallbackLink);
          fallbackLink.click();
          fallbackLink.remove();
        });
      } else {
        var nativeLink = document.createElement("a");
        nativeLink.href = msExplorerUri;
        nativeLink.style.display = "none";
        document.body.appendChild(nativeLink);
        nativeLink.click();
        nativeLink.remove();
      }

      if (statusElement) {
        statusElement.textContent = "Запрошено открытие через ОС: " + normalized;
      }
    } catch (error) {
      var safeFallback = document.createElement("a");
      safeFallback.href = fileUri;
      safeFallback.style.display = "none";
      document.body.appendChild(safeFallback);
      safeFallback.click();
      safeFallback.remove();
      if (statusElement) {
        statusElement.textContent = "Открытие через ms-explorer недоступно. Применен fallback file://";
      }
    }
  }

  function buildSidebar() {
    var body = document.body;
    if (!body) {
      return;
    }

    body.classList.add("with-fixed-sidebar");

    var sidebar = document.createElement("aside");
    sidebar.className = "fixed-sidebar";

    var linksHtml = "";
    for (var index = 0; index < COMMAND_LINKS.length; index++) {
      linksHtml += '<a class="sidebar-tab" href="' + COMMAND_LINKS[index].href + '" data-href="' + COMMAND_LINKS[index].href + '">' + COMMAND_LINKS[index].title + "</a>";
    }

    sidebar.innerHTML =
      '<div class="sidebar-section">' +
      '  <h3 class="sidebar-title">Настройки путей</h3>' +
      '  <label class="media-source-label" for="mediaSourcePathInput">Папка источников фото/видео</label>' +
      '  <input id="mediaSourcePathInput" class="media-source-input" type="text" list="mediaSourcePathHistory" />' +
      '  <datalist id="mediaSourcePathHistory"></datalist>' +
      '  <div class="media-source-actions">' +
      '    <button id="mediaSourceApplyButton" type="button" class="media-source-button">Применить путь</button>' +
      '    <button id="mediaSourceResetButton" type="button" class="media-source-button secondary">Сбросить</button>' +
      '    <button id="mediaSourceOpenExplorerButton" type="button" class="media-source-button secondary">Открыть папку</button>' +
      "  </div>" +
      '  <div id="mediaSourceStatus" class="media-source-status"></div>' +
      "</div>" +
      '<div class="sidebar-section sidebar-section-tabs">' +
      '  <h3 class="sidebar-title">Команды SAB</h3>' +
      '  <div class="sidebar-tabs">' + linksHtml + "</div>" +
      "</div>";

    body.appendChild(sidebar);

    var input = sidebar.querySelector("#mediaSourcePathInput");
    var applyButton = sidebar.querySelector("#mediaSourceApplyButton");
    var resetButton = sidebar.querySelector("#mediaSourceResetButton");
    var openExplorerButton = sidebar.querySelector("#mediaSourceOpenExplorerButton");
    var status = sidebar.querySelector("#mediaSourceStatus");
    var historyList = sidebar.querySelector("#mediaSourcePathHistory");

    function renderHistory() {
      if (!historyList) {
        return;
      }

      historyList.innerHTML = "";

      var history = getHistory();
      for (var i = 0; i < history.length; i++) {
        var option = document.createElement("option");
        option.value = history[i];
        historyList.appendChild(option);
      }
    }

    function applyRootPath(pathValue, save) {
      var normalized = normalizePath(pathValue);
      var validation = validateRootPath(normalized);

      if (!normalized) {
        status.textContent = "Укажите путь к источникам.";
        status.classList.add("error");
        return;
      }

      if (save) {
        setStoredRoot(normalized);
      }

      input.value = normalized;

      if (!validation.ok) {
        status.textContent = validation.text;
        status.classList.add("error");
      } else {
        status.textContent = "Текущий путь: " + normalized + "\n" + validation.text;
        status.classList.remove("error");
      }

      renderHistory();
      decorateMediaBlocks(normalized, status);
    }

    applyButton.addEventListener("click", function () {
      applyRootPath(input.value, true);
    });

    resetButton.addEventListener("click", function () {
      applyRootPath(DEFAULT_ROOT, true);
    });

    openExplorerButton.addEventListener("click", function () {
      openByOperatingSystem(input.value, status);
    });

    input.addEventListener("keydown", function (event) {
      if (event.key === "Enter") {
        applyRootPath(input.value, true);
      }
    });

    renderHistory();
    applyRootPath(getStoredRoot(), false);

    var tabs = sidebar.querySelectorAll(".sidebar-tab");
    var current = (window.location.pathname || "").toLowerCase();
    for (var t = 0; t < tabs.length; t++) {
      var href = tabs[t].getAttribute("data-href") || "";
      var baseHref = href.split("#")[0].toLowerCase();
      if (baseHref && current.indexOf(baseHref) >= 0) {
        tabs[t].classList.add("active");
      }
    }
  }

  function decorateMediaBlocks(rootPath, statusNode) {
    var placeholders = document.querySelectorAll(".placeholder-media");
    var storedData = loadPlaceholderState();

    for (var index = 0; index < placeholders.length; index++) {
      var placeholder = placeholders[index];
      var key = "ph_" + index;
      var itemState = storedData[key] || {};

      var existingPanel = placeholder.querySelector(".media-action-panel");
      if (existingPanel) {
        existingPanel.remove();
      }

      var panel = document.createElement("div");
      panel.className = "media-action-panel";

      var title = document.createElement("div");
      title.className = "media-action-title";
      title.textContent = "Файлы блока";
      panel.appendChild(title);

      var photoInput = document.createElement("input");
      photoInput.type = "text";
      photoInput.className = "media-source-input";
      photoInput.placeholder = "Относительный путь к фото (например Photos\\step1.png)";
      photoInput.value = itemState.photo || "";
      panel.appendChild(photoInput);

      var videoInput = document.createElement("input");
      videoInput.type = "text";
      videoInput.className = "media-source-input";
      videoInput.placeholder = "Относительный путь к видео (например Video\\step1.mp4)";
      videoInput.value = itemState.video || "";
      panel.appendChild(videoInput);

      var buttonsRow = document.createElement("div");
      buttonsRow.className = "media-source-actions";

      var saveButton = document.createElement("button");
      saveButton.type = "button";
      saveButton.className = "media-source-button secondary";
      saveButton.textContent = "Сохранить пути";
      buttonsRow.appendChild(saveButton);

      var openPhotoButton = document.createElement("button");
      openPhotoButton.type = "button";
      openPhotoButton.className = "media-source-button";
      openPhotoButton.textContent = "Открыть фото";
      buttonsRow.appendChild(openPhotoButton);

      var openVideoButton = document.createElement("button");
      openVideoButton.type = "button";
      openVideoButton.className = "media-source-button";
      openVideoButton.textContent = "Открыть видео";
      buttonsRow.appendChild(openVideoButton);

      panel.appendChild(buttonsRow);

      var localStatus = document.createElement("div");
      localStatus.className = "media-source-status";
      localStatus.textContent = "Источник: " + rootPath;
      panel.appendChild(localStatus);

      saveButton.addEventListener("click", function (placeholderKey, pInput, vInput, localStatusNode) {
        return function () {
          var next = loadPlaceholderState();
          next[placeholderKey] = {
            photo: normalizePath(pInput.value),
            video: normalizePath(vInput.value)
          };
          savePlaceholderState(next);
          localStatusNode.textContent = "Пути сохранены для текущего блока.";
        };
      }(key, photoInput, videoInput, localStatus));

      openPhotoButton.addEventListener("click", function (pInput, localStatusNode) {
        return function () {
          var fullPath = combineRootAndRelative(rootPath, pInput.value);
          openByOperatingSystem(fullPath, localStatusNode || statusNode);
        };
      }(photoInput, localStatus));

      openVideoButton.addEventListener("click", function (vInput, localStatusNode) {
        return function () {
          var fullPath = combineRootAndRelative(rootPath, vInput.value);
          openByOperatingSystem(fullPath, localStatusNode || statusNode);
        };
      }(videoInput, localStatus));

      placeholder.appendChild(panel);
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    buildSidebar();
  });
})();
