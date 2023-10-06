using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Groups;
using HarmonyLib;
using ServerSync;

namespace TombstoneLock;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInIncompatibility("org.bepinex.plugins.valheim_plus")]
[BepInDependency("org.bepinex.plugins.groups", BepInDependency.DependencyFlags.SoftDependency)]
public class TombstoneLock : BaseUnityPlugin
{
	private const string ModName = "TombstoneLock";
	private const string ModVersion = "1.0.7";
	private const string ModGUID = "org.bepinex.plugins.tombstonelock";

	public static readonly ConfigSync configSync = new(ModName) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

	private static ConfigEntry<Toggle> serverConfigLocked = null!;
	private static ConfigEntry<PickupPermission> pickupPermission = null!;
	private static ConfigEntry<int> timedDestruction = null!;

	private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
	{
		ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

		SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
		syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

		return configEntry;
	}

	private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);

	private enum Toggle
	{
		On = 1,
		Off = 0,
	}

	private enum PickupPermission
	{
		Me = 0,
		Everyone = 1,
		Group = 2,
	}

	public static bool admin = false;

	public void Awake()
	{
		serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
		configSync.AddLockingConfigEntry(serverConfigLocked);
		pickupPermission = config("1 - General", "Pickup Permission", PickupPermission.Me, "Who can pick up your tombstone?", false);
		timedDestruction = config("1 - General", "Timed Destruction", 0, "Time in minutes until the tombstone is automatically destroyed, if it is not being picked up. Use 0 to disable this.");

		Assembly assembly = Assembly.GetExecutingAssembly();
		Harmony harmony = new(ModGUID);
		harmony.PatchAll(assembly);
	}

	[HarmonyPatch(typeof(TombStone), nameof(TombStone.Awake))]
	private class SetPickupPermission
	{
		private static void Prefix(TombStone __instance)
		{
			ZDO zdo = __instance.GetComponent<ZNetView>().GetZDO();
			if (zdo.IsOwner() && zdo.GetLong("timeOfDeath") == 0L)
			{
				zdo.Set("TombstoneLock PickupPermission", (int)pickupPermission.Value);
			}
		}

		private static void Postfix(TombStone __instance)
		{
			if (timedDestruction.Value == 0)
			{
				return;
			}

			long now = ZNet.instance.GetTime().Ticks;
			long death = __instance.m_nview.GetZDO().GetLong("timeOfDeath");
			float remaining = timedDestruction.Value * 60 + (death - now) / TimeSpan.TicksPerSecond;
			if (remaining <= 0)
			{
				ZNetScene.instance.Destroy(__instance.gameObject);
			}
			else
			{
				__instance.gameObject.AddComponent<TimedDestruction>().Trigger(remaining);
			}
		}
	}

	[HarmonyPatch(typeof(TombStone), nameof(TombStone.Interact))]
	private class PreventInteraction
	{
		private static bool Prefix(TombStone __instance)
		{
			bool canPickup = false;

			if (admin)
			{
				return true;
			}

			switch ((PickupPermission)__instance.m_nview.GetZDO().GetInt("TombstoneLock PickupPermission"))
			{
				case PickupPermission.Me:
				{
					if (__instance.IsOwner())
					{
						canPickup = true;
					}

					break;
				}
				case PickupPermission.Everyone:
				{
					canPickup = true;
					break;
				}
				case PickupPermission.Group:
				{
					if (API.FindGroupMemberByPlayerId(__instance.GetOwner()) != null)
					{
						canPickup = true;
					}

					break;
				}
				default:
				{
					canPickup = false;

					break;
				}
			}

			if (!canPickup && __instance.GetOwner() != 0)
			{
				Player.m_localPlayer.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_cantpickup"));

				return false;
			}

			return true;
		}
	}
}
