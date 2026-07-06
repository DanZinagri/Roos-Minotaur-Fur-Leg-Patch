using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace danzinagri.femalebodyvariantslegspatch
{
    [StaticConstructorOnStartup]
    public static class ModStartup
    {
        static ModStartup()
        {
            var harmony = new Harmony("danzinagri.femalebodyvariantslegspatch");
            harmony.PatchAll();
        }
    }

    // Roo's Minotaur Xenotype draws basically a 2nd layer of the same body to do that 2-tone design. But if you're using female varients you'll
    // end up with male body drawn over the female because it doens't known to look for the female varients.
    // So small patch to check.
    [HarmonyPatch(typeof(PawnRenderNode_Fur), "GraphicFor")]
    public static class PawnRenderNode_Fur_GraphicFor_FemaleMaskPatch
    {
        public static void Postfix(Pawn pawn, ref Graphic __result)
        {
            if (__result == null || pawn?.story?.bodyType == null || pawn.gender == Gender.Male)
                return;

            if (pawn.story.furDef?.defName != "RBM_UnguligradeLegs")
                return;

            string bodyPath = pawn.story.bodyType.bodyNakedGraphicPath;
            if (bodyPath == null || bodyPath.Contains("_Female"))
                return;

            if (!bodyPath.Contains("_Thin") && !bodyPath.Contains("_Fat") && !bodyPath.Contains("_Hulk"))
                return;

            string femaleBodyPath = bodyPath + "_Female";
            if (ContentFinder<Texture2D>.Get(femaleBodyPath + "_south", false) == null)
                return;

            __result = GraphicDatabase.Get<Graphic_Multi>(
                __result.path,
                __result.Shader,
                __result.drawSize,
                __result.color,
                __result.colorTwo,
                __result.data,
                femaleBodyPath);
        }
    }
}
