namespace Zhally.Toolkit.TreeView;

public sealed partial class TreeNodeChildren<T> : List<TreeNode<T>> where T : TreeNodeContent, new()
{
    public event EventHandler<ChildrenChangedDataPackage<T>>? ChildrenChanged;

    public TreeNodeChildren() : base() { }
    public TreeNodeChildren(int capacity) : base(capacity) { }
    public TreeNodeChildren(IEnumerable<TreeNode<T>> collection) : base(collection)
    {
        if (base.Count > 0)
        {
            int index = 0;
            foreach (var item in collection)
            {
                try
                {
                    ChildrenChanged?.Invoke(this, new ChildrenChangedDataPackage<T>(item, index, addorviceversa: true));
                }
                catch (Exception ex)
                {
                    _ = Shell.Current.DisplayAlert("TreeNodeChildren Error", $"ChildrenChanged Event invocation error: {ex.Message}", "OK");
                }
                index++;
            }
        }
    }

    public new void Add(TreeNode<T> item)
    {
        item.SetDepthIncDescendants();

        base.Add(item);
        ChildrenChanged?.Invoke(this, new ChildrenChangedDataPackage<T>(item, this.Count - 1, addorviceversa: true));
    }

    public new void Insert(int index, TreeNode<T> item)
    {
        item.SetDepthIncDescendants();

        base.Insert(index, item);
        ChildrenChanged?.Invoke(this, new ChildrenChangedDataPackage<T>(item, index, addorviceversa: true));
    }

    public new bool Remove(TreeNode<T> item)
    {
        int index = base.IndexOf(item);
        bool success = base.Remove(item);
        if (success)
        {
            ChildrenChanged?.Invoke(this, new ChildrenChangedDataPackage<T>(item, index, addorviceversa: false));
        }
        return success;
    }

    public new void RemoveAt(int index)
    {
        TreeNode<T> item = base[index];
        base.RemoveAt(index);
        ChildrenChanged?.Invoke(this, new ChildrenChangedDataPackage<T>(item, index, addorviceversa: false));
    }

    public new void RemoveRange(int index, int count)
    {
        for (int i = index + count - 1; i >= index; i--)
        {
            this.RemoveAt(i);
        }
    }

    public new void AddRange(IEnumerable<TreeNode<T>> collection)
    {
        int startIndex = base.Count;
        base.AddRange(collection);
        int index = startIndex;
        foreach (var item in collection)
        {
            item.SetDepthIncDescendants();

            ChildrenChanged?.Invoke(this, new ChildrenChangedDataPackage<T>(item, index, addorviceversa: true));
            index++;
        }
    }

    public new void InsertRange(int index, IEnumerable<TreeNode<T>> collection)
    {
        base.InsertRange(index, collection);
        foreach (var item in collection)
        {
            item.SetDepthIncDescendants();

            ChildrenChanged?.Invoke(this, new ChildrenChangedDataPackage<T>(item, index, addorviceversa: true));
            index++;
        }
    }

    public new int RemoveAll(Predicate<TreeNode<T>> match)
    {
        List<TreeNode<T>> itemsToRemove = [];
        for (int i = 0; i < this.Count; i++)
        {
            if (match(this[i]))
            {
                itemsToRemove.Add(this[i]);
            }
        }

        int removedCount = 0;
        foreach (var item in itemsToRemove)
        {
            int index = base.IndexOf(item);
            if (base.Remove(item))
            {
                ChildrenChanged?.Invoke(this, new ChildrenChangedDataPackage<T>(item, index, addorviceversa: false));
                removedCount++;
            }
        }
        return removedCount;
    }

    [Obsolete("此方法在 TreeNodeChildren 中已过时，不建议使用。", false)]
    public new void Reverse() { }

    [Obsolete("此方法在 TreeNodeChildren 中已过时，不建议使用。", false)]
    public new void Reverse(int index, int count) { }

    [Obsolete("此方法在 TreeNodeChildren 中已过时，不建议使用。", false)]
    public new void Sort() { }

    [Obsolete("此方法在 TreeNodeChildren 中已过时，不建议使用。", false)]
    public new void Sort(IComparer<TreeNode<T>>? comparer) { }

    [Obsolete("此方法在 TreeNodeChildren 中已过时，不建议使用。", false)]
    public new void Sort(int index, int count, IComparer<TreeNode<T>>? comparer) { }

    [Obsolete("此方法在 TreeNodeChildren 中已过时，不建议使用。", false)]
    public new void Sort(Comparison<TreeNode<T>> comparison) { }
}