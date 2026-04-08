using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace asBIM
{
    internal class AllUsedCategoryList
    {
        public IEnumerable<BuiltInCategory> categoryList = new BuiltInCategory[]
        {
            BuiltInCategory.OST_Walls,                  // Стены
            BuiltInCategory.OST_Floors,                 // Полы
            BuiltInCategory.OST_Ceilings,               // Потолки
            BuiltInCategory.OST_Columns,                // Колонны
            BuiltInCategory.OST_StructuralColumns,      // Конструктивные колонны
            BuiltInCategory.OST_Roofs,                  // Крыши
            BuiltInCategory.OST_Doors,                  // Двери
            BuiltInCategory.OST_Windows,                // Окна
            BuiltInCategory.OST_Stairs,                 // Лестницы
            BuiltInCategory.OST_StairsRailing,          // Ограждения лестниц
            BuiltInCategory.OST_Ramps,                  // Пандусы
            BuiltInCategory.OST_Furniture,              // Мебель
            BuiltInCategory.OST_CurtainWallMullions,    // Импосты витражей
            BuiltInCategory.OST_CurtainWallPanels,      // Панели витражей
            BuiltInCategory.OST_GenericModel,           // Общие модели
            BuiltInCategory.OST_MechanicalEquipment,    // Механическое оборудование
            BuiltInCategory.OST_PipeCurves,             // Трубы
            BuiltInCategory.OST_PipeFitting,            // Фитинги труб
            BuiltInCategory.OST_DuctCurves,             // Воздуховоды
            BuiltInCategory.OST_DuctFitting,            // Фитинги воздуховодов
            BuiltInCategory.OST_PlumbingFixtures,       // Сантехнические приборы
            BuiltInCategory.OST_LightingFixtures,       // Светильники
            BuiltInCategory.OST_ElectricalEquipment,    // Электрооборудование
            BuiltInCategory.OST_ElectricalFixtures,     // Электроприборы
            BuiltInCategory.OST_CableTray,              // Кабельные лотки
            BuiltInCategory.OST_Conduit,                // Кабельные каналы
            BuiltInCategory.OST_Casework,               // Корпусная мебель
            BuiltInCategory.OST_CommunicationDevices,   // Устройства связи
            BuiltInCategory.OST_FireAlarmDevices,       // Устройства пожарной сигнализации
            BuiltInCategory.OST_DataDevices,            // Устройства передачи данных
            BuiltInCategory.OST_NurseCallDevices,       // Устройства вызова медсестры
            BuiltInCategory.OST_SecurityDevices,        // Устройства безопасности
            BuiltInCategory.OST_FurnitureSystems,       // Системы мебели
            BuiltInCategory.OST_SpecialityEquipment,    // Специальное оборудование
            BuiltInCategory.OST_LightingDevices,        // Осветительные устройства
            BuiltInCategory.OST_Parking,                // Парковочные места
            BuiltInCategory.OST_Railings,               // Ограждения
            BuiltInCategory.OST_Topography,             // Топография
            // BuiltInCategory.OST_Plants,                 // Растения
            BuiltInCategory.OST_Entourage,              // Окружение
            BuiltInCategory.OST_Mass,                   // Массы
            BuiltInCategory.OST_MassFloor,              // Полы масс
            BuiltInCategory.OST_AnalyticalNodes,        // Аналитические узлы
            // BuiltInCategory.OST_AnalyticalSurfaces,     // Аналитические поверхности
            // BuiltInCategory.OST_AnalyticalLinks,        // Аналитические связи
            BuiltInCategory.OST_Rebar,                  // Арматура
            BuiltInCategory.OST_Areas,                  // Зоны
            BuiltInCategory.OST_Rooms,                  // Помещения
            // BuiltInCategory.OST_Spaces,                 // Пространства
            //  BuiltInCategory.OST_Elevators,              // Лифты
            // BuiltInCategory.OST_Footings,               // Фундаменты
            // BuiltInCategory.OST_Framing,                // Каркасы
            // BuiltInCategory.OST_Trusses,                // Фермы
            // BuiltInCategory.OST_Wires,                  // Провода
            BuiltInCategory.OST_FabricAreas,            // Зоны арматурных сеток
            BuiltInCategory.OST_FabricReinforcement,    // Арматурные сетки
            BuiltInCategory.OST_FabricationPipework,    // Изготовленные трубы
            BuiltInCategory.OST_FabricationDuctwork,    // Изготовленные воздуховоды
            BuiltInCategory.OST_MEPSpaces,              // Пространства инженерных систем
            BuiltInCategory.OST_PointClouds,            // Облачные точки
            BuiltInCategory.OST_RoomSeparationLines,    // Линии разделения помещений
            BuiltInCategory.OST_Grids,                  // Оси
            BuiltInCategory.OST_Levels,                 // Уровни
            BuiltInCategory.OST_DesignOptions,          // Варианты проектирования
            BuiltInCategory.OST_Viewports,              // Видовые экраны
            BuiltInCategory.OST_Sheets,                 // Листы
            BuiltInCategory.OST_Views,                  // Виды
            BuiltInCategory.OST_Schedules,              // Ведомости
            BuiltInCategory.OST_Materials,              // Материалы
            BuiltInCategory.OST_Tags,                   // Маркировки
            BuiltInCategory.OST_Revisions,              // Ревизии
            // BuiltInCategory.OST_Insulation,             // Изоляция
            BuiltInCategory.OST_StructuralFoundation,   // Конструктивные фундаменты
            // BuiltInCategory.OST_BraceFrames,            // Рамы распорок
            BuiltInCategory.OST_Cameras,                // Камеры
            BuiltInCategory.OST_TextNotes,              // Текстовые аннотации
            BuiltInCategory.OST_DetailComponents,       // Деталировочные компоненты
            BuiltInCategory.OST_KeynoteTags,            // Марки ключевых заметок
            BuiltInCategory.OST_Dimensions,             // Размеры
            // BuiltInCategory.OST_Annotations,            // Аннотации
            BuiltInCategory.OST_Coordination_Model,     // Модель координации
            BuiltInCategory.OST_ColorFillLegends,       // Легенды цветового заполнения
            BuiltInCategory.OST_Parts,                  // Части
            BuiltInCategory.OST_Assemblies              // Узлы
        };
    }
}