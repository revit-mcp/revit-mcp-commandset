using Newtonsoft.Json;
using RevitMCPCommandSet.Models.Geometry;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Features.ElementTransform.Models
{
    /// <summary>
    /// 元素变换操作参数设置
    /// </summary>
    public class TransformOperationSetting
    {
        /// <summary>
        /// 目标元素ID数组
        /// </summary>
        [JsonProperty("elementIds")]
        public List<int> ElementIds { get; set; }

        /// <summary>
        /// 变换操作类型（枚举：Rotate, Mirror, Flip, Move, Copy）
        /// </summary>
        [JsonProperty("transformAction")]
        public string TransformAction { get; set; }

        // ===== Rotate 专用参数 =====

        /// <summary>
        /// 旋转轴线（P1阶段仅支持垂直轴）
        /// </summary>
        [JsonProperty("rotateAxis")]
        public JZLine RotateAxis { get; set; }

        /// <summary>
        /// 旋转角度（度数，正值为逆时针）
        /// </summary>
        [JsonProperty("rotateAngle")]
        public double RotateAngle { get; set; }

        // ===== Mirror 专用参数 =====

        /// <summary>
        /// 镜像平面
        /// </summary>
        [JsonProperty("mirrorPlane")]
        public JZPlane MirrorPlane { get; set; }

        // ===== Flip 专用参数 =====

        /// <summary>
        /// 翻转方向（Hand / Facing）
        /// Hand: 左右翻转
        /// Facing: 前后翻转
        /// </summary>
        [JsonProperty("flipDirection")]
        public string FlipDirection { get; set; }

        // ===== Move 专用参数 =====

        /// <summary>
        /// 移动向量（毫米）
        /// </summary>
        [JsonProperty("moveVector")]
        public JZVector MoveVector { get; set; }

        /// <summary>
        /// Move策略（directTransform / recreate）
        /// P1阶段仅支持 directTransform
        /// </summary>
        [JsonProperty("moveStrategy")]
        public string MoveStrategy { get; set; } = "directTransform";

        /// <summary>
        /// 参数验证
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrEmpty(TransformAction))
            {
                throw new ArgumentException("transformAction 不能为空");
            }

            if (ElementIds == null || ElementIds.Count == 0)
            {
                throw new ArgumentException("elementIds 不能为空");
            }

            // 操作特定验证
            switch (TransformAction)
            {
                case "Rotate":
                    if (RotateAngle == 0)
                    {
                        throw new ArgumentException("Rotate 操作的 rotateAngle 不能为0");
                    }
                    // rotateAxis 现在是可选的，如果未提供将智能获取元素位置
                    break;

                case "Mirror":
                    // mirrorPlane 现在是可选的，如果未提供将智能获取元素位置和默认法向量
                    break;

                case "Flip":
                    if (string.IsNullOrEmpty(FlipDirection))
                    {
                        throw new ArgumentException("Flip 操作需要提供 flipDirection");
                    }
                    if (FlipDirection != "Hand" && FlipDirection != "Facing")
                    {
                        throw new ArgumentException("flipDirection 必须是 Hand 或 Facing");
                    }
                    break;

                case "Move":
                    if (MoveVector == null)
                    {
                        throw new ArgumentException("Move 操作需要提供 moveVector");
                    }
                    // 直接设置默认值，不保留兼容分支
                    MoveStrategy = "directTransform";
                    break;

                case "Copy":
                    if (MoveVector == null)
                    {
                        throw new ArgumentException("Copy 操作需要提供 moveVector");
                    }
                    break;

                default:
                    var validActions = new[] { "Rotate", "Mirror", "Flip", "Move", "Copy" };
                    throw new ArgumentException(
                        $"不支持的操作: {TransformAction}，支持的操作: {string.Join(", ", validActions)}"
                    );
            }
        }
    }
}