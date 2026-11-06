using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace RuProxyDEPO
{
    [BepInPlugin("ru.makorddev.ruproxydepo", "RuProxyDEPO", "1.2.0")]
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

            PatchAllOnlineTargets(harmony);

            Logger.LogInfo("RuProxyDEPO loaded.");
        }

        private void PatchAllOnlineTargets(Harmony harmony)
        {
            var transpilerMethod = AccessTools.Method(typeof(UrlTranspiler), nameof(UrlTranspiler.Transpile));
            var transpilerHarmonyMethod = new HarmonyMethod(transpilerMethod);

            (string parentTypeName, string nestedTypeName, string methodName)[] targets = new[]
            {
                ("WSManager", (string)null, "CrearSocket"),
                ("ManagerRankingCoronas", "<ObtenerCoronas>d__13", "MoveNext"),
                ("ManagerRankingCoronas", "<ObtenerRankingsOnline>d__14", "MoveNext"),
                ("ManagerEggHunt", "<ObtenerHuevos>d__147", "MoveNext"),
                ("ManagerEggHunt", "<EnviarHuevo>d__149", "MoveNext"),
                ("UnityStandardAssets.Characters.FirstPerson.FirstPersonController", "<ObtenerMuertesOnline>d__236", "MoveNext"),
                ("UnityStandardAssets.Characters.FirstPerson.FirstPersonController", "<EnviarMuerteOnline>d__237", "MoveNext"),
                ("Contrarreloj", "<ObtenerRankingsOnline>d__54", "MoveNext"),
                ("Contrarreloj", "<EnviarRankingsOnline>d__55", "MoveNext"),
                ("ManagerEscribirNotas", "<EnviarComentarioOnline>d__21", "MoveNext"),
                ("ManagerLeerEscribirNotas", "<ObtenerComentariosOnline>d__20", "MoveNext")
            };

            var gameAssembly = typeof(WSManager).Assembly;

            foreach (var target in targets)
            {
                try
                {
                    Type targetType = target.nestedTypeName == null
                        ? gameAssembly.GetType(target.parentTypeName)
                        : gameAssembly.GetType(target.parentTypeName + "+" + target.nestedTypeName);

                    if (targetType == null)
                    {
                        Logger.LogWarning($"Target type not found: {target.parentTypeName}" + (target.nestedTypeName != null ? "+" + target.nestedTypeName : ""));
                        continue;
                    }

                    MethodInfo targetMethod = AccessTools.Method(targetType, target.methodName);
                    if (targetMethod == null)
                    {
                        Logger.LogWarning($"Target method not found: {targetType.FullName}.{target.methodName}");
                        continue;
                    }

                    harmony.Patch(targetMethod, transpiler: transpilerHarmonyMethod);
                    Logger.LogInfo($"Patched {targetType.FullName}.{target.methodName}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error patching {target.parentTypeName}: {ex.Message}");
                }
            }

            PatchAsyncByMethodName(harmony, transpilerHarmonyMethod, gameAssembly, "WSManagerMenus", "ConectarConSesion");
            PatchAsyncByMethodName(harmony, transpilerHarmonyMethod, gameAssembly, "ServidorSesion", "IniciarSesion");
        }

        private void PatchAsyncByMethodName(Harmony harmony, HarmonyMethod transpiler, Assembly asm, string typeName, string methodName)
        {
            try
            {
                Type parent = asm.GetType(typeName);
                if (parent == null)
                {
                    Logger.LogWarning($"Parent type not found: {typeName}");
                    return;
                }

                string pattern = $"<{methodName}>d__";
                Type stateMachine = parent.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
                                         .FirstOrDefault(t => t.Name.StartsWith(pattern));

                if (stateMachine == null)
                {
                    Logger.LogWarning($"State machine not found for {typeName}.{methodName}");
                    return;
                }

                MethodInfo moveNext = AccessTools.Method(stateMachine, "MoveNext");
                if (moveNext == null)
                {
                    Logger.LogWarning($"MoveNext not found in {stateMachine.FullName}");
                    return;
                }

                harmony.Patch(moveNext, transpiler: transpiler);
                Logger.LogInfo($"Patched {stateMachine.FullName}.MoveNext");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error patching {typeName}.{methodName}: {ex.Message}");
            }
        }
    }

    public static class UrlTranspiler
    {
        public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            var replaceMethod = AccessTools.Method(typeof(UrlTranspiler), nameof(ReplaceUrl));
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string str && str.Contains("api.rombykon.com"))
                {
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Call, replaceMethod);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        public static string ReplaceUrl(string original)
        {
            if (string.IsNullOrEmpty(original))
            {
                return original;
            }

            return original.Replace("https://api.rombykon.com", RuProxyDEPO.ServerUrl.Value);
        }
    }
}
