using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitMCPCommandSet.Features.ElementFilter.Models;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders
{
    /// <summary>
    /// 字段构建上下文
    /// 提供缓存机制减少重复的 Revit API 调用
    /// </summary>
    public class FieldContext
    {
        private readonly Dictionary<string, object> _cache;

        /// <summary>
        /// Revit 文档
        /// </summary>
        public Document Document { get; }

        /// <summary>
        /// 当前元素
        /// </summary>
        public Element Element { get; }

        /// <summary>
        /// 结果字典（用于写入字段数据）
        /// </summary>
        public Dictionary<string, object> Result { get; }

        /// <summary>
        /// 过滤器设置
        /// </summary>
        public FilterSetting Settings { get; }

        /// <summary>
        /// 参数请求配置
        /// </summary>
        public ParameterRequest Parameters { get; }

        /// <summary>
        /// 警告信息列表
        /// </summary>
        public List<string> Warnings { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public FieldContext(Document document, Element element, Dictionary<string, object> result, FilterSetting settings = null)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Settings = settings;
            Parameters = settings?.Parameters;
            Warnings = new List<string>();
            _cache = new Dictionary<string, object>();
        }

        /// <summary>
        /// 获取缓存值，如果不存在则创建
        /// </summary>
        /// <typeparam name="T">缓存值类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="factory">创建函数</param>
        /// <returns>缓存值</returns>
        public T GetCached<T>(string key, Func<T> factory) where T : class
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached as T;
            }

            var value = factory();
            _cache[key] = value;
            return value;
        }

        /// <summary>
        /// 获取元素的类型元素（缓存）
        /// </summary>
        public Element TypeElement => GetCached("typeElement", () =>
        {
            var typeId = Element.GetTypeId();
            return typeId != ElementId.InvalidElementId ? Document.GetElement(typeId) : null;
        });

        /// <summary>
        /// 获取族信息（缓存，仅对族实例有效）
        /// </summary>
        public Family Family => GetCached("family", () =>
        {
            if (Element is FamilyInstance fi)
            {
                return fi.Symbol?.Family;
            }
            return null;
        });

        /// <summary>
        /// 获取标高信息（缓存）
        /// </summary>
        public Level Level => GetCached("level", () =>
        {
            // 尝试从多个来源获取标高
            Level level = null;

            // 1. 尝试从 SCHEDULE_LEVEL_PARAM 参数获取
            var levelParam = Element.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
            if (levelParam != null && levelParam.HasValue)
            {
                var levelId = levelParam.AsElementId();
                if (levelId != ElementId.InvalidElementId)
                {
                    level = Document.GetElement(levelId) as Level;
                }
            }

            // 2. 尝试从 FAMILY_LEVEL_PARAM 参数获取（族实例）
            if (level == null)
            {
                var familyLevelParam = Element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                if (familyLevelParam != null && familyLevelParam.HasValue)
                {
                    var levelId = familyLevelParam.AsElementId();
                    if (levelId != ElementId.InvalidElementId)
                    {
                        level = Document.GetElement(levelId) as Level;
                    }
                }
            }

            // 3. 特殊处理 Level 元素本身
            if (level == null && Element is Level elementAsLevel)
            {
                level = elementAsLevel;
            }

            return level;
        });

        /// <summary>
        /// 获取边界框（缓存）
        /// </summary>
        public BoundingBoxXYZ BoundingBox => GetCached("boundingBox", () =>
        {
            return Element.get_BoundingBox(Document.ActiveView);
        });

        /// <summary>
        /// 获取变换矩阵（缓存，仅对族实例有效）
        /// </summary>
        public Transform Transform => GetCached("transform", () =>
        {
            if (Element is FamilyInstance fi)
            {
                return fi.GetTransform();
            }
            return null;
        });

        /// <summary>
        /// 获取几何元素（缓存）
        /// </summary>
        public GeometryElement GeometryElement => GetCached("geometryElement", () =>
        {
            var options = new Options();
            return Element.get_Geometry(options);
        });

        /// <summary>
        /// 获取实例参数集合（缓存）
        /// </summary>
        public ParameterSet InstanceParameters => GetCached("instanceParameters", () =>
        {
            return Element.Parameters;
        });

        /// <summary>
        /// 获取类型参数集合（缓存）
        /// </summary>
        public ParameterSet TypeParameters => GetCached("typeParameters", () =>
        {
            var typeElement = TypeElement;
            return typeElement?.Parameters;
        });

        /// <summary>
        /// 检查缓存中是否存在指定键
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>如果存在返回 true</returns>
        public bool HasCached(string key)
        {
            return _cache.ContainsKey(key);
        }

        /// <summary>
        /// 添加警告信息
        /// </summary>
        /// <param name="message">警告消息</param>
        public void AddWarning(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Warnings.Add(message);
            }
        }

        /// <summary>
        /// 清理缓存（通常在处理完一个元素后调用）
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }
    }
}