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
        int CurrentMeshIndex = -1;
        FLVER.Mesh CurrentMesh = null;
        List<FLVER.Texture> CurrentMapList = new List<FLVER.Texture>();
        List<GXItemContainer> CurrentGXItemList = new List<GXItemContainer>();
        bool selectionChangeJustFailed = false;
        public MainWindow()
        {
            InitializeComponent();
        }

        void LoadFLVER(string path, FLVER f)
        {
            ClearCurrentShit();
            FlverPath = path;
            Flver = f;
            CurrentMeshIndex = -1;
            CurrentGXItemList.Clear();
            CurrentMesh = null;
            CurrentMapList = null;
            ListViewFLVERMeshes.Items.Clear();
            int i = 0;
            foreach (var m in f.Meshes)
            {
                ListViewFLVERMeshes.Items.Add(new Label() { Content = $"[{i++}]{(m.DefaultBoneIndex >= 0 ? $" {f.Bones[m.DefaultBoneIndex]}" : "")}" });
            }
            TabGXItems.IsEnabled = (Flver.Header.Version >= 0x20010);
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
            if (CurrentMesh == null)
                return true;
            Flver.Materials[CurrentMesh.MaterialIndex].Name = TextBoxMaterialName.Text;
            Flver.Materials[CurrentMesh.MaterialIndex].MTD = TextBoxMaterialDefinition.Text;

            bool didShitFail = false;

            if (Flver.Header.Version >= 0x20010)
            {
                try
                {
                    Flver.GXLists[Flver.Materials[CurrentMesh.MaterialIndex].GXIndex].Clear();
                    foreach (var gx in CurrentGXItemList)
                    {
                        Flver.GXLists[Flver.Materials[CurrentMesh.MaterialIndex].GXIndex]
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
                return uint.Parse(input);
            }
        }

        string GetUnscuffedGXID(uint input)
        {
            if (Flver.Header.Version >= 0x20013)
            {
                if (input == 0x7FFFFFFF)
                {
                    return "";
                }
                else
                {
                    return Encoding.ASCII.GetString(BitConverter.GetBytes(input));
                }
            }
            else
            {
                return input.ToString();
            }
        }

        void LoadCurrentShitFromMesh()
        {
            TextBoxMaterialName.Text = Flver.Materials[CurrentMesh.MaterialIndex].Name;
            TextBoxMaterialDefinition.Text = Flver.Materials[CurrentMesh.MaterialIndex].MTD;
            CurrentMapList = Flver.Materials[CurrentMesh.MaterialIndex].Textures;

            CurrentGXItemList.Clear();

            if (Flver.Header.Version >= 0x20010)
            {
                foreach (var gx in Flver.GXLists[Flver.Materials[CurrentMesh.MaterialIndex].GXIndex])
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
            CurrentMeshIndex = -1;
            CurrentGXItemList.Clear();
            CurrentMesh = null;
            CurrentMapList = null;
        }

        bool SwitchToMesh(int meshIndex)
        {
            if (!SaveCurrentShitToMesh())
                return false;

            for (int i = 0; i < ListViewFLVERMeshes.Items.Count; i++)
            {
                ((Label)ListViewFLVERMeshes.Items[i]).FontWeight = i == meshIndex ? FontWeights.Bold : FontWeights.Normal;
            }

            
            if (meshIndex >= 0 && meshIndex <= Flver.Meshes.Count)
            {
                CurrentMesh = Flver.Meshes[meshIndex];
                LoadCurrentShitFromMesh();
            }
            else
            {
                CurrentMesh = null;
                ClearCurrentShit();
            }

            CurrentMeshIndex = meshIndex;

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

        private void ListViewFLVERMeshes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (selectionChangeJustFailed)
            {
                selectionChangeJustFailed = false;
                return;
            }

            if (!SwitchToMesh(ListViewFLVERMeshes.SelectedIndex))
            {
                selectionChangeJustFailed = true;
                ListViewFLVERMeshes.SelectedIndex = CurrentMeshIndex;
            }
        }

        void SetIsLoading(bool isLoading)
        {
            ListViewFLVERMeshes.IsEnabled = !isLoading;
            TextBoxMaterialDefinition.IsEnabled = !isLoading;
            TextBoxMaterialName.IsEnabled = !isLoading;
            DataGridTextureMaps.IsEnabled = !isLoading;

            ListViewFLVERMeshes.Opacity = isLoading ? 0.5f : 1.0f;
            TextBoxMaterialDefinition.Opacity = isLoading ? 0.5f : 1.0f;
            TextBoxMaterialName.Opacity = isLoading ? 0.5f : 1.0f;
            DataGridTextureMaps.Opacity = isLoading ? 0.5f : 1.0f;

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
            }), System.Windows.Threading.DispatcherPriority.Background);
            
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
    }
}
