# Привязка кончика превью к линии —16.10.2026,1.1.4

Прямой запрос пользователя: уменьшенное семейство, origin на кончике стрелки; стрелка направлена вниз, геометрия находится выше origin. User image: `C:/Users/svetl/AppData/Local/Temp/codex-clipboard-cd771123-b3f9-4ef0-8e4f-fc3a60c9e613.png`. Новый совместимый RFA получен из `C:/Users/svetl/Downloads/SAB_Развертки_Направление.rfa`; BasicFileInfo.Format=2022, SHA256=25F8A49176F7111DEF0748E3AE00630560A75C11076903F9DE730343042C316D. Одинаковые байты в ресурсах2022 и2023-2024. Ранее присланный Desktop RFA2023 не попал в итоговую поставку: заменён более поздним пользовательским2022файлом.

`ElevationDirectionPreviewService.Create` вставляет annotation instance в `Line.MidPoint`. `AlignMarker` вращает его вокруг собственного LocationPoint, направляя исходную стрелку−BasisY к−InsideNormal. При Flip annotation origin не двигается: кончик остаётся на исходной линии; тело обозначения переходит на другую сторону вращением.

Настоящая точка фасада хранится отдельно в `Line.MarkerPoint`. Как и раньше, Flip обновляет её по `MidPoint + InsideNormal * MarkerDistance`; создание elevation использует эту точку и настройку отступа. Validate сравнивает annotation location сMidPoint, не сMarkerPoint. Ошибка поворота/commit откатывает и Revit transaction, и in-memory normal/real MarkerPoint. Все guards/lifecycle/ownership/номера сохранены.

Существующие семейства не перезагружаются автоматически. Поэтому в модели со старым семейством пользователь вручную загружает новое с заменой версии. Читаемость задаётся native Keep Readable каждой из двух label и параметрами семейства. Код не пытается вращать labels отдельно, не обещает постоянно горизонтальный текст и не проверяет содержимое RFA без Revit. Origin/ориентация приняты по прямому сообщению пользователя и изображению, а не доказаны offline parsing.

Проверка:66checks actual linked production geometry/services/controller/crop with Revit API doubles. Новые случаи: preview origin независим от реального отступа; наклонные линии и повторные flips держат origin; Flip не вызываетMoveElement; конечный elevation получает реальный offset после previewFlip. Existing failure case теперь injects Rotate failure; commit rollback проверяет origin/angle и draft. MSI22/23 freshly extracted DLL/XAML/инструкции/Toolkit/новыйRFA сравниваются сcurrentbuild. Version1.1.4.

WPF/XAML/footer не менялись. Новый WPF capture не запускался; [41assertions/6PNG1.1.3](screenshots-preview-runtime/verification.txt) — исторический отдельный scope. Live Revit placement, label readability/printing и физический ввод остаются NOT RUN. Новый backend/resource evidence не закрывает их автоматически.

[Provenance](ElevationPreviewTip-Provenance.json) · [Report](ElevationPreviewTip-Report.json) · [Installer](ElevationPreviewTip-Installer.json). Criteria079 уточнён прямым запросом, новые общие визуальные tokens не вводились.
