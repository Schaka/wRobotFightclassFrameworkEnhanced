using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using CombatRotation.RotationFramework;
using robotManager.Helpful;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using wManager.Wow.Bot.States;
using wManager.Events;
using wManager;
using wManager.Wow.Enums;

public class Main : ICustomClass
{
    public float Range => NeedsRangePull() ? 29f : 5f;


    private bool _isLaunched;
    private static WoWLocalPlayer Me = ObjectManager.Me;

    private readonly List<RotationSpell> Enchants = new List<RotationSpell>
    {
        new RotationSpell("Windfury Weapon"),
        new RotationSpell("Flametongue Weapon"),
        new RotationSpell("Rockbiter Weapon"),
    };
    
    private static RotationSpell LightningShield = new RotationSpell("Lightning Shield");
    private static RotationSpell GhostWolf = new RotationSpell("Ghost Wolf", type: RotationSpell.VerificationType.AURA);
    private static RotationSpell HealingWave = new RotationSpell("Healing Wave");
    private static RotationSpell CurePoison = new RotationSpell("Cure Poison");
    private static RotationSpell CureDisease = new RotationSpell("Cure Disease");

    private List<RotationStep> RotationSpells = new List<RotationStep>{
        new RotationStep(new RotationSpell("Attack"), 0.5f, (s, t) => RotationFramework.IsCast && !RotationCombatUtil.IsAutoAttacking(), RotationCombatUtil.BotTarget),
        new RotationStep(new RotationSpell("Gift of the Naaru"), 0.9f, (s, t) => t.HealthPercent < 70 && t.ManaPercentage < 35 && RotationFramework.Target.HealthPercent > 80, RotationCombatUtil.FindMe),
        new RotationStep(new RotationSpell("Blood Fury"), 0.9f, (s, t) => t.ManaPercentage < 10, RotationCombatUtil.FindMe),
        new RotationStep(new RotationSpell("Lightning Bolt", 1), 0.95f, (s, t) => !Me.InCombatFlagOnly && NeedsRangePull() && t.HealthPercent == 100, RotationCombatUtil.BotTarget),
        new RotationStep(new RotationSpell("Healing Wave"), 1.0f, (s, t) => t.HealthPercent < 35, RotationCombatUtil.FindMe),
        new RotationStep(new RotationSpell("Earth Shock", 1), 1.1f, (s, t) => t.IsCasting() && !CanShock(), RotationCombatUtil.BotTarget),
        new RotationStep(new RotationSpell("Grounding Totem"), 1.1f, (s, t) => true, RotationCombatUtil.FindEnemyCastingOnMe),
        new RotationStep(new RotationSpell("Tremor Totem"), 1.1f, (s, t) => t.CastingSpell("Fear", "Terrify", "Howl of Terror", "Hibernate", "Scare Beast"), RotationCombatUtil.FindEnemyCasting, true),
        new RotationStep(new RotationSpell("Stoneclaw Totem"), 1.1f, (s, t) => RotationFramework.Units.Where(u => u.Reaction == Reaction.Hostile && u.IsAlive && u.GetDistance < 8).ToList().Count >= 2 && !HasTotem("Stoneclaw Totem"), RotationCombatUtil.FindMe),
        new RotationStep(new RotationSpell("Shamanistic Rage"), 2.1f, (s, t) => Me.ManaPercentage < 50 && t.HealthPercent > 80 && t.GetDistance <= 8, RotationCombatUtil.FindMe),
        new RotationStep(new RotationSpell("Shamanistic Rage"), 2.1f, (s, t) => RotationFramework.Units.Count(o => o.IsAlive && o.Reaction == Reaction.Hostile && o.GetDistance <= 8) >= 2, RotationCombatUtil.FindMe),
        new RotationStep(new RotationSpell("Stormstrike"), 10f, (s, t) => true, RotationCombatUtil.BotTarget),
        new RotationStep(new RotationSpell("Flame Shock"), 10.1f, (s, t) => CanShock() && !t.HasBuff("Flame Shock") && t.HealthPercent > 40, RotationCombatUtil.BotTarget),
        new RotationStep(new RotationSpell("Earth Shock"), 10.2f, (s, t) => (CanShock() || t.IsCasting()) && t.HasBuff("Stormstrike"), RotationCombatUtil.BotTarget),
        new RotationStep(new RotationSpell("Frost Shock"), 11f, (s, t) => CanShock(), RotationCombatUtil.BotTarget),
        new RotationStep(new RotationSpell("Earth Shock"), 12, (s, t) => CanShock(), RotationCombatUtil.BotTarget),
        new RotationStep(CurePoison, 16f, (s, t) => t.HasDebuffType("Poison") && t.ManaPercentage >= 30, RotationCombatUtil.FindMe),
    };

    public void Initialize()
    {
        ExampleSettings.Load();
        wManagerSetting.CurrentSetting.UseLuaToMove = true;
        
        RotationFramework.Initialize(ExampleSettings.CurrentSetting.SlowRotation, ExampleSettings.CurrentSetting.FrameLock);
        MovementEvents.OnMovementPulse += LongGhostWolfHandler;
        MovementEvents.OnMoveToPulse += GhostWolfHandler;
        _isLaunched = true;

        RotationSpells.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        Rotation();
    }

    private void GhostWolfHandler(Vector3 point, CancelEventArgs cancelable)
    {
        if (point.DistanceTo(Me.Position) >= wManagerSetting.CurrentSetting.MountDistance)
        {
            UseGhostWolf();
        }
    }

    private void LongGhostWolfHandler(List<Vector3> points, CancelEventArgs cancelable)
    {
        if (points.Select(p => p.DistanceTo(Me.Position)).Aggregate(0f, (p1, p2) => p1 + p2) >= wManagerSetting.CurrentSetting.MountDistance)
        {
            UseGhostWolf();
        }
    }

    private void UseGhostWolf()
    {
        if (string.IsNullOrWhiteSpace(wManagerSetting.CurrentSetting.GroundMountName) &&
            !new Regeneration().NeedToRun &&
            !Me.HasBuff("Ghost Wolf") &&
            ExampleSettings.CurrentSetting.GhostWolf &&
            !Me.InCombat)
        {
            RotationCombatUtil.CastSpell(GhostWolf, Me);
            Thread.Sleep(Usefuls.Latency);
            Usefuls.WaitIsCasting();
        }
    }

    public void Rotation()
    {
        while (_isLaunched)
        {
            try
            {
                if (Conditions.InGameAndConnectedAndAliveAndProductStartedNotInPause && !Me.IsDead)
                {

                    UseBuffs();
                    if(Fight.InFight)
                    {
                        if(Me.HasBuff("Ghost Wolf") && RotationFramework.Units.Any(u => u.IsTargetingMe && !u.IsPlayer()))
                            Lua.RunMacroText("/cancelaura Ghost Wolf");

                        RotationFramework.RunRotation(RotationSpells);
                    }
                }
            }catch(Exception e)
            {
                Logging.WriteError("ExampleClass ERROR:" + e);
            }
            
            Thread.Sleep(Usefuls.LatencyReal);
        }
    }

    private static bool CanShock()
    {
        return Me.ManaPercentage > 30 || Me.HasBuff("Focused");
    }

    private void UseBuffs()
    {
        if (Me.IsMounted || Me.InCombatFlagOnly || Fight.InFight || Me.HasBuff("Resurrection Sickness") || Me.HasBuff("Ghost Wolf"))
            return;

        if (Me.HasDebuffType("Poison"))
        {
            RotationCombatUtil.CastSpell(CurePoison, Me);
        }
        
        if (Me.HasDebuffType("Disease"))
        {
            RotationCombatUtil.CastSpell(CureDisease, Me);
        }

        if (Me.HealthPercent < 60)
        {
            RotationCombatUtil.CastSpell(HealingWave, Me);
        }

        if (Me.HasBuff("Lightning Shield"))
        {
            RotationCombatUtil.CastBuff(LightningShield);
        }

        if (!HasMainHandEnchant())
            UseEnchants();
        
        if(HasOffhand() && !HasOffhandEnchant())
            UseEnchants();

        if (!HasMainHandEnchant())
            UseEnchants();
        
    }

    private void UseEnchants()
    {
        foreach (var Enchant in Enchants)
        {
            if (Enchant.IsKnown())
            {
                Enchant.Spell.Launch();
                break;
            }
        }
    }

    private bool HasMainHandEnchant()
    {
        return Lua.LuaDoString<bool>("hasMainHandEnchant, mainHandExpiration, mainHandCharges, hasOffHandEnchant, offHandExpiration, offHandCharges = GetWeaponEnchantInfo();", "hasMainHandEnchant");
    }

    private bool HasOffhandEnchant()
    {
        return Lua.LuaDoString<bool>("hasMainHandEnchant, mainHandExpiration, mainHandCharges, hasOffHandEnchant, offHandExpiration, offHandCharges = GetWeaponEnchantInfo();", "hasOffHandEnchant");
    }

    private bool HasOffhand()
    {
        return Lua.LuaDoString<bool>(
            @"hasOffhand = false;
            local itemId = GetInventoryItemLink(""player"", 17);
            if not itemId then return end
            local itemName, itemLink, itemRarity, itemLevel, itemMinLevel, itemType, itemSubType, itemStackCount, itemEquipLoc, itemTexture = GetItemInfo(itemId)
            if itemType == ""Weapon"" then
                hasOffhand = true;
            end
            ", "hasOffhand");
    }

    private static bool HasTotem(String totem)
    {
        return RotationFramework.Units.FirstOrDefault(o => o.IsMyPet && o.GetDistance < 20 && o.Name.Contains(totem)) != null;
    }

    private static bool HasNoTotems()
    {
        return RotationFramework.Units.FirstOrDefault(o => o.IsMyPet && o.GetDistance < 20) == null;
    }
    
    private static bool NeedsRangePull()
    {
        if (Fight.CombatStartSince > 8000)
        {
            return false;
        }
        
        return RotationFramework.Units.FirstOrDefault(o =>
                   o.IsAlive && o.Reaction == Reaction.Hostile &&
                   o.Guid != RotationFramework.Target.Guid &&
                   o.Position.DistanceTo(RotationFramework.Target.Position) <= 38) != null;
    }

    public void Dispose()
    {
        _isLaunched = false;
        RotationFramework.Dispose();
        
        MovementEvents.OnMovementPulse -= LongGhostWolfHandler;
        MovementEvents.OnMoveToPulse -= GhostWolfHandler;
    }

    public void ShowConfiguration()
    {
        ExampleSettings.Load();
        ExampleSettings.CurrentSetting.ToForm();
        ExampleSettings.CurrentSetting.Save();
    }

}

/*
 * SETTINGS
*/

[Serializable]
public class ExampleSettings : Settings
{
    
    [Setting]
    [DefaultValue(true)]
    [Category("General")]
    [DisplayName("Framelock")]
    [Description("Lock frames before each combat rotation (can help if it skips spells)")]
    public bool FrameLock { get; set; }
    
    [Setting]
    [DefaultValue(false)]
    [Category("General")]
    [DisplayName("Slow rotation for performance issues")]
    [Description("If you have performance issues with wRobot and the fightclass, activate this. It will try to sleep until the next spell can be executed. This can and will cause some spells to skip.")]
    public bool SlowRotation { get; set; }

    [Setting]
    [DefaultValue(true)]
    [Category("General")]
    [DisplayName("Use Ghost Wolf")]
    [Description("Whether Ghost Wolf will be used")]
    public bool GhostWolf { get; set; }

    public ExampleSettings()
    {
        FrameLock = true;
        SlowRotation = false;
        GhostWolf = true;
    }

    public static ExampleSettings CurrentSetting { get; set; }

    public bool Save()
    {
        try
        {
            return Save(AdviserFilePathAndName("CustomClass-ExampleSettings", ObjectManager.Me.Name + "." + Usefuls.RealmName));
        }
        catch (Exception e)
        {
            Logging.WriteError("ExampleSettings > Save(): " + e);
            return false;
        }
    }

    public static bool Load()
    {
        try
        {
            if (File.Exists(AdviserFilePathAndName("CustomClass-ExampleSettings", ObjectManager.Me.Name + "." + Usefuls.RealmName)))
            {
                CurrentSetting =
                    Load<ExampleSettings>(AdviserFilePathAndName("CustomClass-ExampleSettings",
                                                                 ObjectManager.Me.Name + "." + Usefuls.RealmName));
                return true;
            }
            CurrentSetting = new ExampleSettings();
        }
        catch (Exception e)
        {
            Logging.WriteError("ExampleSettings > Load(): " + e);
        }
        return false;
    }
}