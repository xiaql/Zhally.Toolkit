using Zhally.Toolkit.DynamicGestures;

namespace Zhally.Toolkit.TreeView;

public class TreeViewInnerDragDropMessage<T>(TreeNode<T> source, TreeNode<T> target) where T : TreeNodeContent,new()
{
    public TreeNode<T> Source { get; } = source;
    public TreeNode<T> Target { get; } = target;
}

public class TreeViewDroppedMessage<T>(IDragDropPayload sourcePayload, TreeNode<T> target) where T : TreeNodeContent,new()
{
    public IDragDropPayload SourcePayload { get; } = sourcePayload;
    public TreeNode<T> Target { get; } = target;
}