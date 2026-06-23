using UnityEngine;

public static class BubbleTimeBonusUtility
{
    public static void TryGiveTime(Component bubbleComponent, Collider2D other)
    {
        if (bubbleComponent == null || other == null) return;

        // 只要该泡泡或其父物体上挂了 BubbleTimeBonus，
        // 就表示“这类泡泡会刷新时间”
        BubbleTimeBonus bonus = bubbleComponent.GetComponentInParent<BubbleTimeBonus>();
        if (bonus == null) return;

        PlayerCountdownTimer timer = other.GetComponentInParent<PlayerCountdownTimer>();
        if (timer == null) return;

        // 直接回满
        timer.ResetTimer();
    }
}