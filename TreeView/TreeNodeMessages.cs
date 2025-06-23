using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Zhally.Toolkit.TreeView;


public class TreeViewFocusChangedMessage<T>(TreeNode<T> asFocus) : ValueChangedMessage<TreeNode<T>>(asFocus) where T : TreeNodeContent,new()
{
}

public class TreeNodeFocusChangedMessage<T>(TreeNode<T> asFocus) : ValueChangedMessage<TreeNode<T>>(asFocus) where T : TreeNodeContent,new()
{
}

public class TreeNodeContentInvalidatedMessage<T>(TreeNode<T> context) : ValueChangedMessage<TreeNode<T>>(context) where T : TreeNodeContent, new()
{
}

public class TreeRenderRequestMessage<T>(TreeNode<T> expanded) : ValueChangedMessage<TreeNode<T>>(expanded) where T : TreeNodeContent,new()
{
}
public class RequiredIconShownChangedMessage<T>(TreeView<T> expanded) : ValueChangedMessage<TreeView<T>>(expanded) where T : TreeNodeContent, new()
{
}

public class TreeNodeChildrenChangedMessage<T>(TreeNode<T> parent, ChildrenChangedDataPackage<T> arg) : ChildrenChangedDataPackage<T>(arg.Child, arg.Index, arg.AddOrViceVersa) where T : TreeNodeContent, new()
{
    public TreeNode<T> Parent { get; set; } = parent;
}

public class ChildrenChangedDataPackage<T>(TreeNode<T> item, Int32 index, bool addorviceversa) where T : TreeNodeContent, new()
{
    public TreeNode<T> Child { get; set; } = item;
    public Int32 Index { get; set; } = index;
    public bool AddOrViceVersa { get; set; } = addorviceversa;
}