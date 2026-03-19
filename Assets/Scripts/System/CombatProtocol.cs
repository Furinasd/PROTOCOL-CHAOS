using UnityEngine;

// ==========================================
// Title: 战斗交互协议层 (Combat Protocol)
// Description: 全局唯一的数据契约。所有战斗相关脚本应依赖此处定义。
//              注意：保留 PolarityColor 枚举的别名，以兼容 PlayerPolarity.cs 的现有接口。
// ==========================================

#region 极性枚举 (全局通用)

/// <summary>
/// 统一极性枚举。取代分散在各处的 PolarityColor。
/// PlayerPolarity.cs 保持原样（已有同义 PolarityColor），EnemyHitbox 等新代码改用本枚举。
/// </summary>
public enum Polarity
{
    Blue,    // 绝对零度 / 秩序蓝
    Red,     // 绝对炽热 / 混沌红
    Neutral  // 无极性（特殊攻击，无法被同色吸收）
}

#endregion

#region 攻击数据包 (Attack Data Packet)

/// <summary>
/// 一次攻击的完整数据包：由攻击发起者填充，传递给受击目标的 TakeDamage 方法。
/// </summary>
public struct AttackData
{
    /// <summary>普通生命值伤害</summary>
    public float damage;
    
    /// <summary>熵值/躯干伤害，打满后触发可处决状态</summary>
    public float postureDamage;
    
    /// <summary>攻击的极性：决定"同色吸收"或"异色受伤"</summary>
    public Polarity polarity;
    
    /// <summary>攻击来源的世界坐标，用于计算击退</summary>
    public Vector3 sourcePosition;
    
    /// <summary>精确击飞方向（标准化向量），由判定器计算产生</summary>
    public Vector3 hitDirection;
    
    /// <summary>攻击来源对象，用于弹刀成功后对攻击者施加反馈</summary>
    public GameObject sourceObject;
}

#endregion

#region 受击接口 (IDamageable)

/// <summary>
/// 统一受击接口。玩家、敌人中的受击体都应实现此接口。
/// 返回值：true = 触发了"完美弹刀"，攻击者需要进入大硬直；false = 普通受击或处理。
/// </summary>
public interface IDamageable
{
    bool TakeDamage(AttackData attackData);
}

#endregion

#region 辅助工具 (Protocol Utilities)

/// <summary>
/// 提供 PolarityColor (旧枚举) 与 Polarity (新枚举) 之间的转换桥接。
/// 确保 PlayerPolarity.cs 等现有脚本无需修改即可与新系统协同工作。
/// </summary>
public static class PolarityBridge
{
    public static Polarity FromPolarityColor(PolarityColor color)
    {
        return color == PolarityColor.Red ? Polarity.Red : Polarity.Blue;
    }

    public static PolarityColor ToPolarityColor(Polarity polarity)
    {
        return polarity == Polarity.Red ? PolarityColor.Red : PolarityColor.Blue;
    }
}

#endregion
