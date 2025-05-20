using BepInEx;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Projectile;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MSOTSSurvivorTweaks
{
	[BepInDependency(RecalculateStatsAPI.PluginGUID)]
	[BepInDependency(LanguageAPI.PluginGUID)]
	[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
	public class MSOTSSurvivorTweaks : BaseUnityPlugin
	{
		public const string PluginGUID = PluginAuthor + "." + PluginName;
		public const string PluginAuthor = "mayhemmmwith3ms";
		public const string PluginName = "SOTSSurvivorTweaks";
		public const string PluginVersion = "0.1.0";

		private static Dictionary<string, Dictionary<string, string>> LanguageAPITokenDict;

		public void Awake()
		{
			Log.Init(Logger);
			BindConfigs();
			RemoveLanguageOverridesBasedOnConfig();

			On.RoR2.Projectile.CleaverProjectile.Awake += CleaverProjectile_Awake;
			On.EntityStates.Chef.Dice.OnEnter += Dice_OnEnter;
			On.RoR2.BuffCatalog.Init += BuffCatalog_Init;
			RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
			On.EntityStates.Chef.RolyPoly.OnEnter += RolyPoly_OnEnter;
			On.EntityStates.Chef.OilSpillBase.OnEnter += OilSpillBase_OnEnter;
			On.EntityStates.Chef.YesChef.OnEnter += YesChef_OnEnter;
			On.EntityStates.Chef.OilSpillBase.OnExit += OilSpillBase_OnExit;
			On.EntityStates.FalseSon.MeridiansWillTeleport.FixedUpdate += MeridiansWillTeleport_FixedUpdate;
			On.EntityStates.FalseSon.LaserFatherCharged.OnEnter += LaserFatherCharged_OnEnter;
			On.EntityStates.FalseSon.MeridiansWillAim.OnEnter += MeridiansWillAim_OnEnter;
			On.EntityStates.Seeker.UnseenHand.OnEnter += UnseenHand_OnEnter;
			On.PalmBlastProjectileController.Init += PalmBlastProjectileController_Init; // who wrote this why is it outside of a namespace why doesnt it scale with level damage
			//:adrenaline:
			IL.EntityStates.Chef.Glaze.FireGrenade += ILGlazeWeakenRemoval;
			IL.RoR2.HealthComponent.TakeDamageProcess += ILLunarRuinDamageMultFix;
			IL.EntityStates.FalseSon.MeridiansWillTeleport.OnEnter += ILMeridianTeleportCameraFix;
			IL.EntityStates.Seeker.Meditate.OnEnter += ILMeditateZoomoutTweak;
			//IL.EntityStates.Chef.RolyPoly.StartRolyPoly += ILRollBleedRemoval;
		}

		#region config
		public const string SeekerTweaksConfigSection = "Seeker Tweaks";
		public const string ChefTweaksConfigSection = "Chef Tweaks";
		public const string FalseSonTweaksConfigSection = "False Son Tweaks";

		public static ConfigEntry<bool> Config_SeekerPalmBlastLevelDamageFix { get; set; }

		public static ConfigEntry<bool> Config_SeekerReduceUnseenHandZoomout { get; set; }

		public static ConfigEntry<bool> Config_SeekerReduceMeditateZoomout { get; set; }

		public static ConfigEntry<bool> Config_ChefCleaverTweaks { get; set; }

		public static ConfigEntry<bool> Config_ChefCleaverHoldoutTweaks { get; set; }

		public static ConfigEntry<bool> Config_ChefHideCookingDebuffs { get; set; }

		public static ConfigEntry<bool> Config_ChefOiledTweaks { get; set; }

		public static ConfigEntry<bool> Config_ChefBoostedRollRemoveBleed { get; set; }

		public static ConfigEntry<bool> Config_ChefMarkOilSpillAsSkill { get; set; }

		public static ConfigEntry<bool> Config_ChefFixOilSpillAirMomentum { get; set; }

		public static ConfigEntry<bool> Config_ChefStopYesChefFromStealingBands { get; set; }

		public static ConfigEntry<bool> Config_FalseSonLunarRuinDamageStackingFix { get; set; }

		public static ConfigEntry<bool> Config_FalseSonMeridiansWillCameraJumpFix { get; set; }

		public static ConfigEntry<bool> Config_FalseSonRemoveMeridiansWillZoomout { get; set; }

		public static ConfigEntry<bool> Config_FalseSonReduceChargedLaserFatherZoomout { get; set; }

		public void BindConfigs()
		{
			Config_SeekerReduceMeditateZoomout = Config.Bind(
				SeekerTweaksConfigSection,
				"Reduce Meditate Zoomout",
				true,
				"Reduces the camera zoomout during Meditate."
			);

			Config_SeekerReduceUnseenHandZoomout = Config.Bind(
				SeekerTweaksConfigSection,
				"Reduce Unseen Hand Zoomout",
				true,
				"Reduces the camera zoomout while targeting Unseen Hand."
			);

			Config_SeekerPalmBlastLevelDamageFix = Config.Bind(
				SeekerTweaksConfigSection,
				"Fix Palm Blast Level Scaling",
				true,
				"Fixes Palm Blast's damage not scaling with player level."
			);

			Config_ChefCleaverTweaks = Config.Bind(
				ChefTweaksConfigSection,
				"Dice Tweaks",
				true,
				"Reduces the base damage of Dice from 200% to 180%, and increases the proc coefficient from 0.5 to 0.8."
			);

			Config_ChefCleaverHoldoutTweaks = Config.Bind(
				ChefTweaksConfigSection,
				"Dice Holdout Tweaks",
				true,
				"Changes Dice's lingering cleavers to deal 4x40% damage per second, with a proc coefficient of 0.5."
			);

			Config_ChefHideCookingDebuffs = Config.Bind(
				ChefTweaksConfigSection,
				"Hide Cooking Debuffs",
				true,
				"Hides the 'marker' debuffs inflicted by CHEF's skills to reduce visual clutter."
			);

			Config_ChefOiledTweaks = Config.Bind(
				ChefTweaksConfigSection,
				"Oiled Tweaks",
				true,
				"Changes Glaze to no longer inflict Weak, and instead adds the effects of Weak to Oiled, and makes Oiled count towards activating Death Mark."
			);

			Config_ChefBoostedRollRemoveBleed = Config.Bind(
				ChefTweaksConfigSection,
				"Boosted Roll Tweaks",
				true,
				"Changes Boosted Roll to no longer inflict Bleed, and instead make it rapidly hit enemies in its area for 8x50% damage per second, with a proc coefficient of 0.6"
			);

			Config_ChefMarkOilSpillAsSkill = Config.Bind(
				ChefTweaksConfigSection,
				"Mark Oil Spill as Skill",
				true,
				"Fixes the globs fired by Oil Spill not being marked as a skill for items like Breaching Fin."
			);

			Config_ChefFixOilSpillAirMomentum = Config.Bind(
				ChefTweaksConfigSection,
				"Oil Spill Air Control Fix",
				true,
				"Fixes Oil Spill's increased air control persisting indefinitely after the skill ends."
			);

			Config_ChefStopYesChefFromStealingBands = Config.Bind(
				ChefTweaksConfigSection,
				"Yes, CHEF! Elemental Band Fix.",
				true,
				"Lowers Yes, CHEF!'s damage slightly to prevent it stealing Elemental Band procs."
			);

			Config_FalseSonLunarRuinDamageStackingFix = Config.Bind(
				FalseSonTweaksConfigSection,
				"Lunar Ruin Damage Multiplication Fix",
				true,
				"Fixes Lunar Ruin's damage multiplication applying to the initial damage of the hit and carrying over through proc chains, rather than just the final damage of the hit.\n" +
				"The vanilla behaviour would cause long proc chains to multiply damage exponentially, and for effects based on % base damage to erroneously activate with enough Lunar Ruin (e.g. the Elemental Bands).\n" +
				"This fix brings the damage multiplication in line with all other sources of damage multiplication in the game."
			);

			Config_FalseSonMeridiansWillCameraJumpFix = Config.Bind(
				FalseSonTweaksConfigSection,
				"Meridians Will Camera Lerp Fix",
				true,
				"Fixes the jarring camera movement at the start of the Meridian's Will teleport ability."
			);

			Config_FalseSonRemoveMeridiansWillZoomout = Config.Bind(
				FalseSonTweaksConfigSection,
				"Remove Meridians Will Camera Zoomout",
				true,
				"Removes the camera zoomout while aiming Meridian's Will."
			);

			Config_FalseSonReduceChargedLaserFatherZoomout = Config.Bind(
				FalseSonTweaksConfigSection,
				"Remove Laser of the Father Zoomout",
				true,
				"Removes the camera zoomout while using Laser of the Father."
			);
		}

		// definitively giga ass but i DONT  CARE
		void RemoveLanguageOverridesBasedOnConfig()
		{
			try
			{
				LanguageAPITokenDict = (Dictionary<string, Dictionary<string, string>>)typeof(LanguageAPI).GetField("CustomLanguage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
			}
			catch (Exception e) when (e is ArgumentNullException || e is NullReferenceException)
			{
				Log.Error(e);
				LanguageAPITokenDict = null;
			}

			if (LanguageAPITokenDict is null)
			{
				Log.Error("LanguageAPITokenDict is null! This may indicate an attempt to get the dict before LanguageAPI is initialized, or a change in the field name in the API itself.");
			}

			foreach (var language in LanguageAPITokenDict.Keys)
			{
				if (!Config_ChefCleaverTweaks.Value)
				{
					LanguageAPITokenDict[language].Remove("CHEF_PRIMARY_DESCRIPTION");
					LanguageAPITokenDict[language].Remove("CHEF_PRIMARY_BOOST_DESCRIPTION");
					LanguageAPITokenDict[language].Remove("CHEF_PRIMARY_BOOST_DESCRIPTION_SKILL");
				}

				if (!Config_ChefBoostedRollRemoveBleed.Value)
				{
					LanguageAPITokenDict[language].Remove("CHEF_UTILITY_BOOST_DESCRIPTION_SKILL");
					LanguageAPITokenDict[language].Remove("CHEF_UTILITY_BOOST_DESCRIPTION");
				}

				if (!Config_ChefStopYesChefFromStealingBands.Value)
				{
					LanguageAPITokenDict[language].Remove("CHEF_SPECIAL_ALT1_DESCRIPTION");
				}
			}
		}
		#endregion

		private void MeridiansWillAim_OnEnter(On.EntityStates.FalseSon.MeridiansWillAim.orig_OnEnter orig, EntityStates.FalseSon.MeridiansWillAim self)
		{
			if (Config_FalseSonRemoveMeridiansWillZoomout.Value)
			{
				self.cameraTeleportPositionOffset = Vector3.zero;
			}

			orig(self);
		}

		private void PalmBlastProjectileController_Init(On.PalmBlastProjectileController.orig_Init orig, PalmBlastProjectileController self, CharacterBody body)
		{
			orig(self, body);

			if (Config_SeekerPalmBlastLevelDamageFix.Value) 
			{
				self.projectileDamage.damage = body.damage;
			}
		}

		// this one specifically is done different to all of the other seeker zoomouts so it needs an IL edit :)
		private void ILMeditateZoomoutTweak(ILContext il)
		{
			if (!Config_SeekerReduceMeditateZoomout.Value)
				return;

			il.WrapILHook(cx =>
			{
				ILCursor c = new(cx);

				c.GotoNext(x => x.MatchLdcI4((int)CameraTargetParams.AimType.ZoomedOut));
				c.Remove();
				c.Emit(OpCodes.Ldc_I4, (int)CameraTargetParams.AimType.Aura);
			}, nameof(ILMeditateZoomoutTweak));
		}

		// yes i know im setting a static field in an instance hook shut up
		private void UnseenHand_OnEnter(On.EntityStates.Seeker.UnseenHand.orig_OnEnter orig, EntityStates.Seeker.UnseenHand self)
		{
			if (Config_SeekerReduceUnseenHandZoomout.Value)
			{
				EntityStates.Seeker.UnseenHand.abilityAimType = (int)CameraTargetParams.AimType.Aura;
			}

			orig(self);
		}

		private void LaserFatherCharged_OnEnter(On.EntityStates.FalseSon.LaserFatherCharged.orig_OnEnter orig, EntityStates.FalseSon.LaserFatherCharged self)
		{
			if (Config_FalseSonReduceChargedLaserFatherZoomout.Value)
			{
				EntityStates.FalseSon.LaserFatherCharged.abilityAimType = (int)CameraTargetParams.AimType.Standard;
			}

			orig(self);
		}

		private void MeridiansWillTeleport_FixedUpdate(On.EntityStates.FalseSon.MeridiansWillTeleport.orig_FixedUpdate orig, EntityStates.FalseSon.MeridiansWillTeleport self)
		{
			if (Config_FalseSonMeridiansWillCameraJumpFix.Value)
			{
				//literally just copypasted from vanilla lol
				if (self.fixedAge >= (self.teleportDelayDuration - 0.05f) && self.teleportVector != Vector3.zero) // this sucks fuck this game
				{
					float num = Vector3.Distance(self.aimLocation, self.characterBody.corePosition);
					float num2 = Mathf.Lerp(self.minTeleportCameraLerpDuration, self.maxTeleportCameraLerpDuration, num / self.distanceToCheck);
					if (num2 < 1f)
					{
						num2 = Mathf.Sqrt(num2);
					}
					self.cameraTargetParams.AddLerpRequest(num2 * 0.8f);

					self.teleportVector = Vector3.zero;
				}
			}

			orig(self);
		}

		// only part of the fix, removes the camera lerp request from OnEnter
		private void ILMeridianTeleportCameraFix(ILContext il)
		{
			if (!Config_FalseSonMeridiansWillCameraJumpFix.Value)
				return;

			il.WrapILHook(x =>
			{
				ILCursor c = new(x);

				ILLabel l = null;

				c.GotoNext(x => x.MatchBgeUn(out l));
				c.GotoNext(x => x.MatchRet());
				c.MarkLabel(l);
				c.GotoPrev(x => x.MatchLdarg(0));

				c.RemoveRange(4);

			}, nameof(ILMeridianTeleportCameraFix));
		}

		private void ILLunarRuinDamageMultFix(ILContext il)
		{
			if (!Config_FalseSonLunarRuinDamageStackingFix.Value)
				return;

			il.WrapILHook(x =>
			{
				ILCursor c = new(x);

				// kill existing lunar ruin calculations
				c.GotoNext(x => x.MatchLdsfld(typeof(DLC2Content.Buffs).GetField("lunarruin", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)));
				c.GotoPrev(x => x.MatchLdarg(0));

				var l = c.IncomingLabels.First();

				c.RemoveRange(4);
				c.Emit(OpCodes.Ldc_I4, 0);
				c.Index--;
				c.MarkLabel(l);

				// move to after the 'final' hit damage local is defined
				c.GotoNext(x => x.MatchLdcI4(9));
				c.GotoNext(x => x.MatchStloc(7));
				c.Index++;

				// load arguments onto the stack and then call the delegate with the fixed calculations
				c.Emit(OpCodes.Ldloc, 7);
				c.Emit(OpCodes.Ldarg, 1);
				c.Emit(OpCodes.Ldarg, 0);

				c.EmitDelegate<Func<float, DamageInfo, HealthComponent, float>>((damage, damageInfo, healthComponent) =>
				{
					if (healthComponent.body.HasBuff(DLC2Content.Buffs.lunarruin))
					{
						damage *= 1f + healthComponent.body.GetBuffCount(DLC2Content.Buffs.lunarruin) * 0.1f;
						damageInfo.damageColorIndex = DamageColorIndex.Void;
					}
					return damage;
				});

				c.Emit(OpCodes.Stloc, 7);

			}, nameof(ILLunarRuinDamageMultFix));
		}

		private void YesChef_OnEnter(On.EntityStates.Chef.YesChef.orig_OnEnter orig, EntityStates.Chef.YesChef self)
		{
			if (Config_ChefStopYesChefFromStealingBands.Value)
			{
				self.explosionDamageCoefficient = 3.8f;
			}

			orig(self);
		}

		private void OilSpillBase_OnExit(On.EntityStates.Chef.OilSpillBase.orig_OnExit orig, EntityStates.Chef.OilSpillBase self)
		{
			orig(self);

			if (Config_ChefFixOilSpillAirMomentum.Value)
			{
				self.characterBody.characterMotor.airControl = 0.25f;
			}
		}

		private void OilSpillBase_OnEnter(On.EntityStates.Chef.OilSpillBase.orig_OnEnter orig, EntityStates.Chef.OilSpillBase self)
		{
			if (Config_ChefMarkOilSpillAsSkill.Value)
			{
				self.meatballProjectile.GetComponent<ProjectileDamage>().damageType.damageSource = DamageSource.Utility;
				self.meatballProjectileBoostedIgnite.GetComponent<ProjectileDamage>().damageType.damageSource = DamageSource.Utility;
				self.meatballProjectileFrozen.GetComponent<ProjectileDamage>().damageType.damageSource = DamageSource.Utility;
			}

			orig(self);
		}

		// killing myselffff
		// il hook: remove meaningless bleed from boosted roll (lol)
		//private void ILRollBleedRemoval(ILContext il)
		//{
		//	il.WrapILHook(() =>
		//	{
		//		ILCursor c = new(il);
		//
		//		c.GotoNext(i => i.MatchLdcI4(1024));
		//		c.Remove();
		//		c.Emit(OpCodes.Ldc_I4, 0);
		//	}, nameof(ILRollBleedRemoval));
		//}

		// if you're reading this i just want you to know theres a fireprojectileinfo created for this in the code and
		// its NEVER USED so i spent like 30 minutes making an ilhook for it only to realise it wouldnt do anything
		private void RolyPoly_OnEnter(On.EntityStates.Chef.RolyPoly.orig_OnEnter orig, EntityStates.Chef.RolyPoly self)
		{
			if (Config_ChefBoostedRollRemoveBleed.Value && self.projectilePrefab is not null)
			{
				ProjectileDotZone dotZoneComponent = self.projectilePrefab.GetComponent<ProjectileDotZone>();
				ProjectileDamage damageComponent = self.projectilePrefab.GetComponent<ProjectileDamage>();
				ProjectileController controllerComponent = self.projectilePrefab.GetComponent<ProjectileController>();

				damageComponent.damageType = DamageTypeCombo.GenericUtility;

				//self.projectilePrefab.transform.localScale = Vector3.one * 1.2f;

				self.whirlwindDamageCo = 0.5f;
				dotZoneComponent.damageCoefficient = 1f;
				dotZoneComponent.resetFrequency = 8f;
				dotZoneComponent.fireFrequency = 8f;
				controllerComponent.procCoefficient = 0.6f;
			}

			orig(self);
		}

		// its weak im afraid...
		private void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
		{
			if (!Config_ChefOiledTweaks?.Value ?? false)
				return;

			if (sender.HasBuff(RoR2Content.Buffs.Weak))
			{
				args.armorAdd -= 30f;
				args.damageMultAdd -= 0.4f;
				args.moveSpeedReductionMultAdd += 0.6f;
			}
		}

		// il hook: stops glaze from inflicting Weak (because im moving the effects into oiled)
		private void ILGlazeWeakenRemoval(ILContext il)
		{
			if (!Config_ChefOiledTweaks?.Value ?? false)
				return;

			il.WrapILHook(x =>
			{
				ILCursor c = new(x);

				c.GotoNext(i => i.MatchLdcI4(16384));
				c.Remove();
				c.Emit(OpCodes.Ldc_I4, 0);
			}, nameof(ILGlazeWeakenRemoval));
		}

		private void BuffCatalog_Init(On.RoR2.BuffCatalog.orig_Init orig)
		{
			if (Config_ChefHideCookingDebuffs?.Value ?? true)
			{
				DLC2Content.Buffs.CookingChilled.isHidden = true;
				DLC2Content.Buffs.CookingChopped.isHidden = true;
				DLC2Content.Buffs.CookingOiled.isHidden = true;
				DLC2Content.Buffs.CookingRoasted.isHidden = true;
				DLC2Content.Buffs.CookingRolled.isHidden = true;
				DLC2Content.Buffs.CookingRolling.isHidden = true;
				DLC2Content.Buffs.CookingSearing.isHidden = true;
			}

			if (Config_ChefOiledTweaks.Value)
			{
				DLC2Content.Buffs.Oiled.isDebuff = true;
			}

			orig();
		}

		private void Dice_OnEnter(On.EntityStates.Chef.Dice.orig_OnEnter orig, EntityStates.Chef.Dice self)
		{
			if (Config_ChefCleaverTweaks?.Value ?? true)
			{
				self.damageCoefficient = 1.8f;
				self.boostedDamageCoefficient = 3.6f;
			}

			orig(self);
		}

		private void CleaverProjectile_Awake(On.RoR2.Projectile.CleaverProjectile.orig_Awake orig, RoR2.Projectile.CleaverProjectile self)
		{
			orig(self);

			if (Config_ChefCleaverTweaks?.Value ?? true)
			{
				self.projectileController.procCoefficient = 0.8f;
			}

			if (true)
			{
				self.IdleOverlapAttack.resetInterval = 1f / 4f;
				self.IdleOverlapAttack.damageCoefficient = 0.2f;
				self.IdleOverlapAttack.overlapProcCoefficient = 0.5f;
			}
		}
	}
}
