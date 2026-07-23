using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using PokedexUWP.Models;
using PokedexUWP.Services;
using Windows.Data.Json;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace PokedexUWP
{
    public sealed partial class MainPage : Page
    {
        private JsonObject _store;
        private readonly HashSet<string> _activeChips = new HashSet<string>();
        private string _myTab = "all";
        private string _searchQuery = "";
        private readonly ObservableCollection<PokemonCardVm> _items = new ObservableCollection<PokemonCardVm>();

        private static readonly Dictionary<string, Color> GameColors = new Dictionary<string, Color>
        {
            ["go"] = Color.FromArgb(255, 0xA8, 0xB8, 0x00),
            ["bd"] = Color.FromArgb(255, 0x40, 0x70, 0xE0),
            ["home"] = Color.FromArgb(255, 0xD0, 0x40, 0xA0),
            ["arceus"] = Color.FromArgb(255, 0xD0, 0x80, 0x00),
            ["za"] = Color.FromArgb(255, 0x30, 0xA0, 0x60),
        };

        public MainPage()
        {
            InitializeComponent();
            PokemonGrid.ItemsSource = _items;
            Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            await ContentStore.LoadAsync();
            _store = await LocalDataStore.LoadAsync();
            Render();
        }

        // --- filtros ---

        private void Chip_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton btn = (ToggleButton)sender;
            string gid = (string)btn.Tag;
            if (btn.IsChecked == true) _activeChips.Add(gid);
            else _activeChips.Remove(gid);
            Render();
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton clicked = (ToggleButton)sender;
            foreach (ToggleButton tab in TabsPanel.Children.OfType<ToggleButton>())
            {
                tab.IsChecked = ReferenceEquals(tab, clicked);
            }
            _myTab = (string)clicked.Tag;
            Render();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchQuery = SearchBox.Text.Trim().ToLowerInvariant();
            Render();
        }

        private async void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            MessageDialog confirm = new MessageDialog("Apagar tudo?", "Confirmar");
            confirm.Commands.Add(new UICommand("Apagar"));
            confirm.Commands.Add(new UICommand("Cancelar"));
            confirm.CancelCommandIndex = 1;
            IUICommand result = await confirm.ShowAsync();
            if (result.Label != "Apagar") return;

            _store = new JsonObject();
            await LocalDataStore.SaveAsync(_store);
            Render();
        }

        // --- render ---

        private void Render()
        {
            IEnumerable<PokemonInfo> list = ContentStore.Pokemon.Where(p =>
            {
                if (!string.IsNullOrEmpty(_searchQuery) &&
                    !p.Name.Contains(_searchQuery) && !p.Id.ToString().Contains(_searchQuery))
                {
                    return false;
                }
                if (_myTab == "no") return LocalDataStore.MyLocations(_store, p.Id).Count == 0;
                if (_myTab != "all") return LocalDataStore.Has(_store, p.Id, _myTab);
                if (_activeChips.Count > 0)
                {
                    return _activeChips.Any(gid => ContentStore.InDex(p.Id, gid));
                }
                return true;
            });

            _items.Clear();
            foreach (PokemonInfo p in list)
            {
                List<string> myLocs = LocalDataStore.MyLocations(_store, p.Id);
                Color dotColor;
                if (myLocs.Count == 0)
                {
                    dotColor = Color.FromArgb(255, 0xE0, 0xD0, 0xE0);
                }
                else if (myLocs.Count > 1)
                {
                    dotColor = Color.FromArgb(255, 0xD0, 0x40, 0xA0);
                }
                else
                {
                    dotColor = GameColors.TryGetValue(myLocs[0], out Color c) ? c : Colors.Gray;
                }

                string tags = string.Join(" ", ContentStore.ChipIds
                    .Where(gid => ContentStore.InDex(p.Id, gid))
                    .Select(gid => ContentStore.GameById(gid)?.Label ?? gid));

                _items.Add(new PokemonCardVm
                {
                    Id = p.Id,
                    Name = p.Name,
                    NumberLabel = "#" + p.Id.ToString("D4"),
                    TagsLabel = tags,
                    TypesLabel = string.Join(" / ", p.Types),
                    ImageSource = new BitmapImage(new Uri($"ms-appx:///Assets/Sprites/{p.Id}.png")),
                    DotBrush = new SolidColorBrush(dotColor),
                });
            }

            int total = ContentStore.Pokemon.Count;
            int inHome = ContentStore.Pokemon.Count(p => LocalDataStore.Has(_store, p.Id, "home"));
            int pct = total == 0 ? 0 : (int)Math.Round(inHome * 100.0 / total);
            StatsText.Text = $"{total} pokemon - {inHome} no HOME ({pct}%)";
            HomeProgress.Value = pct;
        }

        // --- detalhe ---

        private async void PokemonGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            PokemonCardVm card = (PokemonCardVm)e.ClickedItem;
            PokemonInfo info = ContentStore.Pokemon.First(p => p.Id == card.Id);
            await ShowDetailDialogAsync(info);
        }

        private async Task ShowDetailDialogAsync(PokemonInfo info)
        {
            StackPanel body = new StackPanel { Padding = new Thickness(4) };

            body.Children.Add(new Image
            {
                Source = new BitmapImage(new Uri($"ms-appx:///Assets/Sprites/{info.Id}.png")),
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            body.Children.Add(new TextBlock
            {
                Text = $"#{info.Id:D4}  {info.Name}",
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            body.Children.Add(new TextBlock
            {
                Text = string.Join(" / ", info.Types),
                Foreground = new SolidColorBrush(Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
            });

            body.Children.Add(new TextBlock { Text = "ONDE EU TENHO", FontWeight = Windows.UI.Text.FontWeights.Bold, FontSize = 12 });

            foreach (GameConfig g in ContentStore.Games)
            {
                bool has = LocalDataStore.Has(_store, info.Id, g.Id);
                ToggleSwitch toggle = new ToggleSwitch
                {
                    Header = g.Name + (g.Oneway ? " (1-WAY)" : ""),
                    OnContent = g.Sub,
                    OffContent = g.Sub,
                    IsOn = has,
                    Tag = g.Id,
                };
                toggle.Toggled += async (s, args) =>
                {
                    ToggleSwitch ts = (ToggleSwitch)s;
                    LocalDataStore.Set(_store, info.Id, (string)ts.Tag, ts.IsOn);
                    await LocalDataStore.SaveAsync(_store);
                    Render();
                };
                body.Children.Add(toggle);
            }

            if (LocalDataStore.Has(_store, info.Id, "za"))
            {
                body.Children.Add(new TextBlock
                {
                    Text = "Aviso: no Z-A a transferencia e permanente - nao volta pros outros jogos.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 0x80, 0x30, 0x20)),
                    FontSize = 11,
                    Margin = new Thickness(0, 8, 0, 0),
                });
            }

            ContentDialog dialog = new ContentDialog
            {
                Title = info.Name,
                Content = new ScrollViewer { Content = body, MaxHeight = 480 },
                CloseButtonText = "Fechar",
            };
            await dialog.ShowAsync();
        }

        // --- sincronizacao ---

        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            FirebaseSession session = SessionService.GetSession();
            if (session == null)
            {
                AuthResult result = await AuthService.SignInWithGoogleAsync();
                if (!result.Success)
                {
                    await new MessageDialog(result.ErrorMessage, "Falha no login").ShowAsync();
                    return;
                }
                SessionService.SaveSession(result.Uid, result.IdToken, result.RefreshToken, result.Email, result.ExpiresInSeconds);
            }

            string status = await SyncService.SyncNowAsync();
            _store = await LocalDataStore.LoadAsync();
            Render();
            await new MessageDialog(status, "Sincronizacao").ShowAsync();
        }
    }
}
