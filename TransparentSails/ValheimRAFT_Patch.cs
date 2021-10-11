using HarmonyLib;
using ValheimRAFT;

namespace TransparentSails
{
    internal class ValheimRAFT_Patch
    {
        [HarmonyPatch(typeof(Ship), "UpdateSailSize")]
        [HarmonyPostfix]
        private static void Ship_UpdateSailSize(Ship __instance)
        {
            MoveableBaseShipComponent mb = __instance.GetComponent<MoveableBaseShipComponent>();
            if (!mb || !mb.m_baseRoot)
            {
                return;
            }
            for (int i = 0; i < mb.m_baseRoot.m_mastPieces.Count; i++)
            {
                MastComponent mast = mb.m_baseRoot.m_mastPieces[i];
                if (mast && mast.m_sailCloth && mast.m_sailObject)
                {
                    TransparentSailsMod.UpdateSail(mast.GetInstanceID(), TransparentSailsMod.ShouldBeTransparent(__instance, mast.m_sailCloth), mast.m_sailObject);
                }
            }
        }
    }
}