using System.Reflection;
using System.Text;
using Hacknet;
using Pathfinder.Event;
using Pathfinder.Event.Loading;
using Pathfinder.Event.Gameplay;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using HarmonyLib;
using Mono.Cecil.Cil;
using Pathfinder.Util;

namespace Pathfinder.Executable;

[HarmonyPatch]
public static class ExecutableManager
{
    private struct CustomExeInfo
    {
        public string ExeData;
        public string XmlId;
        public Type ExeType;
    }

    private static readonly List<CustomExeInfo> CustomExes = [];
        
    static ExecutableManager()
    {
        EventManager<TextReplaceEvent>.AddHandler(GetTextReplacementExe);
        EventManager<ExecutableExecuteEvent>.AddHandler(OnExeExecute);
        EventManager<ExecutableListEvent>.AddHandler(OnExecutableList);
        EventManager.onPluginUnload += OnPluginUnload;
    }

    private static void GetTextReplacementExe(TextReplaceEvent e)
    {
        var exe = CustomExes.FirstOrNull(x => x.XmlId == e.Original);
        if (!exe.HasValue)
            return;
        e.Replacement = exe.Value.ExeData;
    }
    private static void OnExeExecute(ExecutableExecuteEvent e)
    {
        if (e.Result != ExecutionResult.NotFound)
            return;

        var exe = CustomExes.FirstOrNull(x => x.ExeData == e.ExecutableData);
        if (!exe.HasValue)
            return;
        var location = new Rectangle(e.OS.ram.bounds.X, e.OS.ram.bounds.Y + RamModule.contentStartOffset, RamModule.MODULE_WIDTH, (int)OS.EXE_MODULE_HEIGHT);
        if(exe.Value.ExeType.IsSubclassOf(typeof(GameExecutable)))
            e.OS.AddGameExecutable(
                (GameExecutable)Activator.CreateInstance(exe.Value.ExeType, null),
                location,
                e.Arguments.ToArray()
            );
        else
            e.OS.addExe((BaseExecutable)Activator.CreateInstance(exe.Value.ExeType, [location, e.OS, e.Arguments.ToArray()]));
        e.Result = ExecutionResult.StartupSuccess;
    }

    private static void OnPluginUnload(Assembly pluginAsm)
    {
        CustomExes.RemoveAll(x => x.ExeType.Assembly == pluginAsm);
    }

    public static void RegisterExecutable<T>(string xmlName) where T : BaseExecutable => RegisterExecutable(typeof(T), xmlName);
    public static void RegisterExecutable(Type executableType, string xmlName)
    {
        executableType.ThrowNotInherit<BaseExecutable>(nameof(executableType));
        var builder = new StringBuilder();
        foreach (var exeByte in Encoding.ASCII.GetBytes("PathfinderExe:" + executableType.FullName))
            builder.Append(Convert.ToString(exeByte, 2));
        CustomExes.Add(new CustomExeInfo
        {
            ExeData = builder.ToString(),
            XmlId = xmlName,
            ExeType = executableType
        });
    }

    public static bool IsXmlId(string xmlName) =>
        CustomExes.Any(x => x.XmlId == xmlName);

    public static bool IsExeData(string exeData) =>
        CustomExes.Any(x => x.ExeData == exeData);

    public static bool IsRegistered<T>() where T: BaseExecutable =>
        IsRegistered(typeof(T));
    public static bool IsRegistered(Type exeType) =>
        CustomExes.Any(x => x.ExeType == exeType);

    public static string GetCustomExeData(string xmlName) => CustomExes.FirstOrNull(x => x.XmlId == xmlName)?.ExeData;

    public static void UnregisterExecutable(string xmlName)
    {
        CustomExes.RemoveAll(x => x.XmlId == xmlName);
    }
    public static void UnregisterExecutable<T>() => UnregisterExecutable(typeof(T));
    public static void UnregisterExecutable(Type exeType)
    {
        CustomExes.RemoveAll(x => x.ExeType == exeType);
    }

    public static void AddGameExecutable(this OS os, GameExecutable exe, Rectangle location, string[] args)
    {
        exe.Assign(location, os, args);
        os.AddGameExecutable(exe);
    }
    public static void AddGameExecutable(this OS os, GameExecutable exe)
    {
        var computer = os.connectedComp ?? os.thisComputer;
        try
        {
            if(exe.needsProxyAccess && computer.proxyActive)
            {
                exe.OnProxyBypassFailure();
                if(!exe.IgnoreProxyFailPrint)
                    os.write(LocaleTerms.Loc("Proxy Active -- Cannot Execute"));
                return;
            }

            if(os.ramAvaliable >= exe.ramCost)
            {
                exe.OnInitialize();
                if(exe.CanAddToSystem)
                    os.exes.Add(exe);
                return;
            }

            exe.OnNoAvailableRam();
            if(!exe.IgnoreMemoryBehaviorPrint)
            {
                os.ram.FlashMemoryWarning();
                os.write(LocaleTerms.Loc("Insufficient Memory"));
            }
        }
        catch(Exception e)
        {
            if(!exe.CatchException(e))
                throw e;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(OS), nameof(OS.addExe))]
    private static bool AddExePrefix(OS __instance, ExeModule exe)
    {
        exe.os = __instance;
        if (exe is GameExecutable gameExe)
        {
            __instance.AddGameExecutable(gameExe);
            return false;
        }
        return true;
    }

    private static void OnExecutableList(ExecutableListEvent e)
    {
        foreach (FileEntry exeFile in e.BinExes.Keys.ToList())
        {
            if (IsExeData(exeFile.data))
                e.BinExes[exeFile] = true;
        }
    }

    [HarmonyILManipulator]
    [HarmonyPatch(typeof(OS), nameof(OS.Update), typeof(GameTime), typeof(bool), typeof(bool))]
    private static void onOSUpdate(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(MoveType.Before,
            x => x.MatchNop(),
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(AccessTools.Field(typeof(OS), nameof(OS.exes))),
            x => x.MatchLdloc(1),
            x => x.MatchCallvirt(AccessTools.Method(typeof(List<ExeModule>), nameof(List<ExeModule>.RemoveAt), [typeof(int)]))
        );

        c.Emit(OpCodes.Ldarg, 0);
        c.Emit(OpCodes.Ldloc, 1);

        c.EmitDelegate<Action<OS, int>>((os, index) =>
        {
            if(os.exes[index] is GameExecutable exe)
                exe.Completed();
        });
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Programs), nameof(Programs.kill))]
    private static bool programsKillFix(string[] args, OS os)
    {
        if (args is [_, var pidStr, ..] && int.TryParse(pidStr, out var pid))
        {
            foreach (var exe in os.exes)
            {
                if (exe.PID == pid)
                {
                    if (exe.Kill())
                    {
                        os.write($"Process {pidStr}[{exe.IdentifierName}] Ended");
                    }
                    else
                    {
                        os.write("The process did not respond");
                    }
                    return false;
                }
            }
            os.write(LocaleTerms.Loc("Invalid PID"));
        }
        else
        {
            os.write(LocaleTerms.Loc("Error: Invalid PID or Input Format"));
        }
        return false;
    }
}
