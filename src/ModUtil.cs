using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Text;

namespace SOTSSurvivorTweaks;
public static class ModUtil
{
	// its sooo neccessary yepp
	public static void WrapILHook(this ILContext il, Action<ILContext> edit, string hookName)
	{
		try
		{
			edit.Invoke(il);

			Log.Info($"IL hook {hookName} completed");
		}
		catch (Exception e) 
		{
			Log.Error($"IL hook {hookName} encountered an unhandled exception!!!");
			Log.Error(e);
		}

#if DEBUG
		Log.Warning(il.ToString());
		Log.Warning("The above is only supposed to be seen in a DEBUG build. if you're seeing this in a release version, i've FUCKED UP!");
#endif
	}
}
