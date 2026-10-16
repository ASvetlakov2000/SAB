using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;
using SAB.InteriorElevations.Views;

namespace SAB.InteriorElevations.Services.Elevations
{
    public enum DirectionPreviewOperation { Flip, Create, Cancel }

    public sealed class ElevationDirectionPreviewController
    {
        private static ElevationDirectionPreviewController _active;
        private readonly ElevationDirectionPreviewService _service = new ElevationDirectionPreviewService();
        private readonly UIApplication _application;
        private readonly Action<UIApplication> _create;
        private readonly Nice3point.Revit.Toolkit.External.ExternalEvent<DirectionPreviewOperation> _event;
        private ElevationDirectionPreviewWindow _window;
        private bool _finished;
        private bool _closeRequested;
        private string _closingDocumentId;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        private ElevationDirectionPreviewController(UIApplication application, Action<UIApplication> create)
        {
            _application = application; _create = create;
            _event = new Nice3point.Revit.Toolkit.External.ExternalEvent<DirectionPreviewOperation>(Execute);
        }

        public static bool ActivateExisting()
        {
            if (_active == null || _active._finished) return false;
            _active._window.Show(); _active._window.Activate(); return true;
        }

        public static void Start(UIApplication application, ViewPlan plan, IList<ElevationLineData> lines, Action<UIApplication> create)
        {
            if (_active != null && !_active._finished) throw new InvalidOperationException("Предпросмотр уже открыт.");
            var controller = new ElevationDirectionPreviewController(application, create);
            try
            {
                controller._service.Create(plan.Document, plan, lines);
                int virtualCount = lines.Count(line => line.UsesVirtualClosure);
                int unclosedCount = lines.Count(line => line.IsOpenContour && !line.UsesVirtualClosure);
                controller._window = new ElevationDirectionPreviewWindow("Временных марок: " + lines.Count +
                    (virtualCount > 0 ? ". Марок по виртуальному замыканию: " + virtualCount : string.Empty) +
                    (unclosedCount > 0 ? ". Без определённой внутренней стороны: " + unclosedCount : string.Empty) +
                    (virtualCount == 0 && unclosedCount == 0 ? ". Замкнутые контуры: исходная сторона внутрь." : "."));
                if (application.MainWindowHandle != IntPtr.Zero) new WindowInteropHelper(controller._window).Owner = application.MainWindowHandle;
                controller._window.FlipRequested += () => controller.Request(DirectionPreviewOperation.Flip);
                controller._window.CreateRequested += () => controller.Request(DirectionPreviewOperation.Create);
                controller._window.CancelRequested += () => controller.Request(DirectionPreviewOperation.Cancel);
                application.Application.DocumentClosing += controller.DocumentClosing;
                application.Application.DocumentClosed += controller.DocumentClosed;
                application.Application.DocumentSaving += controller.DocumentSaving;
                application.Application.DocumentSavingAs += controller.DocumentSavingAs;
                application.Application.DocumentSynchronizingWithCentral += controller.DocumentSynchronizing;
                _active = controller; controller._window.Show();
            }
            catch
            {
                controller._service.DeleteMarkers(); controller.Finish(); throw;
            }
        }

        private void Request(DirectionPreviewOperation operation)
        {
            if (_finished || _window.IsBusy) return;
            _window.SetStatus(operation == DirectionPreviewOperation.Cancel ? "Удаление временных марок…" : "Ожидание Revit…", true);
            try
            {
                var request = _event.Raise(operation);
                if (request != ExternalEventRequest.Accepted && request != ExternalEventRequest.Pending)
                    _window.SetStatus("Revit сейчас не может выполнить действие. Завершите текущую команду и повторите.");
            }
            catch (Exception exception) { _window.SetStatus(exception.Message); }
        }

        private void Execute(UIApplication application, DirectionPreviewOperation operation)
        {
            if (_finished) return;
            try
            {
                if (operation == DirectionPreviewOperation.Cancel)
                {
                    _service.DeleteMarkers(); Finish();
                    if (_closeRequested) global::Helpers.Notifications.ToastNotifications.ToastNotifier.ShowInfo("SAB Развёртки", "Временные марки удалены. Повторите закрытие документа.");
                    return;
                }
                var uiDocument = application.ActiveUIDocument;
                if (uiDocument == null || !_service.IsSourceDocument(uiDocument.Document))
                    throw new InvalidOperationException("Вернитесь в исходный документ предпросмотра.");
                if (uiDocument.ActiveView == null || !RevitElementIdUtils.AreEqual(uiDocument.ActiveView.Id, _service.Plan.Id))
                    throw new InvalidOperationException("Вернитесь на исходный план предпросмотра.");
                if (operation == DirectionPreviewOperation.Flip)
                {
                    ICollection<ElementId> ids = uiDocument.Selection.GetElementIds()
                        .Where(id => _service.Owns(uiDocument.Document.GetElement(id))).ToList();
                    _window.Hide();
                    try
                    {
                        if (ids.Count == 0)
                        {
                            if (application.MainWindowHandle != IntPtr.Zero) SetForegroundWindow(application.MainWindowHandle);
                            ids = uiDocument.Selection.PickObjects(ObjectType.Element,
                                new PreviewSelectionFilter(_service), "Выберите марки для смены стороны. Завершите выбор кнопкой Готово.")
                                .Select(reference => reference.ElementId).ToList();
                        }
                        _service.Flip(ids);
                    }
                    finally { if (!_finished) _window.Show(); }
                    _window.SetStatus("Сторона изменена. Марок: " + ids.Count + ".");
                    return;
                }
                _service.Validate(true);
                _service.DeleteMarkers();
                Finish();
                _create(application);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                if (!_finished) _window.SetStatus("Выбор марок отменён.");
            }
            catch (Exception exception)
            {
                if (!_finished) _window.SetStatus(exception.Message);
                else global::Helpers.Notifications.ToastNotifications.ToastNotifier.ShowError("SAB Развёртки", exception.Message);
            }
        }

        private void DocumentClosing(object sender, DocumentClosingEventArgs args)
        {
            if (_finished || !_service.IsSourceDocument(args.Document)) return;
            // DocumentClosing is read-only. Never start a transaction here.
            _closingDocumentId = args.DocumentId.ToString();
            if (args.Cancellable)
            {
                args.Cancel(); _closeRequested = true;
                _window.SetStatus("Удаление временных марок перед закрытием…", true);
                try
                {
                    var request = _event.Raise(DirectionPreviewOperation.Cancel);
                    if (request != ExternalEventRequest.Accepted && request != ExternalEventRequest.Pending)
                        _window.SetStatus("Не удалось запустить очистку. Закройте предпросмотр кнопкой «Отмена» и повторите закрытие документа.");
                }
                catch (Exception exception) { _window.SetStatus(exception.Message); }
            }
            else _window.Hide(); // Save callbacks remove annotations before any shutdown save.
        }

        private void DocumentClosed(object sender, DocumentClosedEventArgs args)
        {
            if (_finished || _closingDocumentId != args.DocumentId.ToString()) return;
            if (!_service.Document.IsValidObject) Finish();
            else if (!_closeRequested) { _closingDocumentId = null; _window.Show(); }
        }

        private void DocumentSaving(object sender, DocumentSavingEventArgs args)
        {
            if (_finished || !_service.IsSourceDocument(args.Document)) return;
            try { CancelBeforeSave(); }
            catch (Exception exception)
            {
                if (args.Cancellable) args.Cancel();
                _window.SetStatus("Не удалось очистить предпросмотр перед сохранением: " + exception.Message);
            }
        }

        private void DocumentSavingAs(object sender, DocumentSavingAsEventArgs args)
        {
            if (_finished || !_service.IsSourceDocument(args.Document)) return;
            try { CancelBeforeSave(); }
            catch (Exception exception)
            {
                if (args.Cancellable) args.Cancel();
                _window.SetStatus("Не удалось очистить предпросмотр перед сохранением: " + exception.Message);
            }
        }

        private void CancelBeforeSave()
        {
            // Saving/SavingAs allow DB modification. Temporary marks must not be written to the RVT.
            _service.DeleteMarkers(); Finish();
            global::Helpers.Notifications.ToastNotifications.ToastNotifier.ShowInfo("SAB Развёртки", "Предпросмотр отменён перед сохранением документа.");
        }

        private void DocumentSynchronizing(object sender, DocumentSynchronizingWithCentralEventArgs args)
        {
            if (_finished || !_service.IsSourceDocument(args.Document)) return;
            try { CancelBeforeSave(); }
            catch (Exception exception)
            {
                if (args.Cancellable) args.Cancel();
                _window.SetStatus("Не удалось очистить предпросмотр перед синхронизацией: " + exception.Message);
            }
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            _application.Application.DocumentClosing -= DocumentClosing;
            _application.Application.DocumentClosed -= DocumentClosed;
            _application.Application.DocumentSaving -= DocumentSaving;
            _application.Application.DocumentSavingAs -= DocumentSavingAs;
            _application.Application.DocumentSynchronizingWithCentral -= DocumentSynchronizing;
            if (_window != null) _window.CloseAfterCleanup();
            if (_active == this) _active = null;
            var disposable = (object)_event as IDisposable;
            if (disposable != null) disposable.Dispose();
        }

        private sealed class PreviewSelectionFilter : ISelectionFilter
        {
            private readonly ElevationDirectionPreviewService _service;
            public PreviewSelectionFilter(ElevationDirectionPreviewService service) { _service = service; }
            public bool AllowElement(Element element) { return _service.Owns(element); }
            public bool AllowReference(Reference reference, XYZ position) { return false; }
        }
    }
}
