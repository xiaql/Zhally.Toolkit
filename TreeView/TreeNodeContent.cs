using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Zhally.Toolkit.TreeView;
public abstract class TreeNodeContent : ObservableObject, ITreeNodeContent
{
    public static string PrimogenitorKey { get; set; } = "Root"; //数独题集（目录）
    public const int PrimogenitorID =int.MinValue;

    public static string ContigencyKey { get; set; } = "Contigency";//自由诗
    public const int ContigencyID = int.MaxValue;

    public static string FavoritesKey { get; set; } = "Favorites";
    public const int FavoritesID = -1;

    public static string TrashKey { get; set; } = "Trash";
    public const int TrashID = -int.MaxValue;

    public static readonly Collection<string> ReservedCatalogueNames = ["Samples", TreeNodeContent.FavoritesKey, TreeNodeContent.TrashKey];

    public int ID { get; set; } = 0;
    private string title = string.Empty;
    public string Title
    {
        get => title;
        set
        {
            if (title != value)
            {
                title = value;
                OnPropertyChanged(nameof(Title));
            }
        }
    }
}
