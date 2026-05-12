using Autodesk.Revit.UI;
using SAB.RoomGeometryTools.ViewModels;
using System;
using System.Collections.Generic;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Обработчик ExternalEvent для modeless-окна Room Geometry Tools.
    /// Через него все операции, требующие API-контекст Revit, выполняются безопасно.
    /// </summary>
    public class RoomGeometryExternalEventHandler : IExternalEventHandler
    {
        private readonly object _sync = new object();
        private readonly Queue<RoomGeometryUiOperation> _pendingOperations = new Queue<RoomGeometryUiOperation>();

        private RoomGeometryToolsViewModel _viewModel;

        /// <summary>
        /// Привязка ViewModel после ее создания.
        /// </summary>
        public void AttachViewModel(RoomGeometryToolsViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        /// <summary>
        /// Очередь операции из modeless-окна.
        /// </summary>
        public void Enqueue(RoomGeometryUiOperation operation)
        {
            if (operation == RoomGeometryUiOperation.None)
            {
                return;
            }

            lock (_sync)
            {
                _pendingOperations.Enqueue(operation);
            }
        }

        /// <summary>
        /// Выполнение операций в контексте Revit.
        /// </summary>
        public void Execute(UIApplication app)
        {
            Queue<RoomGeometryUiOperation> operations = new Queue<RoomGeometryUiOperation>();

            lock (_sync)
            {
                while (_pendingOperations.Count > 0)
                {
                    operations.Enqueue(_pendingOperations.Dequeue());
                }
            }

            while (operations.Count > 0)
            {
                RoomGeometryUiOperation operation = operations.Dequeue();
                try
                {
                    _viewModel?.ExecuteOperationFromExternalEvent(operation);
                }
                catch (Exception exception)
                {
                    _viewModel?.SetStatusFromExternalEvent(
                        "Ошибка выполнения операции через ExternalEvent: " + exception.Message);
                }
            }
        }

        public string GetName()
        {
            return "SAB.RoomGeometryTools.ExternalEventHandler";
        }
    }
}

