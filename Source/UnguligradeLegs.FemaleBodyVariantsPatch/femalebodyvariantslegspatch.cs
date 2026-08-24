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

    [DefOf]
    public static class LegsFurDefOf
    {
        public static FurDef RBM_UnguligradeLegs;

        static LegsFurDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(LegsFurDefOf));
        }
    }

    // Vanilla stores exactly one fur per pawn (pawn.story.furDef), picked at random from every gene
    // with a <fur>, but each gene declaring a PawnRenderNode_Fur gets its own node. On a pawn with two
    // fur genes (minotaur legs + e.g. AG_DrakonoriBody) every node draws whichever fur won the roll.
    // Roo's Satyr DLL compounds it: while the legs gene is active its prefix hijacks *every* fur node
    // on the pawn and masks it to the body path. Result is the leg fur painted over the whole body.
    //
    // The node knows which gene owns it (PawnRenderNode.gene), so draw that gene's own fur instead.
    [HarmonyPatch(typeof(PawnRenderNode_Fur), "GraphicFor")]
    [HarmonyAfter("OskarPotocki.VEF")] // VEF's postfix also rebuilds from pawn.story.furDef
    public static class PawnRenderNode_Fur_GraphicFor_FemaleMaskPatch
    {
        public static void Postfix(PawnRenderNode_Fur __instance, Pawn pawn, ref Graphic __result)
        {
            if (__result == null || pawn?.story == null)
                return;

            FurDef ownFur = __instance.gene?.def?.fur;
            if (ownFur == null)
                return; // not a gene-owned fur node, leave it alone

            string ownPath = ownFur.GetFurBodyGraphicPath(pawn);
            if (ownPath.NullOrEmpty())
                return; // this fur has no texture for the pawn's body type

            string maskPath;
            if (ownFur == LegsFurDefOf.RBM_UnguligradeLegs)
            {
                // Our node. Keep Roo's body mask, but resolve the female variant when one exists.
                maskPath = BodyMaskPathFor(pawn);
            }
            else
            {
                // Someone else's node. Only touch it when the legs gene is what broke it.
                if (!HasActiveLegsGene(pawn))
                    return;

                maskPath = __result.maskPath;
                if (maskPath == pawn.story.bodyType?.bodyNakedGraphicPath)
                    maskPath = null; // undo the body mask Roo's prefix forced onto this node
            }

            if (__result.path == ownPath && __result.maskPath == maskPath)
                return; // already correct, nothing hijacked it

            __result = GraphicDatabase.Get<Graphic_Multi>(
                ownPath,
                __result.Shader,
                __result.drawSize,
                __result.color,
                __result.colorTwo,
                __result.data,
                maskPath);
        }

        private static bool HasActiveLegsGene(Pawn pawn)
        {
            var genes = pawn.genes?.GenesListForReading;
            if (genes == null)
                return false;

            for (int i = 0; i < genes.Count; i++)
            {
                if (genes[i].Active && genes[i].def.fur == LegsFurDefOf.RBM_UnguligradeLegs)
                    return true;
            }
            return false;
        }

        // The leg overlay must be masked against the same silhouette the body is actually drawn
        // with. Several mods replace that texture by postfixing PawnRenderNode_Body.GraphicFor:
        // FemaleBodyVariants swaps in "<path>_Female", Sized Apparel (OTYOTY.SizedApparel) swaps in
        // "<path>_BaseBodyF"/"_BaseBodyM", optionally suffixed with a body variation and with a
        // "/CustomPose/<pose>" segment spliced in. Rather than reimplement each mod's naming, read
        // back whatever the body node resolved - the Body node is this fur node's parent, so it is
        // always initialised first.
        private static string BodyMaskPathFor(Pawn pawn)
        {
            string drawnBodyPath = pawn.Drawer?.renderer?.renderTree?.BodyGraphic?.path;
            if (!drawnBodyPath.NullOrEmpty()
                && ContentFinder<Texture2D>.Get(drawnBodyPath + "_south", false) != null)
                return drawnBodyPath;

            // Body node not resolved yet, or its texture is missing. Fall back to resolving the
            // FemaleBodyVariants path by hand.
            string bodyPath = pawn.story.bodyType?.bodyNakedGraphicPath;
            if (bodyPath == null || pawn.gender == Gender.Male || bodyPath.Contains("_Female"))
                return bodyPath;

            if (!bodyPath.Contains("_Thin") && !bodyPath.Contains("_Fat") && !bodyPath.Contains("_Hulk"))
                return bodyPath;

            string femaleBodyPath = bodyPath + "_Female";
            if (ContentFinder<Texture2D>.Get(femaleBodyPath + "_south", false) == null)
                return bodyPath;

            return femaleBodyPath;
        }
    }
}
