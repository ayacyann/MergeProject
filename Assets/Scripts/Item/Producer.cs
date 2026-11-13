using System.Collections;
using UnityEngine;

/// <summary>
/// 普通生产者：点击生产会消耗一次次数，次数降到 0 后进入冷却，
/// 冷却时长取该等级的 ProduceInfo.timesInterval，结束后把次数补满。
/// </summary>
public class Producer : BaseProducer
{
    // 冷却结束补满次数的协程
    private Coroutine recoverRoutine;

    protected override void OnTimesExhausted()
    {
        // 次数耗尽：按配置的等待时长进入冷却，结束后恢复次数
        float interval = GetCooldownSeconds();
        if (interval <= 0f)
            return;

        ItemCooldown cooldown = GetComponent<ItemCooldown>();
        if (cooldown != null)
            cooldown.StartCooldown(interval);

        if (recoverRoutine != null)
            StopCoroutine(recoverRoutine);
        recoverRoutine = StartCoroutine(RecoverAfterCooldown(interval));
    }

    public override void SetDefinitionInfo(MergeItemDefinition mid, MergeChain mc)
    {
        base.SetDefinitionInfo(mid, mc);

        // 换成新定义（合并升级、对象池复用）后旧冷却作废
        CancelCooldown();
    }

    private IEnumerator RecoverAfterCooldown(float interval)
    {
        yield return new WaitForSeconds(interval);

        recoverRoutine = null;

        // 冷却结束时物品可能已经换了定义，只在还有定义时恢复次数
        if (MID != null && ProducerMC != null)
            currentTimes = ProducerMC.GetMaxProduceInfo(MID.tier);
    }

    private void CancelCooldown()
    {
        if (recoverRoutine != null)
        {
            StopCoroutine(recoverRoutine);
            recoverRoutine = null;
        }

        ItemCooldown cooldown = GetComponent<ItemCooldown>();
        if (cooldown != null)
            cooldown.ResetCooldown();
    }

    private void OnDisable()
    {
        // 回收/隐藏时协程会停掉，别留下失效的句柄
        recoverRoutine = null;
    }

    private float GetCooldownSeconds()
    {
        if (MID == null || ProducerMC == null)
            return 0f;

        ProduceInfo info = ProducerMC.GetProduceInfo(MID.tier);
        return info != null ? info.timesInterval : 0f;
    }
}
