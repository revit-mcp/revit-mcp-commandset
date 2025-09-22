namespace RevitMCPCommandSet.Utils.FamilyCreation
{
    /// <summary>
    /// 族创建功能的默认值常量
    /// </summary>
    public static class FamilyCreationDefaults
    {
        /// <summary>
        /// 是否自动查找宿主元素
        /// </summary>
        public const bool AutoFindHost = true;

        /// <summary>
        /// 是否自动查找标高
        /// </summary>
        public const bool AutoFindLevel = true;

        /// <summary>
        /// 搜索半径（毫米）
        /// </summary>
        public const double SearchRadius = 1000;

        /// <summary>
        /// 底部偏移（毫米）
        /// </summary>
        public const double BaseOffset = 0;

        /// <summary>
        /// 顶部偏移（毫米）
        /// </summary>
        public const double TopOffset = 0;

        /// <summary>
        /// 默认宿主类别（墙）
        /// </summary>
        public static readonly string[] DefaultHostCategories = { "OST_Walls" };

        /// <summary>
        /// 架构版本
        /// </summary>
        public const int SchemaVersion = 1;
    }
}