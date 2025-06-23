using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using System.ComponentModel;

namespace Zhally.Toolkit.TreeView;

public partial class TreeNode<T> : ObservableObject where T : TreeNodeContent, new()
{
    private readonly TreeNodeChildren<T> _children;

    public TreeNode()
    {
        Primogenitor = this;
        parent = null;
        _children = [];
        _children.ChildrenChanged += OnChildrenChanged;
        _content.PropertyChanged += OnContentInvalidated;
    }

    public TreeNodeChildren<T> Children => _children;
    private void OnChildrenChanged(object? sender, ChildrenChangedDataPackage<T> e)
    {
        e.Child.Parent = e.AddOrViceVersa ? this : null;
        e.Child.Primogenitor = e.AddOrViceVersa ? this.Primogenitor : e.Child;
        e.Child.SetDepthIncDescendants();
        if (e.AddOrViceVersa)
        {
            double serial = e.Index == 0 ? 0 : _children[e.Index - 1].Serial + 1;
            for (int i = e.Index; i < _children.Count; i++)
            {
                _children[i].Serial = serial++;
            }
        }

        if (!IsLeaf)
        {
            required = _children.Any(c => c.Required);
        }

        _ = WeakReferenceMessenger.Default.Send<TreeNodeChildrenChangedMessage<T>>(new(this, e));
    }
    public int ID => Content.ID;
    public double Serial { get; set; } = 0D;
    public string Title
    {
        get => Content.Title;
        set
        {
            if (Content.Title != value)
            {
                Content.Title = value;
                OnPropertyChanged(nameof(Title));
            }
        }
    }


    public bool IsLeaf { get; set; } = true;

    private bool required = false;
    public bool Required
    {
        get => IsLeaf ? required : Children.Any(c => c.Required);
        set
        {
            if (required != value)
            {
                bool old = IsLeaf ? required : Children.Any(c => c.Required);
                required = value;

                _ = WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(
                    sender: this,
                    propertyName: nameof(Required),
                    oldValue: old,
                    newValue: value
                ));
            }
        }
    }

    private T _content = new();
    public T Content
    {
        get => _content; set
        {
            if (_content != value)
            {
                if (_content != null)
                {
                    _content.PropertyChanged -= OnContentInvalidated; // 取消旧订阅}
                }

                _content = value;

                if (_content != null)
                {
                    _content.PropertyChanged += OnContentInvalidated; // 添加新订阅
                }
            }
        }
    }

    private void OnContentInvalidated(object? sender, PropertyChangedEventArgs e)
    {
        _ = WeakReferenceMessenger.Default.Send<TreeNodeContentInvalidatedMessage<T>>(new(this));
    }

    public int Depth { get; set; } = 0;

    public bool IsEmpty => (!IsLeaf && Children.Count == 0) || (IsLeaf && Content is null);

    public TreeNode<T> Primogenitor { get; set; }


    TreeNode<T>? parent;
    public TreeNode<T>? Parent
    {
        get => parent ?? this.Ancestors().LastOrDefault();
        protected set
        {
            if (parent != value)
            {
                parent = value;
            }
        }
    }

    private bool isExpanded = false;
    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded != value)
            {
                isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));

                if (!isExpanded)
                {
                    foreach (TreeNode<T> descendant in Descendants())
                    {
                        descendant.IsExpanded = false;
                    }
                }
                else if (Primogenitor is not null)
                {
                    foreach (TreeNode<T> ancestor in Ancestors(Primogenitor))
                    {
                        ancestor.IsExpanded = true;
                    }
                }
            }
        }
    }

    public void SetDepthIncDescendants()
    {
        this.ParenthoodTraverse((context) => context.IsLeaf || context.Children.Count == 0, (context, parent) =>
        {
            context.Depth = parent is null ? 0 : (parent.Depth + 1);
        }, this.Parent);
    }

    public IEnumerable<TreeNode<T>> Descendants()
    {
        Stack<TreeNode<T>> stack = new(this.Children);

        while (stack.Count > 0)
        {
            TreeNode<T> node = stack.Pop();
            yield return node;

            foreach (TreeNode<T> child in node.Children)
            {
                stack.Push(child);
            }
        }
    }

    public IEnumerable<TreeNode<T>> Ancestors(TreeNode<T>? pg = null)
    {
        TreeNode<T> root = pg ?? Primogenitor;
        if (!root.Descendants().Contains(this))
        {
            yield break;
        }

        Stack<TreeNode<T>> stack = new();
        TreeNode<T> current = this;
        while (current != current.Primogenitor && !root.Children.Contains(current))
        {
            TreeNode<T> ancester = root.Descendants().First<TreeNode<T>>(node => node.Children.Contains(current));
            stack.Push(ancester);
            current = ancester;
        }
        stack.Push(root);

        while (stack.Count > 0)
        {
            yield return stack.Pop();
        }
    }

    public override string ToString() => $"TreeNode<{typeof(T).Name}>(ID={ID}, Key={Title}, Children={Children.Count}, IsLeaf={IsLeaf})";
}