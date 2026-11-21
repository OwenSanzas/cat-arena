using UnityEngine;

// 猫猫外观：按部位记录渲染器，运行时可以换毛色；也提供挂护甲的骨骼点
public class CatSkin : MonoBehaviour
{
    public Renderer[] fur, belly, accent, eye, scarf;
    public Transform head;
    public Transform[] feet;

    public void Apply(Color furC, Color bellyC, Color accentC, Color eyeC, Color scarfC)
    {
        Set(fur, furC); Set(belly, bellyC); Set(accent, accentC); Set(eye, eyeC); Set(scarf, scarfC);
    }

    static void Set(Renderer[] rs, Color c)
    {
        if (rs == null) return;
        var m = WeaponModel.Lit(c);
        foreach (var r in rs) if (r) r.sharedMaterial = m;
    }
}
