using CommunityToolkit.Mvvm.Messaging;
using Zhally.Toolkit.DynamicGestures;

namespace Zhally.Toolkit.TreeView;

public partial class TreeView<T> : ScrollView where T : TreeNodeContent, new()
{

    private TreeNodeView<T>? root; // 泛型根节点视图
    public static volatile bool RequiredIconVisible = false;

    public TreeView()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Default;

        // 订阅泛型消息
        WeakReferenceMessenger.Default.Register(this, (MessageHandler<object, TreeNodeFocusChangedMessage<T>>)((r, m) =>
        {
            if (Primogenitor == m.Value.Primogenitor && this.Current != m.Value)
            {
                this.Current = m.Value;
                _ = WeakReferenceMessenger.Default.Send<TreeViewFocusChangedMessage<T>>(new(Current));
                root?.Render();
            }
        }));

        WeakReferenceMessenger.Default.Register<TreeRenderRequestMessage<T>>(this, (r, m) =>
        {
            if (Primogenitor == m.Value.Primogenitor)
            {
                if (Primogenitor == m.Value)
                {
                    Content = null;
                    root = TreeNodeView<T>.CreateTreeViewNode(m.Value); // 创建泛型根节点
                    Content = root;
                }
                else
                {
                    root?.Render();
                }
            }
        });

        WeakReferenceMessenger.Default.Register<IDragDropMessage>(this, (r, m) =>
        {
            if (m.TargetPayload.View is TreeNodeView<T> targetTreeNodeView
                && targetTreeNodeView.Context.Primogenitor == this.Primogenitor)
            {
                if (m.SourcePayload.View is TreeNodeView<T> sourceTreeNodeView
                    && sourceTreeNodeView.Context.Primogenitor == this.Primogenitor
                    && sourceTreeNodeView != targetTreeNodeView)
                {
                    // 针对同一颗树不同节点的移动操作。
                    // 将 source 移动到 target 之后，然后发送业已移动消息。可对 source / target 后续处理， 如：
                    // 删除 source (即删除）
                    // 删除 target （替换) 
                    // 将 source 的内容拷贝到 target 然后删除 （合并）
                    var source = sourceTreeNodeView.Context;
                    var target = targetTreeNodeView.Context;
                    if (MoveOnDrag)
                    {
                        if (source.Parent?.Children.Remove(source) ?? false)
                        {
                            if (this.Current == source)
                            {
                                this.Current = null;
                            }
                        }

                        var targetParent = target.Parent ?? target.Primogenitor;
                        int index = targetParent.Children.IndexOf(target);
                        if (index != -1)
                        {
                            targetParent.Children.Insert(index + 1, source);
                            _ = WeakReferenceMessenger.Default.Send(new TreeNodeFocusChangedMessage<T>(source));   // 移动后的 source 为当前 focus 节点
                        }

                        var innerMessage = new TreeViewInnerDragDropMessage<T>(sourceTreeNodeView.Context, targetTreeNodeView.Context);
                        _ = WeakReferenceMessenger.Default.Send(innerMessage);
                    }
                }
                else
                {
                    _ = WeakReferenceMessenger.Default.Send(new TreeViewDroppedMessage<T>(m.SourcePayload, targetTreeNodeView.Context));
                }
            }
        });
    }

    // 泛型 Primogenitor 属性
    public TreeNode<T> Primogenitor
    {
        get => (TreeNode<T>)GetValue(PrimogenitorProperty);
        set => SetValue(PrimogenitorProperty, value);
    }
    public static readonly BindableProperty PrimogenitorProperty = BindableProperty.Create(nameof(Primogenitor), typeof(TreeNode<T>), typeof(TreeView<T>),
                                defaultValue: new TreeNode<T>
                                {
                                    Content = new T()
                                    {
                                        ID = TreeNodeContent.PrimogenitorID,
                                        Title = TreeNodeContent.PrimogenitorKey
                                    }
                                },
                                propertyChanged: (BindableProperty.BindingPropertyChangedDelegate)((bindable, old, newValue) =>
                                {
                                    if (bindable is TreeView<T> @this && newValue is TreeNode<T> context)
                                    {
                                        @this.root = TreeNodeView<T>.CreateTreeViewNode(context);
                                        @this.Content = @this.root;
                                        @this.Current = context;
                                        _ = WeakReferenceMessenger.Default.Send(new RequiredIconShownChangedMessage<T>(@this));
                                    }
                                }));

    public TreeNode<T>? Current
    {
        get => (TreeNode<T>)GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }
    public static readonly BindableProperty CurrentProperty = BindableProperty.Create(nameof(Current), typeof(TreeNode<T>), typeof(TreeView<T>), defaultValue: null);

    public bool RequiredIconShown
    {
        get => (bool)GetValue(RequiredIconShownProperty);
        set => SetValue(RequiredIconShownProperty, value);
    }
    public static readonly BindableProperty RequiredIconShownProperty = BindableProperty.Create(nameof(RequiredIconShown), typeof(bool), typeof(TreeView<T>),
                                defaultValue: true,
                                propertyChanged: (BindableProperty.BindingPropertyChangedDelegate)((bindable, old, newValue) =>
                                {
                                    if (bindable is TreeView<T> @this && newValue is bool shown)
                                    {
                                        TreeView<T>.RequiredIconVisible = shown;
                                        _ = WeakReferenceMessenger.Default.Send(new RequiredIconShownChangedMessage<T>(@this));
                                        @this.root?.Render();
                                    }
                                }));
    public bool MoveOnDrag
    {
        get => (bool)GetValue(MoveOnDragProperty);
        set => SetValue(MoveOnDragProperty, value);
    }
    public static readonly BindableProperty MoveOnDragProperty = BindableProperty.Create(nameof(MoveOnDrag), typeof(bool), typeof(TreeView<T>), defaultValue: true);
}