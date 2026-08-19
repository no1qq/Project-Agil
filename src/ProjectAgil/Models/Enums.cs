namespace ProjectAgil.Models;

public enum TweakCategory
{
    TcpStack,
    Interface,
    Adapter,
    System,
    Gaming,
    Advanced,
}

public enum TweakRisk
{
    Safe,
    Moderate,
    Advanced,
}

public enum TweakStatus
{
    Unknown,
    Optimized,
    NotOptimized,
    Unsupported,
}

public enum RegistryScope
{
    Machine,
    User,
    Interface,
    AdapterClass,
}

public enum TuningLevel
{
    Disabled,
    HighlyRestricted,
    Restricted,
    Normal,
    Experimental,
}

public enum ConnectionType
{
    Fiber,
    Cable,
    Dsl,
    Wifi,
    Mobile,
    Satellite,
}

public static class EnumLabels
{
    public static string ToDisplay(this TweakCategory value) =>
        value switch
        {
            TweakCategory.TcpStack => "TCP stack",
            TweakCategory.Interface => "Interface",
            TweakCategory.Adapter => "Network adapter",
            TweakCategory.System => "System",
            TweakCategory.Gaming => "Gaming",
            TweakCategory.Advanced => "Advanced",
            _ => value.ToString(),
        };

    public static string ToDisplay(this TuningLevel value) =>
        value switch
        {
            TuningLevel.Disabled => "Disabled",
            TuningLevel.HighlyRestricted => "Highly restricted",
            TuningLevel.Restricted => "Restricted",
            TuningLevel.Normal => "Normal",
            TuningLevel.Experimental => "Experimental",
            _ => value.ToString(),
        };

    public static string ToNetshValue(this TuningLevel value) =>
        value switch
        {
            TuningLevel.Disabled => "Disabled",
            TuningLevel.HighlyRestricted => "HighlyRestricted",
            TuningLevel.Restricted => "Restricted",
            TuningLevel.Normal => "Normal",
            TuningLevel.Experimental => "Experimental",
            _ => "Normal",
        };

    public static string ToDisplay(this ConnectionType value) =>
        value switch
        {
            ConnectionType.Fiber => "Fiber",
            ConnectionType.Cable => "Cable / DOCSIS",
            ConnectionType.Dsl => "DSL / PPPoE",
            ConnectionType.Wifi => "Wi-Fi",
            ConnectionType.Mobile => "Mobile / 4G / 5G",
            ConnectionType.Satellite => "Satellite",
            _ => value.ToString(),
        };

    public static string ToDisplay(this TweakRisk value) =>
        value switch
        {
            TweakRisk.Safe => "Safe",
            TweakRisk.Moderate => "Moderate",
            TweakRisk.Advanced => "Advanced",
            _ => value.ToString(),
        };
}
