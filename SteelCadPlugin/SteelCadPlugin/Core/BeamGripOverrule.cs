using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;

namespace SteelCadPlugin.Core
{
    // ── Định nghĩa loại grip ─────────────────────────────────────────────

    public enum BeamGripType
    {
        WpStart     = 0,    // ■ Working Point đầu
        WpEnd       = 1,    // ■ Working Point cuối
        CutStart    = 2,    // ▶ Đoạn cắt đầu
        CutEnd      = 3,    // ◀ Đoạn cắt cuối
        TextPos     = 4,    // ■ Vị trí text (move)
        MoveAll     = 5,    // ⊕ Di chuyển toàn bộ
        SectionPick = 6     // ▽ Chọn tiết diện (trigger dialog)
    }

    /// <summary>Custom grip data cho dầm thép</summary>
    public class BeamGripData : GripData
    {
        public BeamGripType GripType { get; }
        public ObjectId     BeamId  { get; }

        public BeamGripData(BeamGripType type, ObjectId beamId)
        {
            GripType      = type;
            BeamId        = beamId;
            ForcedPickOn  = false;
            HotGripInvokesRightClick = (type == BeamGripType.SectionPick);
        }
    }

    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// GripOverrule cho BlockReference có XData của SteelCAD.
    /// Thay thế toàn bộ grips mặc định bằng 7 custom grips.
    /// </summary>
    public class BeamGripOverrule : GripOverrule
    {
        public BeamGripOverrule()
        {
            // Chỉ áp dụng cho BlockReference có XData STEELCAD
            SetCustomFilter();
        }

        // ── Filter: chỉ áp dụng cho SteelBeam ───────────────────────────

        public override bool IsApplicable(RXObject overruledSubject)
        {
            if (overruledSubject is BlockReference bref)
                return BeamXData.IsSteelBeam(bref);
            return false;
        }

        // ── Trả về danh sách grips ───────────────────────────────────────

        public override void GetGripPoints(Entity entity,
                                            GripDataCollection grips,
                                            double curViewUnitSize,
                                            int gripSize,
                                            Vector3d curViewDir,
                                            GetGripPointsFlags bitFlags)
        {
            var bref = entity as BlockReference;
            if (bref == null) return;

            BeamData data = BeamXData.ReadFromEntity(bref);
            if (data == null) return;

            // Tính các điểm WCS từ WpStart, WpEnd, góc dầm
            double angle = data.Angle;
            var unitDir  = new Vector3d(Math.Cos(angle), Math.Sin(angle), 0);
            var perp     = new Vector3d(-Math.Sin(angle), Math.Cos(angle), 0);

            Point3d wpStart = data.WpStart;
            Point3d wpEnd   = data.WpEnd;

            // ■ WP_Start
            var g0 = new BeamGripData(BeamGripType.WpStart, bref.ObjectId);
            g0.GripPoint = wpStart;
            grips.Add(g0);

            // ■ WP_End
            var g1 = new BeamGripData(BeamGripType.WpEnd, bref.ObjectId);
            g1.GripPoint = wpEnd;
            grips.Add(g1);

            // ▶ Cut_Start (từ WpStart dọc theo dầm một đoạn Cut1)
            var g2 = new BeamGripData(BeamGripType.CutStart, bref.ObjectId);
            g2.GripPoint = wpStart + unitDir * data.Cut1;
            grips.Add(g2);

            // ◀ Cut_End (từ WpEnd ngược lại một đoạn Cut2)
            var g3 = new BeamGripData(BeamGripType.CutEnd, bref.ObjectId);
            g3.GripPoint = wpEnd - unitDir * data.Cut2;
            grips.Add(g3);

            // ■ Text position (midpoint + offset)
            Point3d mid = data.Midpoint;
            var g4 = new BeamGripData(BeamGripType.TextPos, bref.ObjectId);
            g4.GripPoint = mid + unitDir * data.TextOffsetX + perp * data.TextOffsetY;
            grips.Add(g4);

            // ⊕ Move All (midpoint)
            var g5 = new BeamGripData(BeamGripType.MoveAll, bref.ObjectId);
            g5.GripPoint = mid;
            grips.Add(g5);

            // ▽ Section Picker (dưới tiết diện)
            var g6 = new BeamGripData(BeamGripType.SectionPick, bref.ObjectId);
            g6.GripPoint = mid - perp * (data.bf / 2 + 80 / data.MmPerUnit);
            grips.Add(g6);
        }

        // ── Xử lý khi grip bị kéo ────────────────────────────────────────

        public override void MoveGripPointsAt(Entity entity,
                                               GripDataCollection grips,
                                               Vector3d offset,
                                               MoveGripPointsFlags bitFlags)
        {
            var bref = entity as BlockReference;
            if (bref == null) { base.MoveGripPointsAt(entity, grips, offset, bitFlags); return; }

            BeamData data = BeamXData.ReadFromEntity(bref);
            if (data == null) { base.MoveGripPointsAt(entity, grips, offset, bitFlags); return; }

            foreach (GripData grip in grips)
            {
                if (!(grip is BeamGripData bg)) continue;

                switch (bg.GripType)
                {
                    case BeamGripType.WpStart:
                        data.WpStart = data.WpStart + offset;
                        break;

                    case BeamGripType.WpEnd:
                        data.WpEnd = data.WpEnd + offset;
                        break;

                    case BeamGripType.CutStart:
                        // Chiều dài cut tăng/giảm theo component dọc trục dầm
                        double delta1 = ProjectOntoBeam(offset, data);
                        data.Cut1 = Math.Max(0, data.Cut1 + delta1);
                        break;

                    case BeamGripType.CutEnd:
                        double delta2 = -ProjectOntoBeam(offset, data);
                        data.Cut2 = Math.Max(0, data.Cut2 + delta2);
                        break;

                    case BeamGripType.TextPos:
                        // Cập nhật TextOffset trong local coords
                        double angle = data.Angle;
                        var unitDir  = new Vector3d(Math.Cos(angle), Math.Sin(angle), 0);
                        var perp     = new Vector3d(-Math.Sin(angle), Math.Cos(angle), 0);
                        data.TextOffsetX += offset.DotProduct(unitDir);
                        data.TextOffsetY += offset.DotProduct(perp);
                        break;

                    case BeamGripType.MoveAll:
                        data.WpStart = data.WpStart + offset;
                        data.WpEnd   = data.WpEnd   + offset;
                        break;

                    case BeamGripType.SectionPick:
                        // Mở Section Picker dialog — handled by hotspot click, not drag
                        break;
                }
            }

            // Regenerate geometry trong database
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    BeamManager.RegenerateBeam(doc.Database, tr, bref.ObjectId, data);
                    tr.Commit();
                }
            }
        }

        // ── GripStatus: xử lý khi click vào Section Picker grip ──────────

        public override void OnGripStatusChanged(Entity entity,
                                                   GripStatus status)
        {
            // Chú ý: hotgrip không dễ phân biệt grip nào đang được click
            // Sẽ xử lý qua command STEELEDIT khi người dùng double-click
        }

        // ── Helper ────────────────────────────────────────────────────────

        private static double ProjectOntoBeam(Vector3d offset, BeamData data)
        {
            double angle = data.Angle;
            var unitDir = new Vector3d(Math.Cos(angle), Math.Sin(angle), 0);
            return offset.DotProduct(unitDir);
        }
    }
}
