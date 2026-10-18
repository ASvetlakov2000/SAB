# Независимая проверка footer и MSI1.1.4 —18.10.2026

Reviewer `/root/notification_settings_reviewer`, read-only; builder записал полученный вердикт, не выдавая собственную проверку за независимую.

**Bounded verdict: `ship_ui_code_payload`.** Открытых материальных findings в исправлении footer и свежих установщиках1.1.4 нет.

Проверены первичный снимок, общий opt-in компонент и настоящий ViewTemplateGraphicsWindow, новые captures, source chain и упаковка после исправления. SAB-UI-092 и регрессия090 — PASS: Padding12,10/MinHeight44/Wrap/Auto, normal/minimum/tall/long/error не перекрывают рамку и кнопки; card→status16 и соседние footer gaps12 сохранены. StatusText, исходные имена, button parameters и семь вкладок сохранены; старые общие стили не меняются глобально.

Finding SAB-UI-009 — пустой footer-detail — **resolved**. Harness использует VisualBrush фактического attached visual на локальном начале координат; повторный detail валиден, источник соответствует capture.

Независимая сверка:0 расхождений для6 свежих PNG,9source/proof файлов, первичного изображения, before/after chain, трёх MSI/Installer report/SHA256SUMS и48извлечённых payload-файлов каждого MSI. Проверены DLL, актуальный XAML редактора, стили и инструкции. Исторические proofs сохранены.

Все MSI ProductVersion1.1.4, DLL1.1.4.0, API соответствующего года. Скомпилированное окно уведомлений включено; pure Settings/Store первого запуска дают OFF без создания профиля. Preview/Host/Harness/test settings/spool в MSI отсутствуют. Фактические Upgrade rows допускают предшествующие версии и прежнюю ошибочную1.1.4, более новые версии распознаются как downgrade. PrepareOnly PASS; release notes явно отмечают отсутствие автоматического OK.

TagSuffix ограничен и сохраняет исторический v1.1.4. До публикации независимо проверены latest v1.1.3, historical tag SHA8b6979551c123892b3a790030139edb15495f966, отсутствие v1.1.4-notifications.

Builder-протокол:103PASS/6PNG,7source checks,3freshMSI, docs validator92ID. Native-прогоны и сборки reviewer не повторял; source/visual review, MSI table reads и сверка файлов выполнены read-only.

NOT RUN: установка/реальное обновление установленного продукта, live Revit, physical input/accessibility/OS DPI. Commit/push/publication ещё не выполнены на момент review и требуют отдельного подтверждения. Вердикт не подтверждает завершённую систему подавления.

Доказательства: [Report](TemplateEditorFooter-Report.json), [provenance](screenshots-template-editor-footer/provenance.json), [протокол](screenshots-template-editor-footer/verification.txt), [пакеты](NotificationSettings-Installer.json), [SourceContract](TemplateEditorFooter-SourceContract.json).
