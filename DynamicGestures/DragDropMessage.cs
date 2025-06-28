namespace Zhally.Toolkit.DynamicGestures;

public class DragDropPayload
{
    public required View View { get; init; }                        // 拖放源/目标控件
    public object? Affix { get; init; }                             // 任意附加数据（如文本、对象）
    public Action<View, object?>? Callback { get; init; }           // 拖放完成后的回调
    public View? Anchor { get; set; } = null;                       // 拖放源/目标控件的 recognizer 依附 View 组件

    public SourceTypeEnum SourceType { get; set; }                  // 标识。源/目标之间标识有交集者才能交互

}

public sealed class DragDropMessage
{
    public required DragDropPayload SourcePayload { get; init; }
    public required DragDropPayload TargetPayload { get; init; }

    public DropActions DropActions { get; init; }
}
public enum DropActions { None, Append, Merge, Add, Move, Delete }

[Flags]
public enum SourceTypeEnum : int
{
    None = 0,
    Reserved_1 = 1 << 0,  // 2^0 = 1
    Reserved_2 = 1 << 1,  // 2^1 = 2
    Reserved_3 = 1 << 2,  // 2^2 = 4
    Reserved_4 = 1 << 3,  // 2^3 = 8
    Reserved_5 = 1 << 4,  // 2^4 = 16
    Reserved_6 = 1 << 5,  // 2^5 = 32
    Reserved_7 = 1 << 6,  // 2^6 = 64
    Common_0 = 1 << 7,    // 2^7 = 128
    Common_1 = 1 << 8,    // 2^8 = 256
    Common_2 = 1 << 9,    // 2^9 = 512
    Common_3 = 1 << 10,   // 2^10 = 1024
    Common_4 = 1 << 11,   // 2^11 = 2048
    Common_5 = 1 << 12,   // 2^12 = 4096
    Common_6 = 1 << 13,   // 2^13 = 8192
    Common_7 = 1 << 14,   // 2^14 = 16384
    Special_0 = 1 << 15,  // 2^15 = 32768
    Special_1 = 1 << 16,  // 2^16 = 65536
    Special_2 = 1 << 17,  // 2^17 = 131072
    Special_3 = 1 << 18,  // 2^18 = 262144
    Special_4 = 1 << 19,  // 2^19 = 524288
    Special_5 = 1 << 20,  // 2^20 = 1048576
    Special_6 = 1 << 21,  // 2^21 = 2097152
    Special_7 = 1 << 22,  // 2^22 = 4194304
    Extend_0 = 1 << 23,   // 2^23 = 8388608
    Extend_1 = 1 << 24,   // 2^24 = 16777216
    Extend_2 = 1 << 25,   // 2^25 = 33554432
    Extend_3 = 1 << 26,   // 2^26 = 67108864
    Extend_4 = 1 << 27,   // 2^27 = 134217728
    Extend_5 = 1 << 28,   // 2^28 = 268435456
    Extend_6 = 1 << 29,   // 2^29 = 536870912
    Extend_7 = 1 << 30,   // 2^30 = 1073741824

    Reserved = Reserved_1 | Reserved_2 | Reserved_3 | Reserved_4 | Reserved_5 | Reserved_6 | Reserved_7
}