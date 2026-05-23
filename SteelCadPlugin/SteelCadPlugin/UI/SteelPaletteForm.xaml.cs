using System;
using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using SteelCadPlugin.Core;
using SteelCadPlugin.Data;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SteelCadPlugin.UI
{
    public partial class SteelPaletteForm : Window
    {
        // ── Properties ────────────────────────────────────────────────────

        private SectionProfile _selectedSection;

        public SectionProfile SelectedSection
        {
            get => _selectedSection;
            set
            {
                _selectedSection = value;
                UpdateDisplay(value);
            }
        }

        public ViewType CurrentViewType
        {
            get
            {
                if (RadioElev?.IsChecked == true) return ViewType.Elevation;
                if (RadioSect?.IsChecked == true) return ViewType.Section;
                return ViewType.Plan;
            }
        }

        public double MmPerUnit
        {
            get
            {
                if (double.TryParse(MmPerUnitBox?.Text, out double v) && v > 0)
                    return v;
                return 1.0;
            }
        }

        // ── Constructor ───────────────────────────────────────────────────

        public SteelPaletteForm()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var db = SectionDatabase.Instance;
            DbStatusText.Text = db.IsLoaded
                ? $"{db.TotalCount} H-sections loaded"
                : "Database not loaded";
        }

        // ── Buttons ───────────────────────────────────────────────────────

        private void BrowseSections_Click(object sender, RoutedEventArgs e)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var dlg = new SectionPickerWindow(SelectedSection?.Name);
            AcApp.ShowModalWindow(AcApp.MainWindow.Handle, dlg, false);

            if (dlg.ChosenSection != null)
                SelectedSection = dlg.ChosenSection;
        }

        private void ViewType_Checked(object sender, RoutedEventArgs e)
        {
            // CurrentViewType reads from radio state — nothing else needed
        }

        private void PlaceBeam_Click(object sender, RoutedEventArgs e)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            doc?.SendStringToExecute("STEELBEAM\n", false, false, false);
        }

        private void EditBeam_Click(object sender, RoutedEventArgs e)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            doc?.SendStringToExecute("STEELEDIT\n", false, false, false);
        }

        private void SwitchView_Click(object sender, RoutedEventArgs e)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            doc?.SendStringToExecute("STEELVIEW\n", false, false, false);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void UpdateDisplay(SectionProfile s)
        {
            if (SectionNameText == null) return; // Not yet initialized

            if (s == null)
            {
                SectionNameText.Text = "—";
                ValD.Text = ValBf.Text = ValTw.Text = ValTf.Text = ValW.Text = ValA.Text = "—";
            }
            else
            {
                SectionNameText.Text = s.Name;
                ValD.Text  = $"{s.d:F1} mm";
                ValBf.Text = $"{s.bf:F1} mm";
                ValTw.Text = $"{s.tw:F1} mm";
                ValTf.Text = $"{s.tf:F1} mm";
                ValW.Text  = $"{s.Weight:F2} kg/m";
                ValA.Text  = $"{s.Area:F0} cm²";
            }
        }
    }
}
