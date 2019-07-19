using Microsoft.Win32;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FlverMaterialStudio
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string FlverPath = "";
        FLVER Flver = null;
        int CurrentMaterialIndex = -1;
        FLVER.Material CurrentMaterial = null;
        List<FLVER.Texture> CurrentMapList = new List<FLVER.Texture>();
        List<GXItemContainer> CurrentGXItemList = new List<GXItemContainer>();
        bool selectionChangeJustFailed = false;
        public MainWindow()
        {
            InitializeComponent();

            SetIsEverythingDisabled(true);
        }

        void LoadFLVER(string path, FLVER f)
        {
            ClearCurrentShit();
            FlverPath = path;
            Flver = f;
            CurrentMaterialIndex = -1;
            CurrentGXItemList.Clear();
            CurrentMaterial = null;
            CurrentMapList = null;
            ListViewFlverMaterials.Items.Clear();
            int i = 0;
            foreach (var m in f.Materials)
            {
                ListViewFlverMaterials.Items.Add(new Label() { Content = $"[{i++}] {m.Name}" });
            }
            TabGXItems.IsEnabled = (Flver.Header.Version >= 0x20010);
            SetIsEverythingDisabled(true);
        }

        void SwitchTabs(bool isTextureMapsTab)
        {
            if (isTextureMapsTab)
                DataGridTextureMaps.ItemsSource = CurrentMapList;
            else
                DataGridTextureMaps.ItemsSource = CurrentGXItemList;
        }

        bool SaveCurrentShitToMesh()
        {
            if (CurrentMaterial == null)
                return true;
            CurrentMaterial.Name = TextBoxMaterialName.Text;
            CurrentMaterial.MTD = TextBoxMaterialDefinition.Text;

            bool didShitFail = false;

            if (Flver.Header.Version >= 0x20010)
            {
                try
                {
                    Flver.GXLists[CurrentMaterial.GXIndex].Clear();
                    foreach (var gx in CurrentGXItemList)
                    {
                        Flver.GXLists[CurrentMaterial.GXIndex]
                            .Add(new FLVER.GXItem(GetScuffedGXID(gx.ID), gx.Unk, GetGXBytesPacked(gx.Bytes)));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Current GX data is invalid and cannot be stored. Cancelling operaton.\n\n(Exception encountered:)\n\n{ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    didShitFail = true;
                }
            }

            return !didShitFail;
        }
        
        byte[] GetGXBytesPacked(string unpacked)
        {
            return unpacked.Split(' ')
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Select(b => byte.Parse(b, System.Globalization.NumberStyles.HexNumber))
                .ToArray();
        }

        uint GetScuffedGXID(string input)
        {
            if (input.Length > 4)
                input = input.Substring(0, 4);
            if (Flver.Header.Version >= 0x20013)
            {
                return string.IsNullOrEmpty(input) ?
                    0x7FFFFFFF : BitConverter.ToUInt32(Encoding.ASCII.GetBytes(input), 0);
            }
            else
            {
                return string.IsNullOrEmpty(input) ?
                    0x7FFFFFFF : uint.Parse(input);
            }
        }

        string GetUnscuffedGXID(uint input)
        {
            if (input == 0x7FFFFFFF)
            {
                return "";
            }
            else
            {
                if (Flver.Header.Version >= 0x20013)
                {

                    return Encoding.ASCII.GetString(BitConverter.GetBytes(input));
                }
                else
                {
                    return input.ToString();
                }
            }
        }

        void LoadCurrentShitFromMesh()
        {
            TextBoxMaterialName.Text = CurrentMaterial.Name;
            TextBoxMaterialDefinition.Text = CurrentMaterial.MTD;
            CurrentMapList = CurrentMaterial.Textures;

            CurrentGXItemList.Clear();

            if (Flver.Header.Version >= 0x20010)
            {
                foreach (var gx in Flver.GXLists[CurrentMaterial.GXIndex])
                {
                    CurrentGXItemList.Add(new GXItemContainer()
                    {
                        ID = GetUnscuffedGXID(gx.ID),
                        Bytes = string.Join(" ", gx.Data.Select(g => g.ToString("X2"))),
                        Unk = gx.Unk04,
                    });
                }
            }

            MainTabControl.SelectedItem = TabTextureMaps;
            SwitchTabs(isTextureMapsTab: true);
        }

        void ClearCurrentShit()
        {
            TextBoxMaterialName.Text = "";
            TextBoxMaterialDefinition.Text = "";
            DataGridTextureMaps.ItemsSource = null;
            CurrentMaterialIndex = -1;
            CurrentGXItemList.Clear();
            CurrentMaterial = null;
            CurrentMapList = null;
        }

        bool SwitchToMaterial(int materialIndex)
        {
            if (!SaveCurrentShitToMesh())
                return false;

            for (int i = 0; i < ListViewFlverMaterials.Items.Count; i++)
            {
                ((Label)ListViewFlverMaterials.Items[i]).FontWeight = i == materialIndex ? FontWeights.Bold : FontWeights.Normal;
            }

            
            if (materialIndex >= 0 && materialIndex <= Flver.Meshes.Count)
            {
                CurrentMaterial = Flver.Materials[materialIndex];
                LoadCurrentShitFromMesh();
            }
            else
            {
                CurrentMaterial = null;
                ClearCurrentShit();
            }

            CurrentMaterialIndex = materialIndex;

            return true;
        }

        private void MenuFileOpenFLVER_Click(object sender, RoutedEventArgs e)
        {
            var browseDialog = new OpenFileDialog()
            {
                Filter = "All Files (*.*)|*.*",
                Title = "Open FLVER Model or BND",
            };

            if (browseDialog.ShowDialog() == true)
            {
                var loadedFile = SFHelper.ReadFile<FLVER>(this, browseDialog.FileName);
                TextBlockRunCurrentlyEditing.Text = loadedFile.Uri;
                LoadFLVER(loadedFile.Uri, (FLVER)loadedFile.File);
            }
        }

        private void ListViewFlverMaterials_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (selectionChangeJustFailed)
            {
                selectionChangeJustFailed = false;
                return;
            }

            if (!SwitchToMaterial(ListViewFlverMaterials.SelectedIndex))
            {
                selectionChangeJustFailed = true;
                ListViewFlverMaterials.SelectedIndex = CurrentMaterialIndex;
            }

            SetIsEverythingDisabled(false);
        }

        void SetIsEverythingDisabled(bool isEverythingDisabled)
        {
            
            TextBoxMaterialDefinition.IsEnabled = !isEverythingDisabled;
            TextBoxMaterialName.IsEnabled = !isEverythingDisabled;
            DataGridTextureMaps.IsEnabled = !isEverythingDisabled;
            MainTabControl.IsEnabled = !isEverythingDisabled;

            
            TextBoxMaterialDefinition.Opacity = isEverythingDisabled ? 0.5f : 1.0f;
            TextBoxMaterialName.Opacity = isEverythingDisabled ? 0.5f : 1.0f;
            DataGridTextureMaps.Opacity = isEverythingDisabled ? 0.5f : 1.0f;
            MainTabControl.Opacity = isEverythingDisabled ? 0.5f : 1.0f;
        }

        void SetIsLoading(bool isLoading)
        {
            ListViewFlverMaterials.IsEnabled = !isLoading;
            ListViewFlverMaterials.Opacity = isLoading ? 0.5f : 1.0f;
            SetIsEverythingDisabled(isLoading);

            if (isLoading)
                Cursor = Cursors.Wait;
            else
                Cursor = Cursors.Arrow;
        }

        void Save()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Dispatcher.Invoke(() => SetIsLoading(true));
                if (SaveCurrentShitToMesh())
                {
                    SFHelper.WriteFile(Flver, FlverPath);
                }
                Dispatcher.Invoke(() => SetIsLoading(false));
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            
        }

        private void MenuFileSave_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void CommandBindingSave_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Save();
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabControl.SelectedItem == TabTextureMaps)
                SwitchTabs(isTextureMapsTab: true);
            else if (MainTabControl.SelectedItem == TabGXItems)
                SwitchTabs(isTextureMapsTab: false);
        }

        private void TextBoxMaterialName_TextChanged(object sender, TextChangedEventArgs e)
        {
            ((Label)ListViewFlverMaterials.SelectedItem).Content = TextBoxMaterialName.Text;
        }
    }
}
