namespace Zhally.Toolkit.TreeView;

public static class TreeNodeExtensions
{
    public static void ParenthoodTraverse<T>(this TreeNode<T> root, Predicate<TreeNode<T>> predicate, Action<TreeNode<T>, TreeNode<T>?> action, TreeNode<T>? parent) where T : TreeNodeContent, new()
    {
        action(root, parent);
        if (predicate(root))
        {
            return;
        }

        foreach (var child in root.Children)
        {
            child.ParenthoodTraverse(predicate, action, root);
        }
    }

    public static TreeNode<T>? FindTravase<T>(this TreeNode<T> parent, Predicate<TreeNode<T>> match)
        where T : TreeNodeContent, new()
    {
        if (match(parent))
        {
            return parent;
        }

        foreach (var child in parent.Children)
        {
            var found = child.FindTravase(match);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    public static void HuntTravase<T>(this TreeNode<T> parent, Predicate<TreeNode<T>> predicate, Action<TreeNode<T>> action) where T : TreeNodeContent, new()
    {
        if (predicate(parent))
        {
            action(parent);
            return;
        }

        foreach (var child in parent.Children)
        {
            child.HuntTravase(predicate, action);
        }
    }

    public static int GetTreeScopeMaxID<T>(this TreeNode<T> treeContext) where T : TreeNodeContent, new()
    {
        int maxID = treeContext.Primogenitor.Descendants()
            .Where(c => c.ID is not (TreeNodeContent.ContigencyID or TreeNodeContent.FavoritesID))
            .MaxBy(c => c.ID)?.ID ?? TreeNodeContent.PrimogenitorID + 1;
        return maxID;
    }

    public static TreeNode<T> SetContigency<T>(this TreeNode<T> primogenitor)
        where T : TreeNodeContent, new()
    {
        Lock _lock = new();

        if (primogenitor.Primogenitor != primogenitor && primogenitor.ID != TreeNodeContent.PrimogenitorID)
        {
            _ = Shell.Current.DisplayAlert("Error!", $"Primogenitor {primogenitor.ID}-{primogenitor.Title} is not properly set.", "Cancel");
            return primogenitor;
        }

        TreeNode<T>? contigency = primogenitor.FindTravase((context) => context.ID == TreeNodeContent.ContigencyID);
        lock (_lock)
        {
            if (contigency is null)
            {
                // 双重检查锁定，防止多个线程同时进入锁块后重复创建
                contigency = new()
                {
                    Content = new T()
                    {
                        ID = TreeNodeContent.ContigencyID,
                        Title = TreeNodeContent.ContigencyKey,
                    },
                    Primogenitor = primogenitor.Primogenitor,
                    IsLeaf = false,
                    IsExpanded = true,
                };

            }
            else
            {
                _ = contigency.Parent!.Children.Remove(contigency);
            }
        }
        primogenitor.Children.Insert(0, contigency);

        return contigency;
    }

    public static TreeNode<T> SetFavorites<T>(this TreeNode<T> contigency) where T : TreeNodeContent, new()
    {
        Lock _lock = new();

        // 查找 TreeNodeContent.FavoritesKey 节点
        TreeNode<T>? favorites = contigency.Primogenitor.FindTravase((context) => context.ID == TreeNodeContent.FavoritesID);

        lock (_lock)
        {
            if (favorites is null)
            {
                favorites = new()
                {
                    Content = new T()
                    {
                        ID = TreeNodeContent.FavoritesID,
                        Title = TreeNodeContent.FavoritesKey,
                    },
                    IsLeaf = false,
                    IsExpanded = false,
                };
            }
            else
            {
                _ = favorites.Parent?.Children.Remove(favorites);
            }
        }

        contigency.Children.Insert(0, favorites);
        return favorites;
    }

    public static TreeNode<T> SetTrash<T>(this TreeNode<T> contigency) where T : TreeNodeContent, new()
    {
        Lock _lock = new();

        // 查找 TreeNodeContent.TrashKey 节点
        TreeNode<T>? trash = contigency.Primogenitor.FindTravase((context) => context.ID == TreeNodeContent.TrashID);

        lock (_lock)
        {
            if (trash is null)
            {
                trash = new()
                {
                    Content = new T()
                    {
                        ID = TreeNodeContent.TrashID,
                        Title = TreeNodeContent.TrashKey,
                    },
                    IsLeaf = false,
                    IsExpanded = false,
                };
            }
            else
            {
                _ = trash.Parent!.Children.Remove(trash);
            }
        }

        contigency.Children.Add(trash);
        return trash;
    }
}