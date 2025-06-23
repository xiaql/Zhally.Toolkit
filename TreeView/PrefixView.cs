using CommunityToolkit.Mvvm.Messaging;

namespace Zhally.Toolkit.TreeView;

public partial class ImageCache : Image
{
    private static readonly Dictionary<string, ImageSource> _cache = [];

    public static ImageSource ImageSourceFromCache(string sourcePath)
    {
        if (_cache.TryGetValue(sourcePath, out var source))
        {
            return source;
        }

        var newSource = ImageSource.FromFile(sourcePath);
        _cache[sourcePath] = newSource;
        return newSource;
    }

    public static readonly BindableProperty SourceCacheProperty = BindableProperty.Create(nameof(SourceCache), typeof(string), typeof(ImageCache), defaultValue: null, propertyChanged: OnSourceCacheChanged);

    private static void OnSourceCacheChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (newValue is string resourcePath && bindable is ImageCache imageCache)
        {
            imageCache.Source = ImageSourceFromCache(resourcePath);
        }
    }

    public string SourceCache
    {
        get => (string)GetValue(SourceCacheProperty);
        set => SetValue(SourceCacheProperty, value);
    }
}

public partial class PrefixView<T> : ContentView where T : TreeNodeContent, new()
{
    private readonly ImageCache expandIcon;
    private readonly ImageCache checkIcon;

    public PrefixView()
    {
        expandIcon = new()
        {
            HeightRequest = 16,
            WidthRequest = 16,
            Margin = new Thickness(0, 0, 8, 0)
        };
        checkIcon = new()
        {
            HeightRequest = 16,
            WidthRequest = 16,
        };
        Content = new HorizontalStackLayout() { expandIcon, checkIcon };

        expandIcon.GestureRecognizers.Add(new TapGestureRecognizer()
        {
            NumberOfTapsRequired = 1,
            Command = new Command(() =>
            {
                if (BindingContext is TreeNode<T> context && !context.IsLeaf)
                {
                    context.IsExpanded = !context.IsExpanded;
                    OnExpandIconChanged(ref context);
                    _ = WeakReferenceMessenger.Default.Send(new TreeRenderRequestMessage<T>(context));
                }
            })
        });

        checkIcon.GestureRecognizers.Add(new TapGestureRecognizer()
        {
            NumberOfTapsRequired = 1,
            Command = new Command(() =>
            {
                if (BindingContext is TreeNode<T> context)
                {
                    context.Required = !context.Required;
                    OnCheckIconChanged(ref context);
                    if (context.IsLeaf)
                    {
                        _ = WeakReferenceMessenger.Default.Send(new TreeRenderRequestMessage<T>(context));
                    }
                }
            })
        });

        WeakReferenceMessenger.Default.Register<RequiredIconShownChangedMessage<T>>(this, (r, m) =>
        {
            if (m.Value.Primogenitor == (this.BindingContext as TreeNode<T>)?.Primogenitor)
            {
                checkIcon.IsVisible = m.Value.RequiredIconShown;
            }
        });
    }

    private void OnExpandIconChanged(ref TreeNode<T> context)
    {
        expandIcon.SourceCache = context switch
        {
            // 1. 首先检查是否为根节点（Primogenitor指向自身）
            { Primogenitor: var p } when p == context => "database.png",

            // 2. 其次检查是否为叶节点且内容为空
            { IsLeaf: true, Content: null } => "leafempty.png",

            // 3. 然后检查是否为普通叶节点
            { IsLeaf: true } => "leaf.png",

            // 4. 接着检查是否为展开的非叶节点
            { IsExpanded: true } => "bagminus.png",

            // 5. 再检查是否为空的非叶节点
            { IsEmpty: true } => "bagempty.png",

            // 6. 最后是默认情况（非空、未展开的非叶节点）
            _ => "bagplus.png"
        };
        expandIcon.Opacity = context.IsEmpty ? 0.8D : 1.0D;
    }

    private void OnCheckIconChanged(ref TreeNode<T> context)
    {
        if (!TreeView<T>.RequiredIconVisible)
        {
            checkIcon.IsVisible = false;
            return;
        }

        checkIcon.IsVisible = true;
        checkIcon.SourceCache = context switch
        {
            // 叶节点
            { IsLeaf: true, Required: true } => "checksquare.png",
            { IsLeaf: true, Required: false } => "square.png",

            // 非叶节点
            { IsLeaf: false } when !context.Children.Any(c => c.Required) => "square.png",
            { IsLeaf: false } when context.Children.All(c => c.Required) => "checksquare.png",
            { IsLeaf: false } => "checkdash.png",

            // 默认情况（不会触发）
            _ => throw new InvalidOperationException("Unexpected context state")
        };
        checkIcon.Opacity = context.IsEmpty ? 0.8D : 1.0D;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is TreeNode<T> context)
        {
            OnExpandIconChanged(ref context);
            OnCheckIconChanged(ref context);
            this.Margin = new Thickness(context.Depth * 30, 0, 8, 0);
        }
    }

    internal void Refresh()
    {
        OnBindingContextChanged();
    }
}