using System;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using SteelCadPlugin.Core;
using SteelCadPlugin.Data;
using SteelCadPlugin.UI;

[assembly: ExtensionApplication(typeof(SteelCadPlugin.App))]
[assembly: CommandClass(typeof(SteelCadPlugin.Commands))]

namespace SteelCadPlugin
{
    public class App : IExtensionApplication
    {
        private static BeamGripOverrule _gripOverrule;
        private static SteelPaletteForm _palette;
        private static bool _paletteClosed = true;

        public void Initialize()
        {
            try
            {
                // Đường dẫn file Excel cùng thư mục với DLL
                string dllDir   = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string xlsPath  = Path.Combine(dllDir, "Section Data.xlsx");

                // Load database tiết diện
                SectionDatabase.Instance.Load(xlsPath);

                // Đăng ký XData application name (cho tất cả documents)
                Application.DocumentManager.DocumentCreated += (s, e) =>
                    BeamXData.RegisterApp(e.Document.Database);

                // Đăng ký cho document hiện tại
                var curDoc = Application.DocumentManager.MdiActiveDocument;
                if (curDoc != null)
                    BeamXData.RegisterApp(curDoc.Database);

                // Đăng ký Grip Overrule
                _gripOverrule = new BeamGripOverrule();
                Overrule.AddOverrule(
                    RXObject.GetClass(typeof(BlockReference)),
                    _gripOverrule,
                    false);
                Overrule.Overruling = true;

                curDoc?.Editor.WriteMessage(
                    $"\n✔ SteelCAD Plugin loaded — {SectionDatabase.Instance.TotalCount} H-sections available.\n" +
                    "  Commands: STEELBEAM | STEELVIEW | STEELPALETTE | STEELEDIT\n");
            }
            catch (System.Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument?.Editor
                    .WriteMessage($"\n✘ SteelCAD init error: {ex.Message}\n");
            }
        }

        public void Terminate()
        {
            if (_gripOverrule != null)
            {
                Overrule.RemoveOverrule(
                    RXObject.GetClass(typeof(BlockReference)),
                    _gripOverrule);
            }
        }

        public static SteelPaletteForm Palette
        {
            get
            {
                if (_palette == null || _paletteClosed)
                {
                    _palette = new SteelPaletteForm();
                    _paletteClosed = false;
                    _palette.Closed += (s, e) => _paletteClosed = true;
                }
                return _palette;
            }
        }
    }
}
