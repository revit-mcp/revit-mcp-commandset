using RevitMCPCommandSet.Models.Geometry;

namespace RevitMCPCommandSet.Utils
{
    public static class ProjectUtils
    {
        /// <summary>
        /// 获取元素指定法向量的平面轮廓
        /// </summary>
        /// <param name="element"></param>
        /// <param name="normal"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static List<JZFace> GetElementProfile(this Element element, XYZ normal = null)
        {
            var profiles = new List<JZFace>();
            var solid = element.GetElementSolid();
            if (solid == null)
            {
                return null;
            }

            var planarFaces = solid.FindFace(normal ?? new XYZ(0, 0, -1));
            if (planarFaces == null)
            {
                return null;
            }

            // 提取底面的所有JZFace（外环+洞）
            foreach (var planarFace in planarFaces)
            {
                var faces = planarFace.ToJZFace();
                if (faces != null)
                {
                    profiles.AddRange(faces);
                }
            }

            return profiles;
        }

        /// <summary>
        /// 获取指定Revit元素的第一个主Solid几何体
        /// </summary>
        /// <param name="element">要提取Solid的Revit元素</param>
        /// <returns>第一个体积大于0的Solid实例，若无则返回null</returns>
        public static Solid GetElementSolid(this Element element)
        {
            #region Step1 获取元素的几何表示

            // 使用Options配置，ComputeReferences用于需要几何引用，IncludeNonVisibleObjects为否仅取可见体
            GeometryElement geomElem = element.get_Geometry(new Options()
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false
            });
            if (geomElem == null) return null;

            #endregion

            #region Step2 遍历所有一级GeometryObject

            foreach (GeometryObject geomObj in geomElem)
            {
                // 1. 如果对象本身是Solid且体积大于0，直接返回
                if (geomObj is Solid solid && solid.Volume > 0)
                {
                    return solid;
                }
                // 2. 如果遇到几何实例（GeometryInstance），进一步获取其实例内部的几何体并遍历
                else if (geomObj is GeometryInstance instance)
                {
                    GeometryElement instanceGeom = instance.GetInstanceGeometry();
                    foreach (GeometryObject instanceObj in instanceGeom)
                    {
                        if (instanceObj is Solid instanceSolid && instanceSolid.Volume > 0)
                        {
                            return instanceSolid;
                        }
                    }
                }
            }

            #endregion

            // 如果遍历后均未找到符合要求的Solid，则返回null
            return null;
        }

        /// <summary>
        /// 获得指定表面
        /// </summary>
        /// <param name="solid"></param>
        /// <param name="normal"></param>
        /// <returns></returns>
        public static List<PlanarFace> FindFace(this Solid solid, XYZ normal)
        {
            var planarFaces = new List<PlanarFace>();
            Solid pSolid = solid;
            FaceArray faces = pSolid.Faces;
            foreach (Face pFace in faces)
            {
                if (pFace is PlanarFace)
                {
                    //Face 强制转换为 PlanarFace
                    PlanarFace pPlanarFace = pFace as PlanarFace;

                    //ComputeNormal是Face的方法，返回的是在某个指定点的法向量
                    //判断pPlanarFace的外法向量和所选面的法向量normal是否总是相同
                    if (pPlanarFace.ComputeNormal(new UV(0.5, 0.5)).IsAlmostEqualTo(normal))
                    {
                        planarFaces.Add(pPlanarFace);
                    }
                }
            }

            return planarFaces;
        }

        /// <summary>
        /// 将PlanarFace的所有边界CurveLoop转换为JZFace列表。
        /// 每个CurveLoop生成一个独立的JZFace。<br/>
        /// 【假设CurveLoop全部由Line组成，否则返回null】<br/>
        /// 单位转换：ft -> mm (×304.8)
        /// </summary>
        /// <param name="planarFace">Revit的PlanarFace</param>
        /// <returns>JZFace对象列表</returns>
        public static List<JZFace> ToJZFace(this PlanarFace planarFace)
        {
            if (planarFace == null)
                return null;

            var faces = new List<JZFace>();

            // 获取所有CurveLoop
            IList<CurveLoop> loops = planarFace.GetEdgesAsCurveLoops();
            if (loops == null || loops.Count == 0)
                return null;

            // 每个CurveLoop生成一个独立的JZFace
            foreach (var curveLoop in loops)
            {
                var face = new JZFace();
                var loop = new List<JZLine>();

                foreach (Curve curve in curveLoop)
                {
                    if (curve is Line line)
                    {
                        // 单位转换：ft -> mm (×304.8)
                        loop.Add(new JZLine(
                            new JZPoint(line.GetEndPoint(0).X * 304.8, line.GetEndPoint(0).Y * 304.8, line.GetEndPoint(0).Z * 304.8),
                            new JZPoint(line.GetEndPoint(1).X * 304.8, line.GetEndPoint(1).Y * 304.8, line.GetEndPoint(1).Z * 304.8)
                        ));
                    }
                    else
                        return null;  // 如果不是Line，返回null
                }

                face.OuterLoop = loop;
                faces.Add(face);
            }

            return faces;
        }
    }
}