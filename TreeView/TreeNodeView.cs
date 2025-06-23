using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Zhally.Toolkit.DynamicGestures;

namespace Zhally.Toolkit.TreeView;

public partial class TreeNodeView<T> : VerticalStackLayout where T : TreeNodeContent, new()
{
    #region 字段与初始化
    private readonly Grid tvnGrid;
    private readonly BoxView focusBoxView;
    private readonly HorizontalStackLayout contentContainer;
    private readonly PrefixView<T> indentPrefixView;
    private readonly Label title;
    private readonly VerticalStackLayout childrenContainer;
    private ObservableCollection<TreeNodeView<T>> tvnChildren;
    #endregion

    #region 依赖属性
    public TreeNode<T> Context
    {
        get => (TreeNode<T>)GetValue(ContextProperty);
        set => SetValue(ContextProperty, value);
    }
    public static readonly BindableProperty ContextProperty = BindableProperty.Create(nameof(Context), typeof(TreeNode<T>), typeof(TreeNodeView<T>),
                                                              defaultValue: new TreeNode<T>(),
                                                              propertyChanged: OnContextPropertyChanged);

    private static void OnContextPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is TreeNodeView<T> @this && newValue is TreeNode<T> context)
        {
            @this.AppendDragDropRecognizers(context);
            @this.RenderOnOnContextPropertyChanged(oldValue, context);
        }
    }

    private void RenderOnOnContextPropertyChanged(object old, TreeNode<T> context)
    {
        if (old is not null and TreeNode<T> oldContext)
        {
            oldContext.PropertyChanged -= OnTreeNodePropertyChanged;
        }
        context.PropertyChanged += OnTreeNodePropertyChanged;
        indentPrefixView.BindingContext = context;
        Render();
    }

    private void OnTreeNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Render();
    }

    private void AppendDragDropRecognizers(TreeNode<T> context)
    {
        if (context == context.Primogenitor)    // 根节点禁用
        {
            contentContainer.Undraggable();
            contentContainer.Undroppable();
        }
        else
        {
            contentContainer.AsDraggable<HorizontalStackLayout, TreeNodeView<T>>(this, (anchor, view) => new DragDropPayload<TreeNodeView<T>>() { View = view, Affix = null, Callback = null });
            contentContainer.AsDroppable<HorizontalStackLayout, TreeNodeView<T>, TreeNodeView<T>>(new DragDropPayload<TreeNodeView<T>>() { View = this, Affix = null, Callback = null });
        }
    }

    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }
    public static readonly BindableProperty RowSpacingProperty = BindableProperty.Create(nameof(RowSpacing), typeof(double), typeof(TreeNodeView<T>),
                                                                 defaultValue: 8d,
                                                                 propertyChanged: (bindable, old, newValue) =>
                                                                 {
                                                                     if (bindable is TreeNodeView<T> @this && newValue is double rowSpacing)
                                                                     {
                                                                         @this.Spacing = rowSpacing;
                                                                         @this.tvnGrid.RowSpacing = rowSpacing;
                                                                     }
                                                                 });

    public bool AsFocus
    {
        get => (bool)GetValue(AsFocusProperty);
        set => SetValue(AsFocusProperty, value);
    }
    public static readonly BindableProperty AsFocusProperty = BindableProperty.Create(nameof(AsFocus), typeof(bool), typeof(TreeNodeView<T>),
                                                                 defaultValue: false,
                                                                 propertyChanged: (bindable, old, newValue) =>
                                                                 {
                                                                     if (bindable is TreeNodeView<T> @this && newValue is bool asFocus)
                                                                     {
                                                                         try
                                                                         {
                                                                             @this.focusBoxView.Color = asFocus ? Colors.Bisque : Colors.Transparent;
                                                                             @this.focusBoxView.Opacity = asFocus ? 0.3D : 0.0D;
                                                                         }
                                                                         catch (ObjectDisposedException) { }
                                                                     }
                                                                 });

    #endregion

    #region 构造函数
    public TreeNodeView()
    {
        VerticalOptions = LayoutOptions.Center;
        Spacing = this.RowSpacing;

        focusBoxView = new BoxView
        {
            Color = Colors.Transparent,
            Opacity = 0,
        };

        indentPrefixView = new PrefixView<T>();
        title = new Label
        {
            FontSize = 14,
            VerticalTextAlignment = TextAlignment.Center
        };
        title.GestureRecognizers.Add(new TapGestureRecognizer
        {
            NumberOfTapsRequired = 1,
            Command = new Command(() =>
            {
                if (!AsFocus)
                {
                    AsFocus = true;
                    _ = WeakReferenceMessenger.Default.Send(new TreeNodeFocusChangedMessage<T>(Context));
                }
            })
        });
        contentContainer = new HorizontalStackLayout() { Spacing = this.RowSpacing, VerticalOptions = LayoutOptions.Center };
        contentContainer.Children.Add(indentPrefixView); contentContainer.Children.Add(title);
        contentContainer.AsPointerPerceptible<HorizontalStackLayout>();


        childrenContainer = new VerticalStackLayout() { Spacing = this.RowSpacing, VerticalOptions = LayoutOptions.Center };

        tvnGrid = new Grid
        {
            RowSpacing = this.RowSpacing,
            ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Star } },
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto } }
        };
        tvnGrid.Add(focusBoxView, 0, 0);
        tvnGrid.Add(contentContainer, 0, 0);
        tvnGrid.Add(childrenContainer, 0, 1);

        this.Children.Add(tvnGrid);

        tvnChildren = [];
        tvnChildren.CollectionChanged += OnTvnChildrenChanged;

        WeakReferenceMessenger.Default.Register<TreeNodeFocusChangedMessage<T>>(this, (r, m) =>
        {
            AsFocus = Context.Primogenitor == m.Value.Primogenitor && Context == m.Value;
        });
    }
    #endregion


    #region 渲染逻辑
    public void Render()
    {
        indentPrefixView.Refresh();
        title.Text = Context.Title;
        title.Opacity = Context.IsEmpty ? 0.8 : 1.0;
        focusBoxView.Color = AsFocus ? Colors.Bisque : Colors.Transparent;
        focusBoxView.Opacity = AsFocus ? 0.3D : 0.0D;

        if (Context.IsExpanded)
        {
            BuildChildrenNotCreatedYet();
        }
        childrenContainer.IsVisible = Context.IsExpanded;

        foreach (var child in tvnChildren)
        {
            child.Render();
        }
    }

    private void BuildChildrenNotCreatedYet()
    {
        bool Built()
        {
            bool built = tvnChildren.Count == Context.Children.Count;
            if (built)
            {
                try
                {
                    for (int i = 0; i < tvnChildren.Count; i++)
                    {
                        built &= tvnChildren[i].Context == Context.Children[i];
                        if (!built) break;
                    }
                }
                catch
                {
                    built = false;
                }
            }
            return built;
        }

        if (!Built())
        {
            List<TreeNodeView<T>> removed = [.. childrenContainer.Children.OfType<TreeNodeView<T>>()];
            foreach (TreeNodeView<T> child in removed)
            {
                _ = childrenContainer.Children.Remove(child);
            }
            tvnChildren = [.. Context.Children.Select(context => CreateTreeViewNode(context))];
        }

        RenderChildren(tvnChildren, childrenContainer);
    }

    private static void RenderChildren(IEnumerable<TreeNodeView<T>>? tvnChildren, VerticalStackLayout childrenContainer)
    {

        if (tvnChildren is null) return;

        foreach (TreeNodeView<T> child in tvnChildren)
        {
            if (!childrenContainer.Children.Contains(child))
            {
                childrenContainer.Children.Add(child);
            }
        }
    }

    private void OnTvnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (TreeNodeView<T> child in e.NewItems!)
                {
                    if (!childrenContainer.Children.Contains(child))
                    {
                        childrenContainer.Children.Add(child);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (TreeNodeView<T> child in e.OldItems!)
                {
                    _ = childrenContainer.Children.Remove(child);
                }
                break;
        }
        Render();
    }
    #endregion


    #region 公共工厂方法
    public static TreeNodeView<T> CreateTreeViewNode(TreeNode<T> context)
    {
        TreeNodeView<T> node = new() { Context = context };
        node.Render();
        return node;
    }
    #endregion
    public override string ToString() =>
       $"TreeViewNode<T> => ID: {Context.ID};  tokenKey: {Context.Title}; Children: {Context.Children.Count};  IsExpanded: {Context.IsExpanded}";
}