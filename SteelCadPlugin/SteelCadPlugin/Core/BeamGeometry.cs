using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace SteelCadPlugin.Core
{
    /// <summary>
    /// Tạo geometry AutoCAD cho 3 kiểu thể hiện dầm H.
    /// Tất cả tọa độ tính trong hệ tọa độ LOCAL của block:
    ///   - Gốc (0,0,0) = WpStart (sau khi BlockRef đặt tại WpStart + xoay theo góc dầm)
    ///   - Trục X = dọc theo dầm (từ WpStart → WpEnd)
    ///   - Trục Y = vuông góc dầm, hướng lên
    /// </summary>
    public static class BeamGeometry
    {
        // Layer names
        public const string LayerPlanOuter  = "STEEL-H-PLAN";
        public const string LayerPlanCenter = "STEEL-H-CENTER";
        public const string LayerElev       = "STEEL-H-ELEV";
        public const string LayerSection    = "STEEL-H-SECTION";
        public const string LayerText       = "STEEL-H-TEXT";

        /// <summary>Tạo danh sách entity cho block definition (local coords)</summary>
        public static IEnumerable<Entity> CreateEntities(BeamData data)
        {
            switch (data.ViewType)
            {
                case ViewType.Plan:      return CreatePlanView(data);
                case ViewType.Elevation: return CreateElevationView(data);
                case ViewType.Section:   return CreateSectionView(data);
                default:                 return CreatePlanView(data);
            }
        }

        // ════════════════════════════════════════════════════════════════
        // TYPE 1 — PLAN VIEW (nhìn từ trên xuống)
        // ════════════════════════════════════════════════════════════════
        private static IEnumerable<Entity> CreatePlanView(BeamData data)
        {
            double L   = data.Length;
            double hBf = data.bf / 2;
            double c1  = data.Cut1;
            double c2  = data.Cut2;
            double drawEnd = L - c2;

            // ── Outline cánh (outer rectangle) ──────────────────────────
            // Top edge
            yield return MakeLine(c1, hBf, drawEnd, hBf, LayerPlanOuter);
            // Bottom edge
            yield return MakeLine(c1, -hBf, drawEnd, -hBf, LayerPlanOuter);
            // Left end cap
            yield return MakeLine(c1, -hBf, c1, hBf, LayerPlanOuter);
            // Right end cap
            yield return MakeLine(drawEnd, -hBf, drawEnd, hBf, LayerPlanOuter);

            // ── Đường tâm (dashed, full length WP to WP) ────────────────
            var center = MakeLine(0, 0, L, 0, LayerPlanCenter);
            center.LinetypeScale = 1.0;
            // Linetype DASHED sẽ được set trong BeamManager sau khi đảm bảo linetype tồn tại
            yield return center;

            // ── Ký hiệu WP (hình vuông nhỏ tại WP_Start và WP_End) ──────
            // Được render bởi GripOverrule, không cần vẽ thêm

            // ── Text tên tiết diện ───────────────────────────────────────
            // Vị trí text: giữa dầm + TextOffset
            double midX = L / 2 + data.TextOffsetX;
            double midY = hBf + data.TextOffsetY + data.tf;
            yield return MakeText(midX, midY, data.SectionName ?? "", LayerText);
        }

        // ════════════════════════════════════════════════════════════════
        // TYPE 2 — ELEVATION VIEW (mặt đứng, nhìn từ bên)
        // Hiển thị tiết diện chữ H/I đứng
        // Trục X của block = chiều đứng (height), dọc trục dầm
        // ════════════════════════════════════════════════════════════════
        private static IEnumerable<Entity> CreateElevationView(BeamData data)
        {
            double d  = data.d;
            double bf = data.bf;
            double tw = data.tw;
            double tf = data.tf;

            // Vẽ tiết diện H như nhìn từ đầu (trục X = chiều cao, Y = chiều rộng)
            // Gốc tại tâm tiết diện
            double hd  = d / 2;
            double hbf = bf / 2;
            double htw = tw / 2;

            // ── Polyline tiết diện chữ H ─────────────────────────────────
            // Đi theo chu vi ngoài: cánh trên → bụng → cánh dưới
            var pts = new Point2dCollection();

            // Bắt đầu từ góc trái-trên, đi ngược chiều kim đồng hồ
            pts.Add(new Point2d(-hbf,  hd));          // A: góc trên trái
            pts.Add(new Point2d( hbf,  hd));          // B: góc trên phải
            pts.Add(new Point2d( hbf,  hd - tf));     // C: trong cánh trên phải
            pts.Add(new Point2d( htw,  hd - tf));     // D: trên bụng phải
            pts.Add(new Point2d( htw, -hd + tf));     // E: dưới bụng phải
            pts.Add(new Point2d( hbf, -hd + tf));     // F: trong cánh dưới phải
            pts.Add(new Point2d( hbf, -hd));          // G: góc dưới phải
            pts.Add(new Point2d(-hbf, -hd));          // H: góc dưới trái
            pts.Add(new Point2d(-hbf, -hd + tf));     // I: trong cánh dưới trái
            pts.Add(new Point2d(-htw, -hd + tf));     // J: dưới bụng trái
            pts.Add(new Point2d(-htw,  hd - tf));     // K: trên bụng trái
            pts.Add(new Point2d(-hbf,  hd - tf));     // L: trong cánh trên trái
            // Đóng về A

            var pline = MakePolyline(pts, closed: true, layer: LayerElev);
            yield return pline;

            // ── Đường tâm đứng ───────────────────────────────────────────
            var vCenter = MakeLine(0, -hd - d * 0.1, 0, hd + d * 0.1, LayerPlanCenter);
            yield return vCenter;
            // Đường tâm ngang
            var hCenter = MakeLine(-hbf - bf * 0.1, 0, hbf + bf * 0.1, 0, LayerPlanCenter);
            yield return hCenter;

            // ── Text ─────────────────────────────────────────────────────
            double textX = data.TextOffsetX;
            double textY = hd + data.TextOffsetY + 50 / data.MmPerUnit;
            yield return MakeText(textX, textY, data.SectionName ?? "", LayerText);
        }

        // ════════════════════════════════════════════════════════════════
        // TYPE 3 — SECTION VIEW (cắt ngang, nhìn dọc trục dầm)
        // Hiển thị mặt cắt thép tại điểm giữa dầm
        // ════════════════════════════════════════════════════════════════
        private static IEnumerable<Entity> CreateSectionView(BeamData data)
        {
            double d  = data.d;
            double bf = data.bf;
            double tw = data.tw;
            double tf = data.tf;

            double L   = data.Length;
            double midX = L / 2;  // Hiển thị tại giữa dầm

            double hd  = d / 2;
            double hbf = bf / 2;
            double htw = tw / 2;

            // ── Outline tiết diện H (giống elevation nhưng đặt tại giữa dầm) ─
            var pts = new Point2dCollection();
            pts.Add(new Point2d(midX - hbf,  hd));
            pts.Add(new Point2d(midX + hbf,  hd));
            pts.Add(new Point2d(midX + hbf,  hd - tf));
            pts.Add(new Point2d(midX + htw,  hd - tf));
            pts.Add(new Point2d(midX + htw, -hd + tf));
            pts.Add(new Point2d(midX + hbf, -hd + tf));
            pts.Add(new Point2d(midX + hbf, -hd));
            pts.Add(new Point2d(midX - hbf, -hd));
            pts.Add(new Point2d(midX - hbf, -hd + tf));
            pts.Add(new Point2d(midX - htw, -hd + tf));
            pts.Add(new Point2d(midX - htw,  hd - tf));
            pts.Add(new Point2d(midX - hbf,  hd - tf));

            var pline = MakePolyline(pts, closed: true, layer: LayerSection);
            yield return pline;

            // ── Hatch (gạch chéo vật liệu thép) ─────────────────────────
            // Tạo hatch cho cánh trên
            yield return MakeHatch(pts, LayerSection);

            // ── Text ─────────────────────────────────────────────────────
            double textX = midX + data.TextOffsetX;
            double textY = hd + data.TextOffsetY + 50 / data.MmPerUnit;
            yield return MakeText(textX, textY, data.SectionName ?? "", LayerText);
        }

        // ════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════

        private static Line MakeLine(double x1, double y1, double x2, double y2, string layer)
        {
            return new Line(new Point3d(x1, y1, 0), new Point3d(x2, y2, 0))
            {
                Layer = layer,
                ColorIndex = 256  // ByLayer
            };
        }

        private static Polyline MakePolyline(Point2dCollection pts, bool closed, string layer)
        {
            var pl = new Polyline();
            pl.Layer = layer;
            pl.ColorIndex = 256;
            for (int i = 0; i < pts.Count; i++)
                pl.AddVertexAt(i, pts[i], 0, 0, 0);
            pl.Closed = closed;
            return pl;
        }

        private static DBText MakeText(double x, double y, string content, string layer)
        {
            return new DBText
            {
                Position = new Point3d(x, y, 0),
                TextString = content,
                Height = 200,   // sẽ override bởi TEXTSIZE
                HorizontalMode = TextHorizontalMode.TextCenter,
                AlignmentPoint = new Point3d(x, y, 0),
                Layer = layer,
                ColorIndex = 256
            };
        }

        private static Hatch MakeHatch(Point2dCollection boundary, string layer)
        {
            var hatch = new Hatch();
            hatch.Layer = layer;
            hatch.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
            hatch.PatternAngle = Math.PI / 4;
            hatch.PatternScale = 5.0;
            hatch.ColorIndex = 256;
            // Loop sẽ được thêm bởi BeamManager sau khi entity được add vào database
            return hatch;
        }
    }
}
