using System;
using System.Collections.Generic;
using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.Services.Data
{
    /// <summary>
    /// Фабрика выбора провайдера по выбранному типу источника.
    /// </summary>
    public class DataProviderFactory
    {
        private readonly List<IDataProvider> _providers;

        public DataProviderFactory(List<IDataProvider> providers)
        {
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        }

        public IDataProvider Create(DataSourceType sourceType)
        {
            for (int i = 0; i < _providers.Count; i++)
            {
                IDataProvider provider = _providers[i];

                if (provider != null && provider.CanHandle(sourceType))
                {
                    return provider;
                }
            }

            throw new InvalidOperationException("Провайдер для выбранного источника не найден.");
        }
    }
}
