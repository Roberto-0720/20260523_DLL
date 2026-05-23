using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using SteelCadPlugin.Core;
using SteelCadPlugin.Data;
using SteelCadPlugin.UI;

namespace SteelCadPlugin
{
    public class Commands
    {
        // ════════════════════════════════════════════════════════════════
        // STEELBEAM — Đặt dầm H mới
        // ════════════════════════════════════════════════════════════════
        [CommandMethod("STEELBEAM", CommandFlags.Modal)]
        public void PlaceBeam()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor   ed  = doc.Editor;

            // Lấy tiết diện từ palette (hoặc mở picker ngay)
            SectionProfile section = App.Palette.SelectedSection;
            if (section == null)
            {
                section = SectionPickerWindow.PickSection(doc);
                if (section == null) return;
                App.Palette.SelectedSection = section;
            }

            ed.WriteMessage($"\nSection: {section.Name} — d={section.d}mm, bf={section.bf}mm");

            // ── Nhập WP_Start ─────────────────────────────────────────
            var opt1 = new PromptPointOptions("\nWorking Point 1 (WP Start): ");
            var res1 = ed.GetPoint(opt1);
            if (res1.Status != PromptStatus.OK) return;
            Point3d wp1 = res1.Value;

            // ── Nhập WP_End với rubber-band line ─────────────────────
            var opt2 = new PromptPointOptions("\nWorking Point 2 (WP End): ")
            {
                UseBasePoint = true,
                BasePoint    = wp1
            };
            var res2 = ed.GetPoint(opt2);
            if (res2.Status != PromptStatus.OK) return;
            Point3d wp2 = res2.Value;

            if (wp1.DistanceTo(wp2) < 1e-6)
            {
                ed.WriteMessage("\nError: WP1 and WP2 cannot be the same point.\n");
                return;
            }

            // ── Xác định MmPerUnit từ INSUNITS ───────────────────────
            double mmPerUnit = GetMmPerUnit(doc.Database);

            // ── Tạo BeamData ──────────────────────────────────────────
            var data = new BeamData
            {
                SectionName  = section.Name,
                d_mm         = section.d,
                bf_mm        = section.bf,
                tw_mm        = section.tw,
                tf_mm        = section.tf,
                R_mm         = section.R,
                WpStart      = wp1,
                WpEnd        = wp2,
                Cut1         = 0,
                Cut2         = 0,
                ViewType     = App.Palette.CurrentViewType,
                TextOffsetX  = 0,
                TextOffsetY  = (section.bf / 2 + 100) / mmPerUnit,
                MmPerUnit    = mmPerUnit
            };

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                BeamManager.PlaceBeam(doc.Database, tr, data);
                tr.Commit();
            }

            ed.Regen();
            ed.WriteMessage($"\n✔ Placed {section.Name} beam, L = {wp1.DistanceTo(wp2):F0} units\n");
        }

        // ════════════════════════════════════════════════════════════════
        // STEELVIEW — Chuyển đổi kiểu thể hiện (Plan/Elevation/Section)
        // ════════════════════════════════════════════════════════════════
        [CommandMethod("STEELVIEW", CommandFlags.Modal)]
        public void SwitchView()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor   ed  = doc.Editor;

            var kwOpts = new PromptKeywordOptions(
                "\nView type [Plan/Elevation/Section] <Plan>: ", "Plan Elevation Section");
            kwOpts.AllowNone = true;
            var kwRes = ed.GetKeywords(kwOpts);

            ViewType newType;
            if (kwRes.Status == PromptStatus.None)
                newType = ViewType.Plan;
            else if (kwRes.Status != PromptStatus.OK)
                return;
            else
            {
                switch (kwRes.StringResult)
                {
                    case "Elevation": newType = ViewType.Elevation; break;
                    case "Section":   newType = ViewType.Section;   break;
                    default:          newType = ViewType.Plan;       break;
                }
            }

            // Chọn đối tượng
            var selRes = ed.GetSelection(new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "INSERT")
            }));
            if (selRes.Status != PromptStatus.OK) return;

            int changed = 0;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject so in selRes.Value)
                {
                    BeamData data = BeamXData.Read(tr, so.ObjectId);
                    if (data == null) continue;
                    data.ViewType = newType;
                    BeamManager.RegenerateBeam(doc.Database, tr, so.ObjectId, data);
                    changed++;
                }
                tr.Commit();
            }

            ed.Regen();
            ed.WriteMessage($"\n✔ Changed {changed} element(s) to {newType} view.\n");
        }

        // ════════════════════════════════════════════════════════════════
        // STEELEDIT — Sửa tiết diện của dầm đang chọn
        // ════════════════════════════════════════════════════════════════
        [CommandMethod("STEELEDIT", CommandFlags.Modal)]
        public void EditBeam()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor   ed  = doc.Editor;

            var selOpt = new PromptEntityOptions("\nSelect steel beam to edit: ");
            selOpt.SetRejectMessage("\nNot a steel beam.");
            selOpt.AddAllowedClass(typeof(BlockReference), true);

            var selRes = ed.GetEntity(selOpt);
            if (selRes.Status != PromptStatus.OK) return;

            BeamData data;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                data = BeamXData.Read(tr, selRes.ObjectId);
                tr.Commit();
            }

            if (data == null)
            {
                ed.WriteMessage("\nSelected entity is not a SteelCAD beam.\n");
                return;
            }

            // Mở Section Picker
            SectionProfile newSection = SectionPickerWindow.PickSection(doc, data.SectionName);
            if (newSection == null) return;

            // Cập nhật BeamData
            data.SectionName = newSection.Name;
            data.d_mm        = newSection.d;
            data.bf_mm       = newSection.bf;
            data.tw_mm       = newSection.tw;
            data.tf_mm       = newSection.tf;
            data.R_mm        = newSection.R;

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                BeamManager.RegenerateBeam(doc.Database, tr, selRes.ObjectId, data);
                tr.Commit();
            }

            ed.Regen();
            ed.WriteMessage($"\n✔ Updated to {newSection.Name}\n");
        }

        // ════════════════════════════════════════════════════════════════
        // STEELPALETTE — Hiện/ẩn palette
        // ════════════════════════════════════════════════════════════════
        [CommandMethod("STEELPALETTE", CommandFlags.Modal)]
        public void ShowPalette()
        {
            var palette = App.Palette;
            if (palette.IsVisible)
                palette.Hide();
            else
                palette.Show();
        }

        // ── Helper: đọc INSUNITS → mm/unit ───────────────────────────────
        private static double GetMmPerUnit(Database db)
        {
            try
            {
                short insunits = (short)db.Insunits;
                switch (insunits)
                {
                    case 1:  return 25.4;       // inches
                    case 2:  return 304.8;      // feet
                    case 4:  return 1.0;        // mm
                    case 5:  return 10.0;       // cm
                    case 6:  return 1000.0;     // m
                    default: return 1.0;        // unitless → assume mm
                }
            }
            catch { return 1.0; }
        }
    }
}
