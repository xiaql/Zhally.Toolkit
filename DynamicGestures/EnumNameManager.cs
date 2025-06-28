using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Zhally.Toolkit.DynamicGestures;


[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
public sealed class Int32FlagsEnumConstraintAttribute : Attribute { }

public static class EnumNameManager<TEnum> where TEnum : struct, Enum
{
    static EnumNameManager()
    {
        ValidateEnumConstraints();
    }

    private static void ValidateEnumConstraints()
    {
        var enumType = typeof(TEnum);

        if (!enumType.GetTypeInfo().GetCustomAttributes(typeof(FlagsAttribute), false).Any())
        {
            throw new InvalidOperationException($"类型 {enumType.Name} 未标记为 [Flags] 属性");
        }

        if (Enum.GetUnderlyingType(enumType) != typeof(int))
        {
            throw new InvalidOperationException($"类型 {enumType.Name} 的基础类型不是 Int32");
        }
    }

    // 存储值 -> 名称 的映射关系
    private static readonly ConcurrentDictionary<int, string> _nameMappings = new();

    // 注册自定义名称
    public static void RegisterName(TEnum value, string name)
    {
        _nameMappings[(int)(object)value] = name;
    }

    // 批量注册自定义名称
    public static void RegisterNames(IDictionary<TEnum, string> nameMap)
    {
        foreach (var pair in nameMap)
        {
            _nameMappings[(int)(object)pair.Key] = pair.Value;
        }
    }

    // 获取枚举值的名称
    public static string GetName(TEnum value)
    {
        var intValue = (int)(object)value;

        // 首先检查是否有自定义名称
        if (_nameMappings.TryGetValue(intValue, out string? customName))
        {
            return customName;
        }

        // 检查是否有DescriptionAttribute
        var field = typeof(TEnum).GetField(value.ToString());
        if (field != null)
        {
            var description = field.GetCustomAttribute<DescriptionAttribute>();
            if (description != null)
            {
                return description.Description;
            }
        }

        // 回退到默认名称
        return value.ToString();
    }

    // 获取组合标志的名称
    public static string GetCombinedNames(TEnum value)
    {
        var intValue = (int)(object)value;
        var names = new List<string>();

        // 处理零值情况
        if (intValue == 0)
        {
            names.Add(GetName(value));
        }
        else
        {
            // 遍历所有可能的标志值
            foreach (TEnum flag in Enum.GetValues<TEnum>())
            {
                int flagValue = (int)(object)flag;

                // 跳过零值
                if (flagValue == 0) continue;

                // 检查是否包含当前标志
                if ((intValue & flagValue) == flagValue)
                {
                    names.Add(GetName(flag));
                }
            }
        }

        return string.Join(" | ", names);
    }
}

// 扩展方法 - 让枚举使用更方便
public static class EnumExtensions
{
    public static string GetDisplayName<TEnum>(this TEnum value) where TEnum : struct, Enum
    {
        return EnumNameManager<TEnum>.GetName(value);
    }

    public static string GetDisplayNames<TEnum>(this TEnum value) where TEnum : struct, Enum
    {
        return EnumNameManager<TEnum>.GetCombinedNames(value);
    }
}