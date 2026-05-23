using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using SteelCadPlugin.Data;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SteelCadPlugin.UI
{
    public partial class SectionPickerWindow : Window
    {
        private ObservableCollection<SectionProfile> _items
            = new ObservableCollection<SectionProfile>();

        private SectionType? _filterType = null;

        // ── Kết quả chọn ─────────────────────────────────────────────────
        public SectionProfile ChosenSection { get; private set; }

        public SectionPickerWindow(string preselect = null)
        {
            InitializeComponent();

            // Khởi tạo type filter
            var types = new List<object> { "(All types)" };
            types.AddRange(SectionDatabase.Instance.AvailableTypes.Cast<object>());
            TypeComboBox.ItemsSource  = types;
            TypeComboBox.SelectedIndex = 0;

            // Load grid
            RefreshGrid();

            // Tổng số
            TotalCountText.Text = $"{SectionDatabase.Instance.TotalCount} sections in database";

            // Pre-select nếu có
            if (!string.IsNullOrEmpty(preselect))
            {
                var found = _items.FirstOrDefault(s =>
                    s.Name.Equals(preselect, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                    SectionGrid.SelectedItem = found;
            }
        }

        // ── Static factory ────────────────────────────────────────────────

        /// <summary>Mở dialog và trả về section được chọn (null nếu cancel)</summary>
        public static SectionProfile PickSection(Document acDoc, string preselect = null)
        {
            var dlg = new SectionPickerWindow(preselect);
            AcApp.ShowModalWindow(AcApp.MainWindow.Handle, dlg, false);
            return dlg.ChosenSection;
        }

        // ── Event handlers ────────────────────────────────────────────────

        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TypeComboBox.SelectedItem is SectionType st)
                _filterType = st;
            else
                _filterType = null;
            RefreshGrid();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshGrid();
        }

        private void SectionGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var sel = SectionGrid.SelectedItem as SectionProfile;
            if (sel != null)
            {
                InfoPanel.Visibility  = Visibility.Visible;
                SelectedInfoText.Text =
                    $"{sel.Name}   d={sel.d:F1}  bf={sel.bf:F1}  tw={sel.tw:F1}  tf={sel.tf:F1} mm";
                OkButton.IsEnabled = true;
            }
            else
            {
                InfoPanel.Visibility = Visibility.Collapsed;
                OkButton.IsEnabled   = false;
            }
        }

        private void SectionGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SectionGrid.SelectedItem != null) Confirm();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)    => Confirm();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => Cancel();

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Cancel();
            base.OnKeyDown(e);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void RefreshGrid()
        {
            string keyword = SearchBox?.Text ?? "";
            var results    = SectionDatabase.Instance.Search(keyword, _filterType);

            _items.Clear();
            foreach (var s in results) _items.Add(s);
            SectionGrid.ItemsSource = _items;

            // Reset selection info
            OkButton.IsEnabled   = false;
            InfoPanel.Visibility = Visibility.Collapsed;
        }

        private void Confirm()
        {
            ChosenSection = SectionGrid.SelectedItem as SectionProfile;
            if (ChosenSection != null) { DialogResult = true; Close(); }
        }

        private void Cancel() { DialogResult = false; Close(); }
    }
}
