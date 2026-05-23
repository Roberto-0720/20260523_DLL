using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace SteelCadPlugin.Core
{
    /// <summary>
    /// Tạo, cập nhật và xóa block cấu kiện thép trong database AutoCAD.
    /// Mỗi cấu kiện = 1 BlockReference trỏ đến BlockTableRecord riêng (tên "SCB_XXXX").
    /// Block được insert tại WpStart với rotation = góc dầm.
    /// </summary>
    public static class BeamManager
    {
        private const string BlockPrefix = "SCB_";
        private static int _counter = 1;

        // ── Layer setup ──────────────────────────────────────────────────

        private static readonly Dictionary<string, (short color, string linetype)> LayerDefs
            = new Dictionary<string, (short, string)>
        {
            { BeamGeometry.LayerPlanOuter,  (5,   "Continuous") }, // 5 = Blue
            { BeamGeometry.LayerPlanCenter, (3,   "DASHED") },     // 3 = Green
            { BeamGeometry.LayerElev,       (2,   "Continuous") }, // 2 = Yellow
            { BeamGeometry.LayerSection,    (1,   "Continuous") }, // 1 = Red
            { BeamGeometry.LayerText,       (7,   "Continuous") }, // 7 = White
        };

        public static void EnsureLayers(Database db, Transaction tr)
        {
            var lt  = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            var lyt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            // Đảm bảo linetype DASHED tồn tại
            if (!lt.Has("DASHED"))
            {
                db.LoadLineTypeFile("DASHED", "acad.lin");
            }

            foreach (var kv in LayerDefs)
            {
                if (!lyt.Has(kv.Key))
                {
                    lyt.UpgradeOpen();
                    var layer = new LayerTableRecord
                    {
                        Name       = kv.Key,
                        Color      = Color.FromColorIndex(ColorMethod.ByAci, kv.Value.color),
                        LineWeight = LineWeight.LineWeight030
                    };

                    // Set linetype
                    if (lt.Has(kv.Value.linetype))
                        layer.LinetypeObjectId = lt[kv.Value.linetype];

                    lyt.Add(layer);
                    tr.AddNewlyCreatedDBObject(layer, true);
                }
            }
        }

        // ── Đặt dầm mới ──────────────────────────────────────────────────

        /// <summary>
        /// Tạo block mới và chèn vào ModelSpace.
        /// Trả về ObjectId của BlockReference vừa tạo.
        /// </summary>
        public static ObjectId PlaceBeam(Database db, Transaction tr, BeamData data)
        {
            EnsureLayers(db, tr);

            // Tạo tên block duy nhất
            string blockName = GenerateBlockName(db, tr);

            // Tạo block definition
            ObjectId blockDefId = CreateBlockDefinition(db, tr, blockName, data);

            // Chèn BlockReference vào ModelSpace
            var ms    = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            var angle = data.Angle;

            var bref = new BlockReference(data.WpStart, blockDefId)
            {
                Rotation = angle,
                Layer    = "0"
            };

            ms.AppendEntity(bref);
            tr.AddNewlyCreatedDBObject(bref, true);

            // Thêm AttributeReference cho PROFILE
            AddProfileAttribute(tr, bref, blockDefId, data.SectionName);

            // Ghi XData
            BeamXData.Write(bref, data);

            return bref.ObjectId;
        }

        // ── Cập nhật dầm đã có ───────────────────────────────────────────

        /// <summary>
        /// Xóa geometry cũ trong block, vẽ lại theo BeamData mới.
        /// </summary>
        public static void RegenerateBeam(Database db, Transaction tr,
                                          ObjectId brefId, BeamData data)
        {
            EnsureLayers(db, tr);

            var bref = tr.GetObject(brefId, OpenMode.ForWrite) as BlockReference;
            if (bref == null) return;

            // Cập nhật vị trí và góc của BlockReference
            bref.Position = data.WpStart;
            bref.Rotation = data.Angle;

            // Xóa geometry cũ trong block definition
            var btr = tr.GetObject(bref.BlockTableRecord, OpenMode.ForWrite) as BlockTableRecord;
            if (btr == null) return;

            var toDelete = new List<ObjectId>();
            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent is AttributeDefinition) continue; // Giữ lại attribute def
                toDelete.Add(id);
            }
            foreach (var id in toDelete)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                ent?.Erase();
            }

            // Vẽ geometry mới
            AppendGeometry(db, tr, btr, data);

            // Cập nhật attribute PROFILE
            UpdateProfileAttribute(tr, bref, data.SectionName);

            // Ghi lại XData
            BeamXData.Write(bref, data);
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static string GenerateBlockName(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            string name;
            do
            {
                name = BlockPrefix + _counter.ToString("D4");
                _counter++;
            } while (bt.Has(name));
            return name;
        }

        private static ObjectId CreateBlockDefinition(Database db, Transaction tr,
                                                       string blockName, BeamData data)
        {
            var bt  = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            var btr = new BlockTableRecord { Name = blockName, Origin = Point3d.Origin };

            bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);

            // Thêm AttributeDefinition cho PROFILE (ẩn, chỉ để Properties hiển thị)
            var attrDef = new AttributeDefinition
            {
                Tag       = "PROFILE",
                Prompt    = "Section Profile",
                TextString = data.SectionName ?? "",
                Position  = new Point3d(0, 0, 0),
                Height    = 200,
                Invisible = false,
                Layer     = BeamGeometry.LayerText
            };
            btr.AppendEntity(attrDef);
            tr.AddNewlyCreatedDBObject(attrDef, true);

            // Thêm geometry
            AppendGeometry(db, tr, btr, data);

            return btr.ObjectId;
        }

        private static void AppendGeometry(Database db, Transaction tr,
                                           BlockTableRecord btr, BeamData data)
        {
            Hatch pendingHatch = null;
            ObjectId sectionPlineId = ObjectId.Null;

            foreach (var ent in BeamGeometry.CreateEntities(data))
            {
                if (ent.Layer == BeamGeometry.LayerPlanCenter)
                    ent.Linetype = "DASHED";

                // Hatch requires boundary in DB first — defer loop assignment
                if (ent is Hatch h)
                {
                    pendingHatch = h;
                    btr.AppendEntity(pendingHatch);
                    tr.AddNewlyCreatedDBObject(pendingHatch, true);
                    continue;
                }

                btr.AppendEntity(ent);
                tr.AddNewlyCreatedDBObject(ent, true);

                // Track the section polyline for hatch loop
                if (data.ViewType == ViewType.Section
                    && ent is Polyline
                    && ent.Layer == BeamGeometry.LayerSection)
                    sectionPlineId = ent.ObjectId;
            }

            // Add boundary loop now that both hatch and polyline are in DB
            if (pendingHatch != null && !sectionPlineId.IsNull)
            {
                var pline = tr.GetObject(sectionPlineId, OpenMode.ForRead) as Polyline;
                if (pline != null)
                {
                    var pts    = new Point2dCollection();
                    var bulges = new DoubleCollection();
                    for (int i = 0; i < pline.NumberOfVertices; i++)
                    {
                        pts.Add(pline.GetPoint2dAt(i));
                        bulges.Add(0.0);
                    }
                    pendingHatch.Associative = false;
                    pendingHatch.AppendLoop(HatchLoopTypes.External, pts, bulges);
                    pendingHatch.EvaluateHatch(true);
                }
            }
        }

        private static void AddProfileAttribute(Transaction tr,
                                                 BlockReference bref,
                                                 ObjectId blockDefId,
                                                 string sectionName)
        {
            var btr = tr.GetObject(blockDefId, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return;

            foreach (ObjectId id in btr)
            {
                var attrDef = tr.GetObject(id, OpenMode.ForRead) as AttributeDefinition;
                if (attrDef == null || attrDef.Tag != "PROFILE") continue;

                var attrRef = new AttributeReference();
                attrRef.SetAttributeFromBlock(attrDef, bref.BlockTransform);
                attrRef.TextString = sectionName ?? "";

                bref.AttributeCollection.AppendAttribute(attrRef);
                tr.AddNewlyCreatedDBObject(attrRef, true);
                break;
            }
        }

        private static void UpdateProfileAttribute(Transaction tr,
                                                    BlockReference bref,
                                                    string sectionName)
        {
            foreach (ObjectId attId in bref.AttributeCollection)
            {
                var att = tr.GetObject(attId, OpenMode.ForWrite) as AttributeReference;
                if (att != null && att.Tag == "PROFILE")
                {
                    att.TextString = sectionName ?? "";
                    break;
                }
            }
        }
    }
}
