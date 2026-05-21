(function () {
  var STORAGE_KEY = "sab.pluginDocs.mediaRoot";
  var HISTORY_KEY = "sab.pluginDocs.mediaRootHistory";
  var DEFAULT_ROOT = "Z:\\IN\\Инструкции";

  function getStoredRoot() {
    try {
      var value = localStorage.getItem(STORAGE_KEY);

      if (value && value.trim().length > 0) {
        return value.trim();
      }
    } catch (error) {
      // ignored
    }

    return DEFAULT_ROOT;
  }

  function setStoredRoot(value) {
    try {
      localStorage.setItem(STORAGE_KEY, value);
      pushRootHistory(value);
    } catch (error) {
      // ignored
    }
  }

  function getHistory() {
    try {
      var raw = localStorage.getItem(HISTORY_KEY);

      if (!raw) {
        return [DEFAULT_ROOT];
      }

      var data = JSON.parse(raw);

      if (!Array.isArray(data)) {
        return [DEFAULT_ROOT];
      }

      return data.filter(function (item) {
        return typeof item === "string" && item.trim().length > 0;
      });
    } catch (error) {
      return [DEFAULT_ROOT];
    }
  }

  function pushRootHistory(root) {
    if (!root || !root.trim()) {
      return;
    }

    var normalized = root.trim();
    var history = getHistory();
    var deduplicated = [normalized];

    for (var i = 0; i < history.length; i++) {
      var item = history[i];

      if (!item) {
        continue;
      }

      if (item.toLowerCase() === normalized.toLowerCase()) {
        continue;
      }

      deduplicated.push(item);

      if (deduplicated.length >= 8) {
        break;
      }
    }

    try {
      localStorage.setItem(HISTORY_KEY, JSON.stringify(deduplicated));
    } catch (error) {
      // ignored
    }
  }

  function normalizePath(pathValue) {
    if (!pathValue) {
      return "";
    }

    var value = String(pathValue).trim();

    if (value.length === 0) {
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
      var drivePath = value.replace(/\\/g, "/");
      return "file:///" + encodeURI(drivePath);
    }

    if (/^\\\\/.test(value)) {
      var uncPath = value.replace(/^\\\\/, "").replace(/\\/g, "/");
      return "file://" + encodeURI(uncPath);
    }

    return "file:///" + encodeURI(value.replace(/\\/g, "/"));
  }

  function validatePath(pathValue) {
    var value = normalizePath(pathValue);

    if (!value) {
      return { isValid: false, message: "Путь не задан." };
    }

    if (/^file:\/\//i.test(value) || /^[a-zA-Z]:[\\/]/.test(value) || /^\\\\/.test(value)) {
      return { isValid: true, message: "Путь принят. Если файлы не открываются, укажите другой источник." };
    }

    return { isValid: false, message: "Некорректный формат пути. Используйте путь вида Z:\\IN\\Инструкции или file:///..." };
  }

  function buildSourcePanel() {
    var hero = document.querySelector(".hero");

    if (!hero || !hero.parentNode) {
      return;
    }

    var panel = document.createElement("section");
    panel.className = "section media-source-panel";

    panel.innerHTML =
      '<h3 class="section-title">Источник фото и видео инструкций</h3>' +
      '<div class="media-source-grid">' +
      '  <div class="media-source-main">' +
      '    <label class="media-source-label" for="mediaSourcePathInput">Путь к папке источников</label>' +
      '    <input id="mediaSourcePathInput" class="media-source-input" type="text" list="mediaSourcePathHistory" />' +
      '    <datalist id="mediaSourcePathHistory"></datalist>' +
      '    <div class="media-source-actions">' +
      '      <button id="mediaSourceApplyButton" type="button" class="media-source-button">Применить путь</button>' +
      '      <button id="mediaSourceResetButton" type="button" class="media-source-button secondary">Сбросить на Z:\\IN\\Инструкции</button>' +
      '      <a id="mediaSourceOpenFolderLink" class="media-source-link" target="_blank" rel="noopener">Открыть папку источника</a>' +
      '    </div>' +
      '    <div id="mediaSourceStatus" class="media-source-status"></div>' +
      '  </div>' +
      '</div>';

    hero.parentNode.insertBefore(panel, hero.nextSibling);

    var input = panel.querySelector("#mediaSourcePathInput");
    var applyButton = panel.querySelector("#mediaSourceApplyButton");
    var resetButton = panel.querySelector("#mediaSourceResetButton");
    var status = panel.querySelector("#mediaSourceStatus");
    var openLink = panel.querySelector("#mediaSourceOpenFolderLink");
    var historyList = panel.querySelector("#mediaSourcePathHistory");

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

    function applyPath(pathValue, saveToStorage) {
      var normalized = normalizePath(pathValue);
      var validation = validatePath(normalized);

      if (!normalized) {
        status.textContent = "Укажите путь к источникам медиа.";
        status.classList.add("error");
        return;
      }

      if (saveToStorage) {
        setStoredRoot(normalized);
      }

      if (input) {
        input.value = normalized;
      }

      openLink.setAttribute("href", toFileUri(normalized));
      openLink.setAttribute("title", normalized);

      if (!validation.isValid) {
        status.textContent = validation.message;
        status.classList.add("error");
      } else {
        status.textContent = "Текущий источник: " + normalized + "\n" + validation.message;
        status.classList.remove("error");
      }

      renderHistory();
      decorateMediaPlaceholders(normalized);
    }

    applyButton.addEventListener("click", function () {
      applyPath(input.value, true);
    });

    resetButton.addEventListener("click", function () {
      applyPath(DEFAULT_ROOT, true);
    });

    input.addEventListener("keydown", function (event) {
      if (event.key === "Enter") {
        applyPath(input.value, true);
      }
    });

    renderHistory();
    applyPath(getStoredRoot(), false);
  }

  function decorateMediaPlaceholders(rootPath) {
    var placeholders = document.querySelectorAll(".placeholder-media");

    if (!placeholders || placeholders.length === 0) {
      return;
    }

    var rootUri = toFileUri(rootPath);

    for (var i = 0; i < placeholders.length; i++) {
      var placeholder = placeholders[i];

      if (!placeholder) {
        continue;
      }

      var existingHint = placeholder.querySelector(".media-source-hint");

      if (existingHint) {
        existingHint.remove();
      }

      var hint = document.createElement("div");
      hint.className = "media-source-hint";

      var text = document.createElement("span");
      text.textContent = "Источник медиа: " + rootPath;
      hint.appendChild(text);

      var link = document.createElement("a");
      link.href = rootUri;
      link.target = "_blank";
      link.rel = "noopener";
      link.className = "media-source-inline-link";
      link.textContent = "Открыть папку";
      hint.appendChild(link);

      var fileRelativePath = placeholder.getAttribute("data-media-file");

      if (fileRelativePath && fileRelativePath.trim().length > 0) {
        var separator = rootUri.endsWith("/") ? "" : "/";
        var fileUrl = rootUri + separator + encodeURI(fileRelativePath.replace(/\\/g, "/"));

        var fileLink = document.createElement("a");
        fileLink.href = fileUrl;
        fileLink.target = "_blank";
        fileLink.rel = "noopener";
        fileLink.className = "media-source-inline-link";
        fileLink.textContent = "Открыть файл";
        hint.appendChild(fileLink);
      }

      placeholder.appendChild(hint);
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    buildSourcePanel();
  });
})();
