using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace RuProxyDEPO
{
    [BepInPlugin("ru.makorddev.ruproxydepo", "RuProxyDEPO", "1.0.0")]
    public class RuProxyDEPO : BaseUnityPlugin
    {
        public static ConfigEntry<string> ServerUrl;

        private void Awake()
        {
            ServerUrl = Config.Bind(
                "Network", 
                "ServerUrl", 
                "https://depo.makordikr.ru", 
                "URL of proxy server."
            );
            
            Harmony harmony = new Harmony("ru.makorddev.ruproxydepo");
            harmony.PatchAll();
            
            Logger.LogInfo("RuProxyDEPO loaded.");
        }
    }

    [HarmonyPatch(typeof(WSManager), "Conectar")]
    public static class Patch_WSManager_Conectar
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr && (string)instruction.operand == "https://api.rombykon.com")
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_WSManager_Conectar), nameof(GetTargetUrl)));
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        public static string GetTargetUrl()
        {
            return RuProxyDEPO.ServerUrl.Value;
        }
    }
}