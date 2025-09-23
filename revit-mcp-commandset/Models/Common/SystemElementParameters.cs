using RevitMCPCommandSet.Models.Geometry;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// 系统族创建参数
    /// </summary>
    public class SystemElementParameters
    {
        /// <summary>
        /// 系统族类型
        /// </summary>
        public SystemElementType ElementType { get; set; }

        /// <summary>
        /// 系统族类型ID (WallType或FloorType的ElementId)
        /// </summary>
        public int TypeId { get; set; }

        /// <summary>
        /// 关联标高ID
        /// </summary>
        public int LevelId { get; set; }

        #region 墙体参数

        /// <summary>
        /// 墙体路径（起点和终点，单位：毫米）
        /// </summary>
        public JZLine WallLine { get; set; }

        /// <summary>
        /// 墙体高度（毫米）
        /// </summary>
        public double Height { get; set; } = 3000; // 默认3米

        /// <summary>
        /// 底部偏移（毫米）
        /// </summary>
        public double BaseOffset { get; set; } = 0;

        /// <summary>
        /// 是否结构墙
        /// </summary>
        public bool IsStructural { get; set; } = false;

        #endregion

        #region 楼板参数

        /// <summary>
        /// 楼板边界点列表（按顺序连接形成闭合轮廓，单位：毫米）
        /// </summary>
        public List<JZPoint> FloorBoundary { get; set; }

        /// <summary>
        /// 楼板顶部偏移（毫米）
        /// </summary>
        public double TopOffset { get; set; } = 0;

        /// <summary>
        /// 楼板坡度（可选，默认为null表示水平楼板）
        /// </summary>
        public double? Slope { get; set; }

        #endregion

        #region 智能选项

        /// <summary>
        /// 自动查找最近标高
        /// </summary>
        public bool AutoFindLevel { get; set; } = true;

        /// <summary>
        /// 自动连接相邻墙体
        /// </summary>
        public bool AutoJoinWalls { get; set; } = true;

        #endregion
    }

    /// <summary>
    /// 系统族类型枚举
    /// </summary>
    public enum SystemElementType
    {
        /// <summary>
        /// 墙体
        /// </summary>
        Wall,

        /// <summary>
        /// 楼板
        /// </summary>
        Floor,

        /// <summary>
        /// 天花板（预留）
        /// </summary>
        Ceiling,

        /// <summary>
        /// 屋顶（预留）
        /// </summary>
        Roof
    }
}