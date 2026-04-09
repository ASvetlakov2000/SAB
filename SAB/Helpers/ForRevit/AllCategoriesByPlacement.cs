using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SAB
{
    internal static class AllCategoriesByPlacement
    {
        // Словарь категорий по типу размещения
        public static readonly Dictionary<string, BuiltInCategory[]> CategoriesByPlacement = new Dictionary<string, BuiltInCategory[]>
        {
            // Элементы по линии/трассе с высотой
            { "LineBased", new BuiltInCategory[]
                {
                    BuiltInCategory.OST_Walls,               // Стены
                    BuiltInCategory.OST_Columns,             // Колонны
                    BuiltInCategory.OST_StructuralColumns,   // Конструктивные колонны
                    // BuiltInCategory.OST_Framing,             // Каркасы
                    // BuiltInCategory.OST_Trusses,             // Фермы
                    // BuiltInCategory.OST_BraceFrames,         // Рамы распорок
                    BuiltInCategory.OST_Railings,            // Ограждения
                    BuiltInCategory.OST_StairsRailing,       // Ограждения лестниц
                    BuiltInCategory.OST_Ramps,               // Пандусы
                    BuiltInCategory.OST_PipeCurves,          // Трубы
                    BuiltInCategory.OST_DuctCurves,          // Воздуховоды
                    // BuiltInCategory.OST_Wires,               // Провода
                    BuiltInCategory.OST_CableTray,           // Кабельные лотки
                    BuiltInCategory.OST_Conduit              // Кабельные каналы
                }
            },

            // Элементы по контуру/области
            { "ContourBased", new BuiltInCategory[]
                {
                    BuiltInCategory.OST_Floors,              // Полы
                    BuiltInCategory.OST_Ceilings,            // Потолки
                    BuiltInCategory.OST_Roofs,               // Крыши
                    BuiltInCategory.OST_FabricAreas,         // Зоны арматурных сеток
                    BuiltInCategory.OST_MassFloor,           // Полы масс
                    BuiltInCategory.OST_Areas,               // Зоны
                    BuiltInCategory.OST_RoomSeparationLines  // Линии разделения помещений
                }
            },

            // Элементы, которые требуют хоста
            { "HostBased", new BuiltInCategory[]
                {
                    BuiltInCategory.OST_Doors,               // Двери (только на стене)
                    BuiltInCategory.OST_Windows              // Окна (только на стене)
                }
            },

            // Элементы по точке/местоположению
            { "PointBased", new BuiltInCategory[]
                {
                    BuiltInCategory.OST_Furniture,             // Мебель
                    BuiltInCategory.OST_Casework,              // Корпусная мебель
                    BuiltInCategory.OST_MechanicalEquipment,   // Механическое оборудование
                    BuiltInCategory.OST_PlumbingFixtures,      // Сантехнические приборы
                    BuiltInCategory.OST_LightingFixtures,      // Светильники
                    BuiltInCategory.OST_ElectricalEquipment,   // Электрооборудование
                    BuiltInCategory.OST_ElectricalFixtures,    // Электроприборы
                    BuiltInCategory.OST_CommunicationDevices,  // Устройства связи
                    BuiltInCategory.OST_FireAlarmDevices,      // Пожарная сигнализация
                    BuiltInCategory.OST_DataDevices,           // Устройства передачи данных
                    BuiltInCategory.OST_NurseCallDevices,      // Устройства вызова медсестры
                    BuiltInCategory.OST_SecurityDevices,       // Устройства безопасности
                    BuiltInCategory.OST_SpecialityEquipment,   // Специальное оборудование
                    BuiltInCategory.OST_LightingDevices,       // Осветительные устройства
                    BuiltInCategory.OST_Parking,               // Парковочные места
                    BuiltInCategory.OST_Cameras                // Камеры
                }
            },

            // Массовые/объёмные элементы
            { "MassBased", new BuiltInCategory[]
                {
                    BuiltInCategory.OST_Mass,                 // Массы
                    BuiltInCategory.OST_GenericModel,         // Общие модели
                    BuiltInCategory.OST_Entourage             // Окружение
                }
            },

            // Аннотационные и измерительные элементы
            { "Annotation", new BuiltInCategory[]
                {
                    BuiltInCategory.OST_TextNotes,           // Текстовые аннотации
                    BuiltInCategory.OST_DetailComponents,    // Деталировочные компоненты
                    BuiltInCategory.OST_KeynoteTags,         // Марки ключевых заметок
                    BuiltInCategory.OST_Dimensions,          // Размеры
                    BuiltInCategory.OST_Tags,                // Маркировки
                    BuiltInCategory.OST_ColorFillLegends     // Легенды цветового заполнения
                }
            },

            // Структурные элементы и фундаменты
            { "Structural", new BuiltInCategory[]
                {
                    BuiltInCategory.OST_StructuralFoundation, // Конструктивные фундаменты
                    // BuiltInCategory.OST_Footings,             // Фундаменты
                    BuiltInCategory.OST_Rebar                 // Арматура
                }
            },

            // Вспомогательные/координационные элементы
            { "Helper", new BuiltInCategory[]
                {
                    BuiltInCategory.OST_Levels,              // Уровни
                    BuiltInCategory.OST_Grids,               // Оси
                    BuiltInCategory.OST_DesignOptions,       // Варианты проектирования
                    BuiltInCategory.OST_Viewports,           // Видовые экраны
                    BuiltInCategory.OST_Sheets,              // Листы
                    BuiltInCategory.OST_Views,               // Виды
                    BuiltInCategory.OST_Schedules,           // Ведомости
                    BuiltInCategory.OST_Materials            // Материалы
                }
            }
        };
    }
}