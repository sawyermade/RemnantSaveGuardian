﻿using RemnantSaveGuardian.locales;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Common.Interfaces;

namespace RemnantSaveGuardian.Views.Pages
{
    /// <summary>
    /// Interaction logic for WorldAnalyzerPage.xaml
    /// </summary>
    public partial class WorldAnalyzerPage : INavigableView<ViewModels.WorldAnalyzerViewModel>
    {
        public ViewModels.WorldAnalyzerViewModel ViewModel
        {
            get;
        }
        private RemnantSave Save;
        private List<RemnantWorldEvent> filteredCampaign;
        private List<RemnantWorldEvent> filteredAdventure;
        public WorldAnalyzerPage(ViewModels.WorldAnalyzerViewModel viewModel, string? pathToSaveFiles = null)
        {
            ViewModel = viewModel;

            InitializeComponent();

            try
            {
                if (pathToSaveFiles == null)
                {
                    pathToSaveFiles = Properties.Settings.Default.SaveFolder;
                }

                Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;

                Save = new(pathToSaveFiles);
                if (pathToSaveFiles == Properties.Settings.Default.SaveFolder)
                {
                    SaveWatcher.SaveUpdated += (sender, eventArgs) => {
                        Dispatcher.Invoke(() =>
                        {
                            var selectedIndex = CharacterControl.SelectedIndex;
                            Save.UpdateCharacters();
                            CharacterControl.Items.Refresh();
                            if (selectedIndex >= CharacterControl.Items.Count)
                            {
                                selectedIndex = 0;
                            }
                            CharacterControl.SelectedIndex = selectedIndex;
                            //CharacterControl_SelectionChanged(null, null);
                        });
                    };
                    Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;
                    BackupsPage.BackupSaveRestored += BackupsPage_BackupSaveRestored;
                }
                CharacterControl.ItemsSource = Save.Characters;
                Save.UpdateCharacters();

                //FontSizeSlider.Value = AdventureData.FontSize;
                //FontSizeSlider.Minimum = 2.0;
                //FontSizeSlider.Maximum = AdventureData.FontSize * 2;
                FontSizeSlider.Value = Properties.Settings.Default.AnalyzerFontSize;
                FontSizeSlider.ValueChanged += FontSizeSlider_ValueChanged;

                filteredCampaign = new();
                filteredAdventure = new();
                CampaignData.ItemsSource = filteredCampaign;
                AdventureData.ItemsSource = filteredAdventure;

                CharacterControl.SelectedIndex = 0;
                checkAdventureTab();
            } catch (Exception ex) {
                Logger.Error($"Error initializing analzyer page: {ex}");
            }

        }

        private void BackupsPage_BackupSaveRestored(object? sender, EventArgs e)
        {
            var selectedIndex = CharacterControl.SelectedIndex;
            Save.UpdateCharacters();
            CharacterControl.Items.Refresh();
            if (selectedIndex >= CharacterControl.Items.Count)
            {
                selectedIndex = 0;
            }
            CharacterControl.SelectedIndex = selectedIndex;
        }

        public WorldAnalyzerPage(ViewModels.WorldAnalyzerViewModel viewModel) : this(viewModel, null)
        {
            
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Properties.Settings.Default.AnalyzerFontSize = (int)Math.Round(FontSizeSlider.Value);
            reloadEventGrids();
        }

        private void GameType_CollapsedExpanded(object sender, PropertyChagedEventArgs e)
        {
            TreeListClass item = (TreeListClass)sender;
            Properties.Settings.Default[$"{item.Tag}_Expanded"] = item.IsExpanded;
        }

        private void SavePlaintextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Forms.FolderBrowserDialog openFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
                openFolderDialog.Description = Loc.T("Export save files as plaintext");
                openFolderDialog.UseDescriptionForTitle = true;
                System.Windows.Forms.DialogResult result = openFolderDialog.ShowDialog();
                if (result != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                if (openFolderDialog.SelectedPath == Properties.Settings.Default.SaveFolder || openFolderDialog.SelectedPath == Save.SaveFolderPath)
                {
                    Logger.Error(Loc.T("export_save_invalid_folder_error"));
                    return;
                }
                File.WriteAllText($@"{openFolderDialog.SelectedPath}\profile.txt", Save.GetProfileData());
                File.Copy(Save.SaveProfilePath, $@"{openFolderDialog.SelectedPath}\profile.sav", true);
                foreach (var filePath in Save.WorldSaves)
                {
                    File.WriteAllText($@"{openFolderDialog.SelectedPath}\{filePath.Substring(filePath.LastIndexOf(@"\")).Replace(".sav", ".txt")}", RemnantSave.DecompressSaveAsString(filePath));
                    File.Copy(filePath, $@"{openFolderDialog.SelectedPath}\{filePath.Substring(filePath.LastIndexOf(@"\"))}", true);
                }
                Logger.Success(Loc.T($"Exported save files successfully to {openFolderDialog.SelectedPath}"));
            } catch (Exception ex)
            {
                Logger.Error(Loc.T("Error exporting save files: {errorMessage}", new() { { "errorMessage", ex.Message } }));
            }
        }

        private void Default_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ShowPossibleItems" || e.PropertyName == "MissingItemColor")
            {
                Dispatcher.Invoke(() =>
                {
                    reloadEventGrids();
                });
            }
            if (e.PropertyName == "SaveFolder")
            {
                Dispatcher.Invoke(() => {
                    Save = new(Properties.Settings.Default.SaveFolder);
                    Save.UpdateCharacters();
                    reloadPage();
                    checkAdventureTab();
                });
            }
            if (e.PropertyName == "ShowCoopItems")
            {
                Dispatcher.Invoke(() =>
                {
                    Save.UpdateCharacters();
                    reloadEventGrids();
                    CharacterControl_SelectionChanged(null, null);
                });
            }

            if(e.PropertyName == "Language")
            {
                Dispatcher.Invoke(() =>
                {
                    reloadEventGrids();
                    CharacterControl_SelectionChanged(null, null);
                });
            }
        }

        private void Data_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var allowColumns = new List<string>() {
                "Location",
                "Type",
                "Name",
                "MissingItemsString",
                "PossibleItemsString"
            };
            if (!allowColumns.Contains(e.Column.Header))
            {
                e.Cancel = true;
                return;
            }
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(FontSizeProperty, FontSizeSlider.Value));
            if (e.Column.Header.Equals("MissingItemsString"))
            {
                e.Column.Header = "Missing Items";
                
                if (Properties.Settings.Default.MissingItemColor == "Highlight")
                {
                    var highlight = System.Drawing.SystemColors.HotTrack;
                    cellStyle.Setters.Add(new Setter(ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(highlight.R, highlight.G, highlight.B))));
                }
            }
            else if (e.Column.Header.Equals("PossibleItemsString"))
            {
                e.Column.Header = "Possible Items";

                if (!Properties.Settings.Default.ShowPossibleItems)
                {
                    e.Cancel = true;
                    return;
                }
            }
            e.Column.CellStyle = cellStyle;
            e.Column.Header = Loc.T(e.Column.Header.ToString());
        }

        private static string[] ModeTags = { "treeMissingNormal", "treeMissingHardcore", "treeMissingSurvival" };
        List<TreeListClass> itemModeNode = new List<TreeListClass>();
        private void CharacterControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //if (CharacterControl.SelectedIndex == -1 && listCharacters.Count > 0) return;
            if (CharacterControl.Items.Count > 0 && CharacterControl.SelectedIndex > -1)
            {
                checkAdventureTab();
                applyFilter();
                //txtMissingItems.Text = string.Join("\n", Save.Characters[CharacterControl.SelectedIndex].GetMissingItems());

                itemModeNode.Clear();
                List<TreeListClass>[] itemNode = new List<TreeListClass>[3] { new List<TreeListClass>(), new List<TreeListClass>(), new List<TreeListClass>() };
                List<TreeListClass>[] itemChild = new List<TreeListClass>[20];
                string[] Modes = { Strings.Normal, Strings.Hardcore, Strings.Survival };
                for (int i = 0;i <= 2; i++)
                {
                    TreeListClass item = new TreeListClass() { Name = Modes[i], Childnode = itemNode[i], Tag = ModeTags[i], IsExpanded = (bool)Properties.Settings.Default[$"{ModeTags[i]}_Expanded"] };
                    item.Expanded += GameType_CollapsedExpanded;
                    itemModeNode.Add(item);
                }
                var idx = -1;
                string typeNodeTag = "";

                Save.Characters[CharacterControl.SelectedIndex].GetMissingItems().Sort(new SortCompare());
                foreach (RemnantItem rItem in Save.Characters[CharacterControl.SelectedIndex].GetMissingItems())
                {
                    string modeNode = ModeTags[(int)rItem.ItemMode];
                    if (!typeNodeTag.Equals($"{modeNode}{rItem.RawType}"))
                    {
                        typeNodeTag = $"{modeNode}{rItem.RawType}";
                        idx++;
                        itemChild[idx] = new List<TreeListClass>();
                        bool isExpanded = true;
                        try
                        {
                            isExpanded = (bool)Properties.Settings.Default[$"{typeNodeTag}_Expanded"];
                        } catch (Exception ex) {
                            Logger.Warn($"Not found properties: {typeNodeTag}_Expand");
                        }
                        TreeListClass item = new TreeListClass() { Name = rItem.Type, Childnode = itemChild[idx], Tag = typeNodeTag, IsExpanded = isExpanded };
                        item.Expanded += GameType_CollapsedExpanded;
                        itemNode[(int)rItem.ItemMode].Add(item) ;
                    }
                    itemChild[idx].Add(new TreeListClass() { Name = rItem.Name, Notes = Loc.GameTHas($"{rItem.RawName}_Notes") ? Loc.GameT($"{rItem.RawName}_Notes") : rItem.ItemNotes, Tag = rItem });
                }

                treeMissingItems.ItemsSource = null;
                treeMissingItems.ItemsSource = itemModeNode;

                foreach (TreeListClass modeNode in itemModeNode)
                {
                    if (modeNode != null)
                    {
                        modeNode.Visibility = (modeNode.Childnode.Count == 0 ? Visibility.Collapsed : Visibility.Visible);
                    }
                }
                foreach (List<TreeListClass> categoryNode in itemNode)
                {
                    if (categoryNode != null)
                    {
                        foreach (TreeListClass category in categoryNode)
                        {
                            category.Visibility = (category.Childnode.Count == 0 ? Visibility.Collapsed : Visibility.Visible);
                        }
                        categoryNode.Sort();
                    }
                }
                foreach (List<TreeListClass> typeNode in itemChild) {
                    if (typeNode != null) {
                        typeNode.Sort();
                    }
                }
            }
        }

        private void checkAdventureTab()
        {
            Dispatcher.Invoke(() => {
                if (CharacterControl.SelectedIndex > -1 && Save.Characters[CharacterControl.SelectedIndex].AdventureEvents.Count > 0)
                {
                    tabAdventure.IsEnabled = true;
                }
                else
                {
                    tabAdventure.IsEnabled = false;
                    if (tabAnalyzer.SelectedIndex == 1)
                    {
                        tabAnalyzer.SelectedIndex = 0;
                    }
                }
            });
        }
        private void reloadPage()
        {
            CharacterControl.ItemsSource = null;
            CharacterControl.ItemsSource = Save.Characters;
            if (filteredCampaign == null) filteredCampaign = new();
            if (filteredAdventure == null) filteredAdventure = new();
            CharacterControl.SelectedIndex = 0;
            CharacterControl.Items.Refresh();
        }
        private void reloadEventGrids()
        {
            var tempData = filteredCampaign;
            CampaignData.ItemsSource = null;
            CampaignData.ItemsSource = tempData;

            tempData = filteredAdventure;
            AdventureData.ItemsSource = null;
            AdventureData.ItemsSource = tempData;
        }

        private string GetTreeListItem(TreeListClass item)
        {
            if (item == null)
            {
                return "";
            }
            if (item.Tag.GetType().ToString() == "RemnantSaveGuardian.RemnantItem") return item.Name;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(item.Name + ":");
            foreach (TreeListClass i in item.Childnode)
            {
                sb.AppendLine("\t- " + GetTreeListItem(i));
            }
            return sb.ToString();
        }

        private void CopyItem_Click(object sender, RoutedEventArgs e)
        {
            if (treeMissingItems.SelectedItem == null)
            {
                return;
            }
            Clipboard.SetDataObject(GetTreeListItem((TreeListClass)treeMissingItems.SelectedItem));
        }

        private void SearchItem_Click(object sender, RoutedEventArgs e)
        {
            var treeItem = (TreeListClass)treeMissingItems.SelectedItem;
            var item = (RemnantItem)treeItem.Tag;

            var itemname = treeItem.Name;
            if (!WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.Culture.ToString().StartsWith("en"))
            {
                itemname = item.RawName;
                var setFound = false;
                if (item.RawType == "Armor")
                {
                    var armorMatch = Regex.Match(itemname, @"\w+_(?<armorPart>(?:Head|Body|Gloves|Legs))_\w+");
                    if (armorMatch.Success)
                    {
                        itemname = itemname.Replace($"{armorMatch.Groups["armorPart"].Value}_", "");
                        setFound = true;
                    }
                }
                itemname = Loc.GameT(itemname, new() { { "locale", "en-US" } });
                if (setFound)
                {
                    itemname += " (";
                }
            } 

            if (item.RawType == "Armor")
            {
                itemname = itemname?.Substring(0, itemname.IndexOf("(")) + "Set";
            }
            Process.Start("explorer.exe", $"https://remnant2.wiki.fextralife.com/{itemname}");
        }
        private void ExpandAllItem_Click(object sender, RoutedEventArgs e)
        {
            CollapseExpandAllItems(itemModeNode, true);
        }
        private void CollapseAllItem_Click(object sender, RoutedEventArgs e)
        {
            CollapseExpandAllItems(itemModeNode, false);
        }
        private void CollapseExpandAllItems(List<TreeListClass> lstItems, bool bExpand)
        {
            foreach (TreeListClass item in lstItems)
            {
                item.IsExpanded = bExpand;
                var child = item.Childnode;
                if (child != null && child.Count > 0)
                {
                    var node = child[0].Childnode;
                    if (node != null && node.Count > 0) { CollapseExpandAllItems(child, bExpand); }
                }
            }
        }
        private void treeMissingItems_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var item = (TreeListClass)treeMissingItems.SelectedItem;
            if (item != null)
            {
                menuMissingItemOpenWiki.Visibility = item.Tag.GetType().ToString() != "RemnantSaveGuardian.RemnantItem" ? Visibility.Collapsed : Visibility.Visible;
                menuMissingItemCopy.Visibility = Visibility.Visible;
            } else {
                menuMissingItemOpenWiki.Visibility = Visibility.Collapsed;
                menuMissingItemCopy.Visibility = Visibility.Collapsed;
            }
        }

        private void treeMissingItems_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item != null)
            {
                var node = (TreeListClass)item.Header;
                if (node != null) {
                    node.IsSelected = true;
                    e.Handled = true;
                }
            }
        }

        private void WorldAnalyzerFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            applyFilter();
        }
        private bool eventPassesFilter(RemnantWorldEvent e)
        {
            var filter = WorldAnalyzerFilter.Text.ToLower();
            if (filter.Length == 0)
            {
                return true;
            }
            if (e.MissingItemsString.ToLower().Contains(filter))
            {
                return true;
            }
            if (Properties.Settings.Default.ShowPossibleItems && e.PossibleItemsString.ToLower().Contains(filter))
            {
                return true;
            }
            if (e.Name.ToLower().Contains(filter))
            {
                return true;
            }
            return false;
        }
        private void applyFilter()
        {
            if (CharacterControl.Items.Count == 0 || CharacterControl.SelectedIndex == -1)
            {
                return;
            }
            var character = Save.Characters[CharacterControl.SelectedIndex];
            if (character == null)
            {
                return;
            }
            filteredCampaign.Clear();
            filteredCampaign.AddRange(character.CampaignEvents.FindAll(e => eventPassesFilter(e)));
            filteredAdventure.Clear();
            filteredAdventure.AddRange(character.AdventureEvents.FindAll(e => eventPassesFilter(e)));
            reloadEventGrids();
        }
        public class SortCompare : IComparer<RemnantItem>
        {
            public int Compare(RemnantItem? x, RemnantItem? y)
            {
                if (x.ItemMode != y.ItemMode)
                {
                    return x.ItemMode.CompareTo(y.ItemMode);
                } else {
                    return x.RawType.CompareTo(y.RawType);
                }
            }
        }
        public class TreeListClass : IComparable<TreeListClass>, INotifyPropertyChanged
        {
            public delegate void EventHandler(object sender, PropertyChagedEventArgs e);
            public event EventHandler Expanded;
            public event PropertyChangedEventHandler PropertyChanged;
            private List<TreeListClass> _childnode;
            private Object _tag;
            private bool _isselected;
            private bool _isexpanded;
            private Visibility _visibility;
            public String Name { get; set; }
            public String? Notes { get; set; }
            public Object Tag {
                get { return _tag == null ? new object() : _tag; }
                set { _tag = value; }
            }
            public List<TreeListClass> Childnode {
                get { return _childnode == null ? new List<TreeListClass>(0) : _childnode; }
                set { _childnode = value; }
            }
            public bool IsSelected {
                get { return _isselected; }
                set { _isselected = value; OnPropertyChanged(); }
            }
            public bool IsExpanded
            {
                get { return _isexpanded; }
                set
                {
                    if (value != _isexpanded)
                    {
                        _isexpanded = value;
                        OnPropertyChanged();
                        PropertyChagedEventArgs ev = new PropertyChagedEventArgs("IsExpanded", _isexpanded, value);
                        if (Expanded != null)
                        {
                            this.Expanded(this, ev);
                        }
                    }
                }
            }
            public Visibility Visibility
            {
                get { return _visibility; }
                set { _visibility = value; OnPropertyChanged(); }
            }
            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                if (this.PropertyChanged != null)
                    this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
            public int CompareTo(TreeListClass other)
            {
                return Name.CompareTo(other.Name);
            }
        }
        public class PropertyChagedEventArgs : EventArgs
        {
            public PropertyChagedEventArgs(string propertyName, object oldValue, object newValue)
            {
                PropertyName = propertyName;
                OldValue = oldValue;
                NewValue = newValue;
            }
            public string PropertyName { get; private set; }
            public object OldValue { get; private set; }
            public object NewValue { get; set; }
        }
    }
}
