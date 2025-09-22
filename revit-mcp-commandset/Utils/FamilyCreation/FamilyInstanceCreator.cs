using Autodesk.Revit.DB.Structure;

namespace RevitMCPCommandSet.Utils.FamilyCreation
{
    /// <summary>
    /// 族实例创建器类，用于配置和创建各种类型的族实例
    /// </summary>
    public class FamilyInstanceCreator
    {
        #region 属性定义

        /// <summary>
        /// Revit文档对象
        /// </summary>
        public Document doc { get; private set; }

        /// <summary>
        /// 族类型
        /// </summary>
        public FamilySymbol FamilySymbol { get; set; }

        /// <summary>
        /// 位置点
        /// </summary>
        public XYZ LocationPoint { get; set; }

        /// <summary>
        /// 基准线
        /// </summary>
        public Line LocationLine { get; set; }

        /// <summary>
        /// 底部标高
        /// </summary>
        public Level BaseLevel { get; set; }

        /// <summary>
        /// 顶部标高（用于TwoLevelsBased）
        /// </summary>
        public Level TopLevel { get; set; }

        /// <summary>
        /// 底部偏移（ft）
        /// </summary>
        public double BaseOffset { get; set; } = -1;

        /// <summary>
        /// 顶部偏移（ft）
        /// </summary>
        public double TopOffset { get; set; } = -1;

        /// <summary>
        /// 面方向
        /// </summary>
        public XYZ FaceDirection { get; set; }

        /// <summary>
        /// 手方向
        /// </summary>
        public XYZ HandDirection { get; set; }

        /// <summary>
        /// 视图对象
        /// </summary>
        public View View { get; set; }

        /// <summary>
        /// 宿主类别数组
        /// </summary>
        public BuiltInCategory[] HostCategories { get; set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化族实例创建器
        /// </summary>
        /// <param name="document">Revit文档对象</param>
        public FamilyInstanceCreator(Document document)
        {
            doc = document ?? throw new ArgumentNullException(nameof(document));
        }

        #endregion
        
        #region 根据FamilyPlacementType的专门配置方法
        /// <summary>
        /// 配置基于单个标高的族（OneLevelBased）
        /// </summary>
        /// <param name="familySymbol">族类型</param>
        /// <param name="locationPoint">位置点</param>
        /// <param name="baseLevel">基准标高（可选，为null时自动查找最近标高）</param>
        /// <returns>当前创建器实例，支持链式调用</returns>
        public FamilyInstanceCreator SetupOneLevelBased(FamilySymbol familySymbol, XYZ locationPoint, Level baseLevel = null)
        {
            FamilySymbol = familySymbol ?? throw new ArgumentNullException(nameof(familySymbol));
            LocationPoint = locationPoint ?? throw new ArgumentNullException(nameof(locationPoint));

            // 如果未指定标高，自动查找最近的标高
            if (baseLevel == null)
            {
                baseLevel = GetNearestLevel(doc,locationPoint.Z);
                if (baseLevel == null)
                    throw new InvalidOperationException("文档中没有找到任何标高！");
            }

            BaseLevel = baseLevel;

            // 计算偏移距离（LocationPoint的Z坐标 - 标高的Z坐标）
            BaseOffset = LocationPoint.Z - BaseLevel.Elevation;

            return this;
        }

        /// <summary>
        /// 配置基于单个标高和主体的族（OneLevelBasedHosted）
        /// </summary>
        /// <param name="familySymbol">族类型</param>
        /// <param name="locationPoint">位置点</param>
        /// <param name="baseLevel">基准标高（可选，指定时将关联到标高）</param>
        /// <returns>当前创建器实例，支持链式调用</returns>
        public FamilyInstanceCreator SetupOneLevelBasedHosted(FamilySymbol familySymbol, XYZ locationPoint, Level baseLevel = null)
        {
            FamilySymbol = familySymbol ?? throw new ArgumentNullException(nameof(familySymbol));
            LocationPoint = locationPoint ?? throw new ArgumentNullException(nameof(locationPoint));
            BaseLevel = baseLevel; // 可选，原代码中有if (baseLevel != null)的判断
            return this;
        }

        /// <summary>
        /// 配置基于两个标高的族（TwoLevelsBased）
        /// </summary>
        /// <param name="familySymbol">族类型</param>
        /// <param name="locationPoint">位置点</param>
        /// <param name="baseLevel">底部标高</param>
        /// <param name="topLevel">顶部标高（可选）</param>
        /// <param name="baseOffset">底部偏移（ft，默认-1表示不设置）</param>
        /// <param name="topOffset">顶部偏移（ft，默认-1表示不设置）</param>
        /// <returns>当前创建器实例，支持链式调用</returns>
        public FamilyInstanceCreator SetupTwoLevelsBased(FamilySymbol familySymbol, XYZ locationPoint, Level baseLevel,
            Level topLevel = null, double baseOffset = -1, double topOffset = -1)
        {
            FamilySymbol = familySymbol ?? throw new ArgumentNullException(nameof(familySymbol));
            LocationPoint = locationPoint ?? throw new ArgumentNullException(nameof(locationPoint));
            BaseLevel = baseLevel ?? throw new ArgumentNullException(nameof(baseLevel));
            TopLevel = topLevel; // 可选
            BaseOffset = baseOffset; // 默认-1，原代码中有if (baseOffset != -1)的判断
            TopOffset = topOffset; // 默认-1，原代码中有if (topOffset != -1)的判断
            return this;
        }

        /// <summary>
        /// 配置基于视图的族（ViewBased）
        /// </summary>
        /// <param name="familySymbol">族类型</param>
        /// <param name="locationPoint">位置点</param>
        /// <param name="view">目标视图</param>
        /// <returns>当前创建器实例，支持链式调用</returns>
        public FamilyInstanceCreator SetupViewBased(FamilySymbol familySymbol, XYZ locationPoint, View view)
        {
            FamilySymbol = familySymbol ?? throw new ArgumentNullException(nameof(familySymbol));
            LocationPoint = locationPoint ?? throw new ArgumentNullException(nameof(locationPoint));
            View = view ?? throw new ArgumentNullException(nameof(view));
            return this;
        }

        /// <summary>
        /// 配置基于工作平面的族（WorkPlaneBased）
        /// </summary>
        /// <param name="familySymbol">族类型</param>
        /// <param name="locationPoint">位置点</param>
        /// <param name="faceDirection">面方向（可选，未指定时系统自动生成）</param>
        /// <param name="hostCategories">宿主类别数组（可选）</param>
        /// <returns>当前创建器实例，支持链式调用</returns>
        public FamilyInstanceCreator SetupWorkPlaneBased(FamilySymbol familySymbol, XYZ locationPoint,
            XYZ faceDirection = null, params BuiltInCategory[] hostCategories)
        {
            FamilySymbol = familySymbol ?? throw new ArgumentNullException(nameof(familySymbol));
            LocationPoint = locationPoint ?? throw new ArgumentNullException(nameof(locationPoint));
            FaceDirection = faceDirection; // 可选，原代码中有if (faceDirection == null || faceDirection == XYZ.Zero)的判断
            HostCategories = hostCategories; // 可选
            return this;
        }

        /// <summary>
        /// 配置基于线且在工作平面上的族（CurveBased）
        /// </summary>
        /// <param name="familySymbol">族类型</param>
        /// <param name="locationLine">基准线</param>
        /// <param name="baseLevel">基准标高（可选，指定时关联标高，未指定时需要宿主面）</param>
        /// <param name="hostCategories">宿主类别数组（当baseLevel为null时使用）</param>
        /// <returns>当前创建器实例，支持链式调用</returns>
        public FamilyInstanceCreator SetupCurveBased(FamilySymbol familySymbol, Line locationLine,
            Level baseLevel = null, params BuiltInCategory[] hostCategories)
        {
            FamilySymbol = familySymbol ?? throw new ArgumentNullException(nameof(familySymbol));
            LocationLine = locationLine ?? throw new ArgumentNullException(nameof(locationLine));
            BaseLevel = baseLevel; // 可选，原代码中有if (baseLevel != null)的判断
            HostCategories = hostCategories; // 当baseLevel为null时需要用于查找宿主面
            return this;
        }

        /// <summary>
        /// 配置基于线且在特定视图中的族（CurveBasedDetail）
        /// </summary>
        /// <param name="familySymbol">族类型</param>
        /// <param name="locationLine">基准线</param>
        /// <param name="view">目标视图</param>
        /// <returns>当前创建器实例，支持链式调用</returns>
        public FamilyInstanceCreator SetupCurveBasedDetail(FamilySymbol familySymbol, Line locationLine, View view)
        {
            FamilySymbol = familySymbol ?? throw new ArgumentNullException(nameof(familySymbol));
            LocationLine = locationLine ?? throw new ArgumentNullException(nameof(locationLine));
            View = view ?? throw new ArgumentNullException(nameof(view));
            return this;
        }

        /// <summary>
        /// 配置结构曲线驱动的族（CurveDrivenStructural）
        /// </summary>
        /// <param name="familySymbol">族类型</param>
        /// <param name="locationLine">基准线</param>
        /// <param name="baseLevel">基准标高</param>
        /// <returns>当前创建器实例，支持链式调用</returns>
        public FamilyInstanceCreator SetupCurveDrivenStructural(FamilySymbol familySymbol, Line locationLine, Level baseLevel)
        {
            FamilySymbol = familySymbol ?? throw new ArgumentNullException(nameof(familySymbol));
            LocationLine = locationLine ?? throw new ArgumentNullException(nameof(locationLine));
            BaseLevel = baseLevel ?? throw new ArgumentNullException(nameof(baseLevel));
            return this;
        }

        /// <summary>
        /// 配置适应性族（Adaptive）
        /// </summary>
        /// <param name="familySymbol">族类型</param>
        /// <returns>当前创建器实例，支持链式调用</returns>
        public FamilyInstanceCreator SetupAdaptive(FamilySymbol familySymbol)
        {
            FamilySymbol = familySymbol ?? throw new ArgumentNullException(nameof(familySymbol));
            throw new NotImplementedException("未实现FamilyPlacementType.Adaptive创建方法！");
        }

        #endregion

        #region 通用属性设置方法
        /// <summary>
        /// 重置所有配置属性
        /// </summary>
        /// <returns>当前创建器实例，支持链式调用</returns>
        public FamilyInstanceCreator Reset()
        {
            FamilySymbol = null;
            LocationPoint = null;
            LocationLine = null;
            BaseLevel = null;
            TopLevel = null;
            BaseOffset = -1;
            TopOffset = -1;
            FaceDirection = null;
            HandDirection = null;
            View = null;
            HostCategories = null;
            return this;
        }

        #endregion

        #region 执行创建
        /// <summary>
        /// 执行创建族实例
        /// </summary>
        /// <returns>创建的族实例，失败返回null</returns>
        public FamilyInstance Create()
        {
            // 基本参数检查
            if (FamilySymbol == null)
                System.Diagnostics.Trace.WriteLine($"[Error] 必要参数{typeof(FamilySymbol)} {nameof(FamilySymbol)}缺失！");

            // 激活族模型
            if (!FamilySymbol.IsActive)
                FamilySymbol.Activate();

            FamilyInstance instance = null;

            // 根据族的放置类型选择创建方法
            switch (FamilySymbol.Family.FamilyPlacementType)
            {
                // 基于单个标高的族（如：公制常规模型）
                case FamilyPlacementType.OneLevelBased:
                    if (LocationPoint == null)
                        System.Diagnostics.Trace.WriteLine($"[Error] 必要参数{typeof(XYZ)} {nameof(LocationPoint)}缺失！");
                    //if (BaseLevel == null)
                    //    System.Diagnostics.Trace.WriteLine($"[Error] 必要参数{typeof(Level)} {nameof(BaseLevel)}缺失！");

                    if (BaseLevel != null)
                    {
                        // 统一使用带标高信息的创建方法
                        instance = doc.Create.NewFamilyInstance(
                        LocationPoint,                  // 实例将被放置的物理位置
                        FamilySymbol,                   // 表示要插入的实例类型的 FamilySymbol 对象
                        BaseLevel,                      // 用作对象基准标高的 Level 对象
                        StructuralType.NonStructural);  // 如果是结构构件，则指定构件的类型
                    }
                    else
                    {
                        // 统一使用带标高信息的创建方法
                        instance = doc.Create.NewFamilyInstance(
                        LocationPoint,                  // 实例将被放置的物理位置
                        FamilySymbol,                   // 表示要插入的实例类型的 FamilySymbol 对象
                        StructuralType.NonStructural);  // 如果是结构构件，则指定构件的类型
                    }

                    // 创建完成后设置偏移值
                    if (instance != null && BaseOffset != -1)
                    {
                        Parameter baseOffsetParam = instance.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM);
                        if (baseOffsetParam != null && baseOffsetParam.StorageType == StorageType.Double)
                        {
                            baseOffsetParam.Set(BaseOffset);
                        }
                        else
                        {
                            // 尝试其他可能的偏移参数
                            baseOffsetParam = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                            if (baseOffsetParam != null && baseOffsetParam.StorageType == StorageType.Double)
                            {
                                baseOffsetParam.Set(BaseOffset);
                            }
                        }
                    }
                    break;

                // 基于单个标高和主体的族（如：门、窗）
                case FamilyPlacementType.OneLevelBasedHosted:
                    if (LocationPoint == null)
                        System.Diagnostics.Trace.WriteLine($"[Error] 必要参数{typeof(XYZ)} {nameof(LocationPoint)}缺失！");
                    // 自动查找最近的宿主元素
                    Element host = GetNearestHostElement(doc,LocationPoint, FamilySymbol);
                    if (host == null)
                        System.Diagnostics.Trace.WriteLine($"[Error] 找不到合规的的宿主信息！");
                    // 布置方向由主体的创建方向决定
                    // 带标高信息
                    if (BaseLevel != null)
                    {
                        instance = doc.Create.NewFamilyInstance(
                            LocationPoint,                  // 实例将被放置在指定标高上的物理位置
                            FamilySymbol,                   // 表示要插入的实例类型的 FamilySymbol 对象
                            host,                           // 实例将嵌入其中的主体对象
                            BaseLevel,                      // 用作对象基准标高的 Level 对象
                            StructuralType.NonStructural);  // 如果是结构构件，则指定构件的类型
                    }
                    // 不带标高信息
                    else
                    {
                        instance = doc.Create.NewFamilyInstance(
                            LocationPoint,                  // 实例将被放置在指定标高上的物理位置
                            FamilySymbol,                   // 表示要插入的实例类型的 FamilySymbol 对象
                            host,                           // 实例将嵌入其中的主体对象
                            StructuralType.NonStructural);  // 如果是结构构件，则指定构件的类型
                    }
                    break;

                // 基于两个标高的族（如：柱子）
                case FamilyPlacementType.TwoLevelsBased:
                    if (LocationPoint == null)
                        System.Diagnostics.Trace.WriteLine($"[Error] 必要参数{typeof(XYZ)} {nameof(LocationPoint)}缺失！");
                    if (BaseLevel == null)
                        System.Diagnostics.Trace.WriteLine($"[Error] 必要参数{typeof(Level)} {nameof(BaseLevel)}缺失！");
                    // 判断是结构柱还是建筑柱
                    StructuralType structuralType = StructuralType.NonStructural;
                    if (FamilySymbol.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralColumns)
                        structuralType = StructuralType.Column;
                    instance = doc.Create.NewFamilyInstance(
                        LocationPoint,              // 实例将被放置的物理位置
                        FamilySymbol,               // 表示要插入的实例类型的 FamilySymbol 对象
                        BaseLevel,                  // 用作对象基准标高的 Level 对象
                        structuralType);            // 如果是结构构件，则指定构件的类型
                                                    // 设置底部标高、顶部标高、底部偏移、顶部偏移
                    if (instance != null)
                    {
                        // 设置柱子的基准标高和顶部标高
                        if (BaseLevel != null)
                        {
                            Parameter baseLevelParam = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                            if (baseLevelParam != null)
                                baseLevelParam.Set(BaseLevel.Id);
                        }
                        if (TopLevel != null)
                        {
                            Parameter topLevelParam = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                            if (topLevelParam != null)
                                topLevelParam.Set(TopLevel.Id);
                        }
                        // 获取底部偏移参数
                        if (BaseOffset != -1)
                        {
                            Parameter baseOffsetParam = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                            if (baseOffsetParam != null && baseOffsetParam.StorageType == StorageType.Double)
                            {
                                // 将毫米转换为Revit内部单位
                                double baseOffsetInternal = BaseOffset;
                                baseOffsetParam.Set(baseOffsetInternal);
                            }
                        }
                        // 获取顶部偏移参数
                        if (TopOffset != -1)
                        {
                            Parameter topOffsetParam = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                            if (topOffsetParam != null && topOffsetParam.StorageType == StorageType.Double)
                            {
                                // 将毫米转换为Revit内部单位
                                double topOffsetInternal = TopOffset;
                                topOffsetParam.Set(topOffsetInternal);
                            }
                        }
                    }
                    break;

                // 族是视图专有的（例如，详图注释）
                case FamilyPlacementType.ViewBased:
                    if (LocationPoint == null)
                        System.Diagnostics.Trace.WriteLine($"[Error] 必要参数{typeof(XYZ)} {nameof(LocationPoint)}缺失！");
                    instance = doc.Create.NewFamilyInstance(
                        LocationPoint,  // 族实例的原点。如果创建在平面视图（ViewPlan）上，该原点将被投影到平面视图上
                        FamilySymbol,   // 表示要插入的实例类型的族符号对象
                        View);          // 放置族实例的2D视图
                    break;

                // 基于工作平面的族（如：基于面的公制常规模型，包括基于面、基于墙等）
                case FamilyPlacementType.WorkPlaneBased:
                    if (LocationPoint == null)
                        System.Diagnostics.Trace.WriteLine($"[Error] 必要参数{typeof(XYZ)} {nameof(LocationPoint)}缺失！");
                    // 获取最近的宿主面
                    Reference hostFace = GetNearestFaceReference(doc,LocationPoint, 1000 / 304.8, HostCategories);
                    if (hostFace == null)
                        System.Diagnostics.Trace.WriteLine($"[Error] 找不到合规的的宿主信息！");

                    // 将点投影到宿主面上
                    XYZ projectedPoint = ProjectPointToFace(doc, LocationPoint, hostFace);
                    if (projectedPoint == null)
                    {
                        System.Diagnostics.Trace.WriteLine($"[Error] 族实例创建失败：无法将点投影到宿主面上，原始点：{LocationPoint}");
                        instance = null;
                        break;
                    }
                    if (HandDirection == null || HandDirection == XYZ.Zero)
                    {
                        var result = GenerateDefaultOrientation(doc, hostFace);
                        HandDirection = result.HandOrientation;
                    }
                    try
                    {
                        // 使用投影后的点和方向在面上创建族实例
                        instance = doc.Create.NewFamilyInstance(
                            hostFace,               // 对面的引用  
                            projectedPoint,         // 投影到面上的点
                            HandDirection,          // 定义族实例方向的向量
                            FamilySymbol);          // 表示要插入的实例类型的 FamilySymbol 对象
                    }
                    catch (Autodesk.Revit.Exceptions.ArgumentsInconsistentException ex)
                    {
                        // 记录方向与面法线平行的错误
                        System.Diagnostics.Trace.WriteLine($"[Error] 族实例创建失败：方向参数不一致 - {ex.Message}，原始点：{LocationPoint}");
                        instance = null;
                    }
                    catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
                    {
                        // 记录无法在面上创建族实例的错误
                        System.Diagnostics.Trace.WriteLine($"[Error] 族实例创建失败：无法在面上创建族实例 - {ex.Message}，原始点：{LocationPoint}，投影点：{projectedPoint}");
                        instance = null;
                    }
                    catch (Exception ex)
                    {
                        // 记录其他未预期的错误
                        System.Diagnostics.Trace.WriteLine($"[Error] 族实例创建失败：未知错误 - {ex.Message}，原始点：{LocationPoint}");
                        instance = null;
                    }
                    break;

                // 基于线且在工作平面上的族（如：基于线的公制常规模型）
                case FamilyPlacementType.CurveBased:
                    if (LocationLine == null)
                        System.Diagnostics.Trace.WriteLine($"[Error] 必要参数{typeof(Line)} {nameof(LocationLine)}缺失！");
                    // 带标高信息
                    if (BaseLevel != null)
                    {
                        instance = doc.Create.NewFamilyInstance(
                            LocationLine,                   // 族实例基于的曲线
                            FamilySymbol,                   // 一个FamilySymbol对象，表示要插入的实例的类型。请注意，此Symbol必须表示其 FamilyPlacementType 为 WorkPlaneBased 或 CurveBased 的族
                            BaseLevel,                      // 一个Level对象，用作该对象的基准标高
                            StructuralType.NonStructural);  // 如果是结构构件，则指定构件的类型
                    }
                    // 不带标高信息
                    else
                    {
                        // 获取最近的宿主面（不允许有误差）
                        Reference lineHostFace = GetNearestFaceReference(doc, LocationLine.Evaluate(0.5, true), 1e-5, HostCategories);
                        if (lineHostFace == null)
                        { 
                            System.Diagnostics.Trace.WriteLine($"[Error] 布置线不在最近的宿主面上！");
                            break;
                        }
                        instance = doc.Create.NewFamilyInstance(
                            lineHostFace,   // 对面的引用 
                            LocationLine,   // 族实例基于的曲线
                            FamilySymbol);  // 一个FamilySymbol对象，表示要插入的实例的类型。请注意，此Symbol必须表示其 FamilyPlacementType 为 WorkPlaneBased 或 CurveBased 的族
                    }
                    break;

                // 基于线且在特定视图中的族（如：详图组件）
                case FamilyPlacementType.CurveBasedDetail:
                    if (LocationLine == null)
                        System.Diagnostics.Trace.WriteLine($"[Error] 必要参数{typeof(Line)} {nameof(LocationLine)}缺失！");
                    if (View == null)
                        System.Diagnostics.Trace.WriteLine($"[Error] 必要参数{typeof(View)} {nameof(View)}缺失！");
                    instance = doc.Create.NewFamilyInstance(
                        LocationLine,   // 族实例的线位置。该线必须位于视图平面内
                        FamilySymbol,   // 表示要插入的实例类型的族符号对象
                        View);          // 放置族实例的2D视图
                    break;

                // 结构曲线驱动的族（如：梁、支撑或斜柱）
                case FamilyPlacementType.CurveDrivenStructural:
                    if (LocationLine == null)
                        System.Diagnostics.Trace.WriteLine($"[Error] 必要参数{typeof(Line)} {nameof(LocationLine)}缺失！");
                    if (BaseLevel == null)
                        System.Diagnostics.Trace.WriteLine($"[Error] 必要参数{typeof(Level)} {nameof(BaseLevel)}缺失！");
                    instance = doc.Create.NewFamilyInstance(
                        LocationLine,                   // 族实例基于的曲线
                        FamilySymbol,                   // 一个FamilySymbol对象，表示要插入的实例的类型。请注意，此Symbol必须表示其 FamilyPlacementType 为 WorkPlaneBased 或 CurveBased 的族
                        BaseLevel,                      // 一个Level对象，用作该对象的基准标高
                        StructuralType.Beam);           // 如果是结构构件，则指定构件的类型
                    break;

                // 适应性族（如：自适应公制常规模型、幕墙嵌板）
                case FamilyPlacementType.Adaptive:
                    throw new NotImplementedException("未实现FamilyPlacementType.Adaptive创建方法！");

                default:
                    break;
            }
            return instance;
        }

        #endregion

        #region 辅助方法
        /// <summary>
        /// 获取距离点最近的面Reference
        /// </summary>
        /// <param name="doc">当前文档</param>
        /// <param name="location">目标点位置</param>
        /// <param name="radius">搜索半径（内部单位）</param>
        /// <param name="categories">要搜索的内置类别数组，如果为null则使用默认类别</param>
        /// <returns>最近面的Reference，未找到返回null</returns>
        private static Reference GetNearestFaceReference(Document doc, XYZ location, double radius = 100 / 304.8, params BuiltInCategory[] categories)
        {
            try
            {
                #region Step1 验证参数
                if (doc == null || location == null)
                    throw new System.ArgumentNullException("doc 或 location 不能为空");
                #endregion

                #region Step2 分类过滤器准备
                // 如果未指定类别，使用默认的建筑类别
                if (categories == null || categories.Length == 0)
                {
                    categories = new BuiltInCategory[]
                    {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Ceilings,
                BuiltInCategory.OST_GenericModel,
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralColumns
                    };
                }

                // 构造分类过滤器
                List<ElementFilter> categoryFilters = new List<ElementFilter>();
                foreach (BuiltInCategory category in categories)
                {
                    categoryFilters.Add(new ElementCategoryFilter(category));
                }
                LogicalOrFilter categoryFilter = new LogicalOrFilter(categoryFilters);
                #endregion

                #region Step3 方向设置
                // 设置6个方向
                XYZ[] directions = new XYZ[]
                {
            XYZ.BasisX,
            -XYZ.BasisX,
            XYZ.BasisY,
            -XYZ.BasisY,
            XYZ.BasisZ,
            -XYZ.BasisZ
                };
                #endregion

                #region Step4 使用临时事务创建临时3D视图
                Reference nearestFace = null;
                double minDistance = double.MaxValue;
                bool found = false;

                using (SubTransaction trans = new SubTransaction(doc))
                {
                    trans.Start();
                    try
                    {
                        // 查找3D视图类型
                        ViewFamilyType vft = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                        if (vft == null)
                            throw new System.InvalidOperationException($"找不到3D视图类型，无法进行射线检测");

                        // 创建isometric临时视图
                        View3D temp3DView = View3D.CreateIsometric(doc, vft.Id);

                        // 创建ReferenceIntersector
                        ReferenceIntersector refIntersector = new ReferenceIntersector(categoryFilter, FindReferenceTarget.Face, temp3DView);
                        refIntersector.FindReferencesInRevitLinks = true;

                        // 六方向射线查找
                        foreach (XYZ direction in directions)
                        {
                            IList<ReferenceWithContext> refs = refIntersector.Find(location, direction);

                            foreach (ReferenceWithContext rwc in refs)
                            {
                                Reference reference = rwc.GetReference();
                                if (reference.ElementReferenceType != ElementReferenceType.REFERENCE_TYPE_SURFACE)
                                    continue;

                                double distance = rwc.Proximity;
                                if (distance <= radius && distance < minDistance)
                                {
                                    minDistance = distance;
                                    nearestFace = reference;
                                    found = true;
                                }
                            }
                        }

                        // 回滚事务清理临时视图
                        trans.RollBack();

                        // 返回结果
                        if (found && nearestFace != null && nearestFace.ElementReferenceType == ElementReferenceType.REFERENCE_TYPE_SURFACE)
                            return nearestFace;
                        else
                            return null;
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack(); // 发生异常也回滚
                        System.Diagnostics.Trace.WriteLine("[GetNearestFaceReference] 射线检测异常: " + ex.Message);
                        throw;
                    }
                }
                #endregion
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"获取最近面时发生错误：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取距离点最近的可作为宿主的元素
        /// </summary>
        /// <param name="doc">当前文档</param>
        /// <param name="location">目标点位置</param>
        /// <param name="familySymbol">族类型，用于判断宿主类型</param>
        /// <param name="radius">搜索半径（内部单位）</param>
        /// <returns>最近的宿主元素，未找到返回null</returns>
        private static Element GetNearestHostElement(Document doc, XYZ location, FamilySymbol familySymbol, double radius = 5.0)
        {
            try
            {
                #region Step1 参数校验与宿主行为
                if (doc == null || location == null || familySymbol == null)
                    throw new System.ArgumentNullException("doc 或 location 或 familySymbol 不能为空");

                // 获取族的宿主行为参数
                Parameter hostParam = familySymbol.Family.get_Parameter(BuiltInParameter.FAMILY_HOSTING_BEHAVIOR);
                int hostingBehavior = hostParam?.AsInteger() ?? 0;
                #endregion

                #region Step2 根据宿主行为生成过滤器
                ElementFilter classFilter;
                switch (hostingBehavior)
                {
                    case 1: // Wall based
                        classFilter = new ElementClassFilter(typeof(Wall));
                        break;
                    case 2: // Floor based
                        classFilter = new ElementClassFilter(typeof(Floor));
                        break;
                    case 3: // Ceiling based
                        classFilter = new ElementClassFilter(typeof(Ceiling));
                        break;
                    case 4: // Roof based
                        classFilter = new ElementClassFilter(typeof(RoofBase));
                        break;
                    default:
                        // 不支持的宿主类型
                        return null;
                }
                #endregion

                #region Step3 方向设置
                // 射线方向（6正交）
                XYZ[] directions = new XYZ[]
                {
            XYZ.BasisX,    // X+
            -XYZ.BasisX,   // X-
            XYZ.BasisY,    // Y+
            -XYZ.BasisY,   // Y-
            XYZ.BasisZ,    // Z+
            -XYZ.BasisZ    // Z-
                };
                #endregion

                #region Step4 创建临时3D视图 + 回滚事务执行射线查找
                Element nearestHost = null;
                double minDistance = double.MaxValue;
                bool found = false;

                using (SubTransaction trans = new SubTransaction(doc))
                {
                    trans.Start();
                    try
                    {
                        // 查找3D视图类型
                        ViewFamilyType vft = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                        if (vft == null)
                            throw new System.InvalidOperationException($"找不到3D视图类型，无法进行射线检测");

                        // 创建临时isometric视图
                        View3D temp3DView = View3D.CreateIsometric(doc, vft.Id);

                        ReferenceIntersector refIntersector = new ReferenceIntersector(
                            classFilter, FindReferenceTarget.Element, temp3DView);
                        refIntersector.FindReferencesInRevitLinks = true;

                        // 遍历6方向
                        foreach (XYZ direction in directions)
                        {
                            IList<ReferenceWithContext> references = refIntersector.Find(location, direction);

                            foreach (ReferenceWithContext rwc in references)
                            {
                                double distance = rwc.Proximity;

                                if (distance <= radius && distance < minDistance)
                                {
                                    minDistance = distance;
                                    nearestHost = doc.GetElement(rwc.GetReference().ElementId);
                                    found = true;
                                }
                            }
                        }

                        trans.RollBack(); // 清理临时视图

                        if (found && nearestHost != null)
                            return nearestHost;
                        else
                            return null;
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        System.Diagnostics.Trace.WriteLine($"[GetNearestHostElement] 射线宿主检测异常: {ex.Message}");
                        throw;
                    }
                }
                #endregion
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"获取最近宿主元素时发生错误：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将点投影到指定面上
        /// </summary>
        /// <param name="doc">当前文档</param>
        /// <param name="point">要投影的点</param>
        /// <param name="faceReference">目标面的Reference</param>
        /// <returns>投影后的点，失败返回null</returns>
        private static XYZ ProjectPointToFace(Document doc, XYZ point, Reference faceReference)
        {
            #region Step1 输入调试信息
            if (doc == null)
            {
                System.Diagnostics.Trace.WriteLine("输入参数 doc 为空！");
                return null;
            }
            if (point == null)
            {
                System.Diagnostics.Trace.WriteLine("输入参数 point 为空！");
                return null;
            }
            if (faceReference == null)
            {
                System.Diagnostics.Trace.WriteLine("输入参数 faceReference 为空！");
                return null;
            }
            #endregion

            try
            {
                #region Step2 获取目标Face对象
                Element element = doc.GetElement(faceReference.ElementId);
                if (element == null)
                {
                    System.Diagnostics.Trace.WriteLine($"通过faceReference.ElementId({faceReference.ElementId.IntegerValue})获取不到Element对象！");
                    return null;
                }

                object geoObj = element.GetGeometryObjectFromReference(faceReference);
                if (geoObj == null)
                {
                    System.Diagnostics.Trace.WriteLine("GetGeometryObjectFromReference返回null");
                    return null;
                }

                Face face = geoObj as Face;
                if (face == null)
                {
                    System.Diagnostics.Trace.WriteLine($"GetGeometryObjectFromReference类型不是Face，实际类型为：{geoObj.GetType().FullName}");
                    return null;
                }
                #endregion

                #region Step3 投影操作
                IntersectionResult result = face.Project(point);

                if (result != null)
                {
                    return result.XYZPoint;
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine("投影结果为null（result == null）。");
                }
                #endregion

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"点投影出现异常：{ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
        
        /// <summary>
        /// 生成默认的朝向和手向
        /// 垂直面：FacingOrientation指向Z轴正方向，HandOrientation在面上且垂直于FacingOrientation
        /// 水平面：FacingOrientation指向短边方向，HandOrientation指向长边方向
        /// </summary>
        /// <param name="doc">当前文档</param>
        /// <param name="hostFace">宿主面引用</param>
        /// <returns>朝向和手向的元组</returns>
        private static (XYZ FacingOrientation, XYZ HandOrientation) GenerateDefaultOrientation(Document doc, Reference hostFace)
        {
            var facingOrientation = new XYZ();  // 朝向方向：族内Y轴正方向在载入后的朝向
            var handOrientation = new XYZ();    // 手向方向：族内X轴正方向在载入后的朝向

            // Step1 从Reference中获取面对象
            Face face = doc.GetElement(hostFace.ElementId).GetGeometryObjectFromReference(hostFace) as Face;
            if (face == null)
                return (facingOrientation, handOrientation);

            // Step2 获取面法向量
            XYZ faceNormal = null;
            if (face is PlanarFace planarFace)
                faceNormal = planarFace.FaceNormal;
            else
                faceNormal = face.ComputeNormal(new UV(0.5, 0.5));

            // Step3 判断面是垂直面还是水平面
            // 如果法向量的Z分量小于XY分量，说明是垂直面
            bool isVerticalFace = Math.Abs(faceNormal.Z) < 0.7; // 约45度角以下认为是垂直面

            if (isVerticalFace)
            {
                // 垂直面：FacingOrientation指向Z轴正方向
                facingOrientation = XYZ.BasisZ;

                // HandOrientation需要垂直于FacingOrientation且在面上
                // 用Z轴和面法向量的叉积得到在面上的水平方向
                handOrientation = XYZ.BasisZ.CrossProduct(faceNormal).Normalize();

                // 如果叉积结果为零向量（面法向量平行于Z轴的特殊情况），使用默认方向
                if (handOrientation.GetLength() < 1e-10)
                {
                    handOrientation = XYZ.BasisX; // 使用X轴作为默认方向
                }

                // 确保符合右手定则（拇指：HandOrientation，食指：FacingOrientation，中指：FaceNormal）
                if (!IsRightHandRuleCompliant(handOrientation,facingOrientation, faceNormal))
                {
                    handOrientation = handOrientation.Negate();
                }
            }
            else
            {
                // 水平面：FacingOrientation指向短边方向，HandOrientation指向长边方向
                var result = GetMainDirections(face);
                var primaryDirection = result.PrimaryDirection;      // 长边方向
                var secondaryDirection = result.SecondaryDirection;  // 短边方向

                // FacingOrientation指向短边方向，HandOrientation指向长边方向
                facingOrientation = secondaryDirection;
                handOrientation = primaryDirection;

                // 判断是否符合右手定则（拇指：HandOrientation，食指：FacingOrientation，中指：FaceNormal）
                if (!IsRightHandRuleCompliant(handOrientation,facingOrientation, faceNormal))
                {
                    var newHandOrientation = GenerateIndexFinger(facingOrientation,faceNormal);
                    if (newHandOrientation != null)
                    {
                        handOrientation = newHandOrientation;
                    }
                    else
                    {
                        // 如果无法生成符合条件的手向，尝试反转当前手向
                        handOrientation = handOrientation.Negate();
                    }
                }
            }

            return (facingOrientation, handOrientation);
        }

        /// <summary>
        /// 判断三个向量是否同时符合右手定则且互相严格垂直
        /// </summary>
        /// <param name="thumb">拇指方向向量</param>
        /// <param name="indexFinger">食指方向向量</param>
        /// <param name="middleFinger">中指方向向量</param>
        /// <param name="tolerance">判断的容差，默认为1e-6</param>
        /// <returns>如果三个向量符合右手定则且互相垂直则返回true，否则返回false</returns>
        private static bool IsRightHandRuleCompliant(XYZ thumb, XYZ indexFinger, XYZ middleFinger, double tolerance = 1e-6)
        {
            // 检查三个向量是否互相垂直（所有点积都接近0）
            double dotThumbIndex = Math.Abs(thumb.DotProduct(indexFinger));
            double dotThumbMiddle = Math.Abs(thumb.DotProduct(middleFinger));
            double dotIndexMiddle = Math.Abs(indexFinger.DotProduct(middleFinger));

            bool areOrthogonal = (dotThumbIndex <= tolerance) &&
                                  (dotThumbMiddle <= tolerance) &&
                                  (dotIndexMiddle <= tolerance);

            // 只有在三个向量互相垂直的情况下才检查右手定则
            if (!areOrthogonal)
                return false;

            // 计算叉积向量与拇指的点积，判断是否符合右手定则
            XYZ crossProduct = indexFinger.CrossProduct(middleFinger);
            double rightHandTest = crossProduct.DotProduct(thumb);

            // 点积为正值表示符合右手定则
            return rightHandTest > tolerance;
        }

        /// <summary>
        /// 根据拇指和中指方向生成符合右手定则的食指方向
        /// </summary>
        /// <param name="thumb">拇指方向向量</param>
        /// <param name="middleFinger">中指方向向量</param>
        /// <param name="tolerance">垂直判断的容差，默认为1e-6</param>
        /// <returns>生成的食指方向向量，如果输入向量不垂直则返回null</returns>
        private static XYZ GenerateIndexFinger(XYZ thumb, XYZ middleFinger, double tolerance = 1e-6)
        {
            // 首先归一化输入向量
            XYZ normalizedThumb = thumb.Normalize();
            XYZ normalizedMiddleFinger = middleFinger.Normalize();

            // 检查两个向量是否垂直（点积接近于0）
            double dotProduct = normalizedThumb.DotProduct(normalizedMiddleFinger);

            // 如果点积的绝对值大于容差，则向量不垂直
            if (Math.Abs(dotProduct) > tolerance)
            {
                return null;
            }

            // 通过叉积计算食指方向并取反
            XYZ indexFinger = normalizedMiddleFinger.CrossProduct(normalizedThumb).Negate();

            // 返回归一化后的食指方向向量
            return indexFinger.Normalize();
        }

        /// <summary>
        /// 提取面的两个主要方向向量
        /// </summary>
        /// <param name="face">输入面</param>
        /// <returns>包含主方向和次方向的元组</returns>
        /// <exception cref="ArgumentNullException">当面为空时抛出</exception>
        /// <exception cref="ArgumentException">当面的轮廓不足以形成有效形状时抛出</exception>
        /// <exception cref="InvalidOperationException">当无法提取有效方向时抛出</exception>
        private static (XYZ PrimaryDirection, XYZ SecondaryDirection) GetMainDirections(Face face)
        {
            // 1. 参数验证
            if (face == null)
                throw new ArgumentNullException(nameof(face), "面不能为空");

            // 2. 获取面的法向量，用于后续可能需要的垂直向量计算
            XYZ faceNormal = face.ComputeNormal(new UV(0.5, 0.5));

            // 3. 获取面的外轮廓
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            if (edgeLoops.Size == 0)
                throw new ArgumentException("面没有有效的边循环", nameof(face));

            // 通常第一个循环是外轮廓
            EdgeArray outerLoop = edgeLoops.get_Item(0);

            // 4. 计算每条边的方向向量和长度
            List<XYZ> edgeDirections = new List<XYZ>();  // 存储每条边的单位向量方向
            List<double> edgeLengths = new List<double>(); // 存储每条边的长度

            foreach (Edge edge in outerLoop)
            {
                Curve curve = edge.AsCurve();
                XYZ startPoint = curve.GetEndPoint(0);
                XYZ endPoint = curve.GetEndPoint(1);

                // 计算从起点到终点的向量
                XYZ direction = endPoint - startPoint;
                double length = direction.GetLength();

                // 忽略太短的边（可能是由于顶点重合或数值精度问题）
                if (length > 1e-10)
                {
                    edgeDirections.Add(direction.Normalize());  // 存储归一化后的方向向量
                    edgeLengths.Add(length);                    // 存储边长
                }
            }

            if (edgeDirections.Count < 4) // 确保至少有4条边
            {
                throw new ArgumentException("提供的面没有足够的边来形成有效的形状", nameof(face));
            }

            // 5. 将相似方向的边分组
            List<List<int>> directionGroups = new List<List<int>>();  // 存储方向组，每组包含边的索引

            for (int i = 0; i < edgeDirections.Count; i++)
            {
                bool foundGroup = false;
                XYZ currentDirection = edgeDirections[i];

                // 尝试将当前边加入已有的方向组
                for (int j = 0; j < directionGroups.Count; j++)
                {
                    var group = directionGroups[j];
                    // 计算当前组的加权平均方向
                    XYZ groupAvgDir = CalculateWeightedAverageDirection(group, edgeDirections, edgeLengths);

                    // 检查当前方向是否与组的平均方向相似（包括正反方向）
                    double dotProduct = Math.Abs(groupAvgDir.DotProduct(currentDirection));
                    if (dotProduct > 0.8) // 约30度内的偏差视为相似方向
                    {
                        group.Add(i);  // 将当前边的索引添加到该方向组
                        foundGroup = true;
                        break;
                    }
                }

                // 如果当前边与所有已有组都不相似，创建新组
                if (!foundGroup)
                {
                    List<int> newGroup = new List<int> { i };
                    directionGroups.Add(newGroup);
                }
            }

            // 6. 计算每个方向组的总权重（边长和）和平均方向
            List<double> groupWeights = new List<double>();
            List<XYZ> groupDirections = new List<XYZ>();

            foreach (var group in directionGroups)
            {
                // 计算该组所有边的长度总和
                double totalLength = 0;
                foreach (int edgeIndex in group)
                {
                    totalLength += edgeLengths[edgeIndex];
                }
                groupWeights.Add(totalLength);

                // 计算该组的加权平均方向
                groupDirections.Add(CalculateWeightedAverageDirection(group, edgeDirections, edgeLengths));
            }

            // 7. 按照权重排序，提取主要方向
            int[] sortedIndices = Enumerable.Range(0, groupDirections.Count)
                .OrderByDescending(i => groupWeights[i])
                .ToArray();

            // 8. 构造结果
            if (groupDirections.Count >= 2)
            {
                // 有至少两个方向组，取权重最大的两组作为主方向和次方向
                int primaryIndex = sortedIndices[0];
                int secondaryIndex = sortedIndices[1];

                return (
                    PrimaryDirection: groupDirections[primaryIndex],      // 主方向
                    SecondaryDirection: groupDirections[secondaryIndex]   // 次方向
                );
            }
            else if (groupDirections.Count == 1)
            {
                // 只有一个方向组，手动创建与主方向垂直的次方向
                XYZ primaryDirection = groupDirections[0];
                // 使用面法向量和主方向的叉积创建垂直向量
                XYZ secondaryDirection = faceNormal.CrossProduct(primaryDirection).Normalize();

                return (
                    PrimaryDirection: primaryDirection,         // 主方向 
                    SecondaryDirection: secondaryDirection      // 人工构造的垂直次方向
                );
            }
            else
            {
                // 无法提取有效的方向（极少发生）
                throw new InvalidOperationException("无法从面中提取有效的方向");
            }
        }

        /// <summary>
        /// 根据边长计算一组边的加权平均方向
        /// </summary>
        /// <param name="edgeIndices">边的索引列表</param>
        /// <param name="directions">所有边的方向向量</param>
        /// <param name="lengths">所有边的长度</param>
        /// <returns>归一化的加权平均方向向量</returns>
        private static XYZ CalculateWeightedAverageDirection(List<int> edgeIndices, List<XYZ> directions, List<double> lengths)
        {
            if (edgeIndices.Count == 0)
                return null;

            double sumX = 0, sumY = 0, sumZ = 0;
            XYZ referenceDir = directions[edgeIndices[0]];  // 使用组内第一个方向作为参考

            foreach (int i in edgeIndices)
            {
                XYZ currentDir = directions[i];

                // 计算当前方向与参考方向的点积，判断是否需要反转
                double dot = referenceDir.DotProduct(currentDir);

                // 如果方向相反（点积为负），反转该向量再计算贡献
                // 这确保同一组内的向量指向一致，避免相互抵消
                double factor = (dot >= 0) ? lengths[i] : -lengths[i];

                // 累加向量分量（带权重）
                sumX += currentDir.X * factor;
                sumY += currentDir.Y * factor;
                sumZ += currentDir.Z * factor;
            }

            // 创建合成向量并归一化
            XYZ avgDir = new XYZ(sumX, sumY, sumZ);
            double magnitude = avgDir.GetLength();

            // 防止零向量
            if (magnitude < 1e-10)
                return referenceDir;  // 回退至参考方向

            return avgDir.Normalize();  // 返回归一化后的方向向量
        }

        /// <summary>
        /// 查找距离给定高度最近的标高
        /// </summary>
        /// <param name="doc">当前Revit文档</param>
        /// <param name="height">目标高度（Revit内部单位）</param>
        /// <returns>距离目标高度最近的标高，若文档中没有标高则返回null</returns>
        public static Level GetNearestLevel(Document doc, double height)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc), "文档不能为空");

            // 直接使用LINQ查询获取距离最近的标高
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => Math.Abs(level.Elevation - height))
                .FirstOrDefault();
        }
        #endregion
    }
}