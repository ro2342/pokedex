using Windows.UI.Xaml.Media;

namespace PokedexUWP
{
    // View-model pro card do GridView - o XAML usa x:Bind contra isso, nao
    // contra o Models.PokemonInfo direto (que nao tem cor/imagem/labels
    // pre-calculados).
    public sealed class PokemonCardVm
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string NumberLabel { get; set; }
        public string TagsLabel { get; set; }
        public string TypesLabel { get; set; }
        public ImageSource ImageSource { get; set; }
        public SolidColorBrush DotBrush { get; set; }
    }
}
