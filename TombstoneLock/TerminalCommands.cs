using System.Collections.Generic;
using HarmonyLib;

namespace TombstoneLock;

public static class TerminalCommands
{
	[HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
	public class AddChatCommands
	{
		private static void Postfix()
		{
			_ = new Terminal.ConsoleCommand("tombstone", "Manages the Tombstone Lock commands.", (Terminal.ConsoleEvent)(args =>
			{
				if (!TombstoneLock.configSync.IsAdmin && !TombstoneLock.configSync.IsSourceOfTruth)
				{
					args.Context.AddString("You are not an admin on this server.");
					return;
				}

				if (args.Length >= 2 && args[1] == "admin")
				{
					TombstoneLock.admin = !TombstoneLock.admin;
					args.Context.AddString($"Tombstone admin is now {TombstoneLock.admin}.");
					return;
				}
				
				args.Context.AddString("Tombstone Lock console commands - use 'Tombstone' followed by one of the following options.");
				args.Context.AddString("admin - toggles the admin mode for this session.");
			}), optionsFetcher: () => new List<string> { "admin" });
		}
	}
}
