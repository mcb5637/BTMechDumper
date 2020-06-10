using BattleTech;
using Harmony;
using Org.BouncyCastle.Crypto.Digests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BTMechDumper
{
    class BTMechDumper
    {
        public static void Init(string dir, string sett)
        {
            var harmony = HarmonyInstance.Create("com.github.mcb5637.BTMechDumper");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static void DumpData(SimGameState s)
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                StreamWriter w = new StreamWriter(dir + "\\BTDump.txt", false);
                StreamWriter mcsv = new StreamWriter(dir + "\\BTDumpMechs.csv", false);
                StreamWriter wcsv = new StreamWriter(dir + "\\BTDumpWeapons.csv", false);
                StreamWriter acsv = new StreamWriter(dir + "\\BTDumpAmmo.csv", false);
                StreamWriter ucsv = new StreamWriter(dir + "\\BTDumpUpgrades.csv", false);
                w.WriteLine("BTDumper: " + s.CompanyName + " " + s.CurrentDate.ToString("dd-MM-yyyy"));
                w.WriteLine();
                DumpMechs(s, w, mcsv);
                w.WriteLine();
                DumpWeapons(s, w, wcsv);
                w.WriteLine();
                DumpAmmo(s, w, acsv);
                w.WriteLine();
                DumpUpgrades(s, w, ucsv);
                w.Close();
                mcsv.Close();
                wcsv.Close();
                acsv.Close();
                ucsv.Close();
            }
            catch (Exception e)
            {
                FileLog.Log(e.Message);
            }
        }

        public static void DumpMechs(SimGameState s, StreamWriter w, StreamWriter csv)
        {
            List<DumperDataEntry> mechs = new List<DumperDataEntry>();
            Dictionary<string, DumperDataEntry> blacklist = new Dictionary<string, DumperDataEntry>();
            mechs.Add(GetMechDesc());
            foreach (KeyValuePair<string, MechDef> kv in s.DataManager.MechDefs)
            {
                int part = s.GetItemCount(kv.Key, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
                int mechstor = s.GetItemCount(kv.Value.Chassis.Description.Id, kv.Value.GetType(), SimGameState.ItemCountType.UNDAMAGED_ONLY);
                int active = 0;
                MechDef d = kv.Value;
                foreach (KeyValuePair<int, MechDef> a in s.ActiveMechs)
                {
                    if (kv.Value.ChassisID==a.Value.ChassisID)
                    {
                        active++;
                        d = a.Value;
                    }
                }
                DumperDataEntry e = FillMech(d, part, s.Constants.Story.DefaultMechPartMax, mechstor, active);
                if (kv.Value.MechTags.Contains("BLACKLISTED"))
                    blacklist.Add(kv.Key, e);
                else
                    mechs.Add(e);
            }
            foreach (KeyValuePair<string, ItemCollectionDef> kv in s.DataManager.ItemCollectionDefs)
            {
                foreach (ItemCollectionDef.Entry e in kv.Value.Entries)
                {
                    if (e.Type==ShopItemType.Mech || e.Type == ShopItemType.MechPart)
                    {
                        if (blacklist.ContainsKey(e.ID))
                        {
                            mechs.Add(blacklist[e.ID]);
                            blacklist.Remove(e.ID);
                        }
                    }
                }
            }
            WriteDataEntries(mechs, w, csv);
        }

        private static void DumpWeapons(SimGameState s, StreamWriter w, StreamWriter csv)
        {
            List<DumperDataEntry> weps = new List<DumperDataEntry>();
            weps.Add(GetWeaponDesc());
            foreach (KeyValuePair<string, WeaponDef> kv in s.DataManager.WeaponDefs)
            {
                if (kv.Value.WeaponCategoryValue.IsMelee || kv.Value.WeaponSubType==WeaponSubType.AIImaginary || kv.Value.WeaponEffectID.Contains("WeaponEffect-Artillery"))
                    continue;
                weps.Add(FillWeapon(kv.Value));
            }
            WriteDataEntries(weps, w, csv);
        }

        private static void DumpAmmo(SimGameState s, StreamWriter w, StreamWriter csv)
        {
            List<DumperDataEntry> l = new List<DumperDataEntry>();
            l.Add(GetAmmoDesc());
            foreach (KeyValuePair<string, AmmunitionBoxDef> kv in s.DataManager.AmmoBoxDefs)
            {
                l.Add(FillAmmo(kv.Value));
            }
            WriteDataEntries(l, w, csv);
        }

        private static void DumpUpgrades(SimGameState s, StreamWriter w, StreamWriter csv)
        {
            List<DumperDataEntry> l = new List<DumperDataEntry>();
            l.Add(GetCompDesc());
            foreach (KeyValuePair<string, UpgradeDef> kv in s.DataManager.UpgradeDefs)
            {
                l.Add(FillComp(kv.Value));
            }
            foreach (KeyValuePair<string, JumpJetDef> kv in s.DataManager.JumpJetDefs)
            {
                l.Add(FillComp(kv.Value));
            }
            foreach (KeyValuePair<string, HeatSinkDef> kv in s.DataManager.HeatSinkDefs)
            {
                l.Add(FillComp(kv.Value));
            }
            WriteDataEntries(l, w, csv);
        }

        private static string GetSpacingFor(string s, int l)
        {
            return new string(' ', l - s.Length + 1);
        }

        private static void WriteDataEntries(List<DumperDataEntry> l, StreamWriter w, StreamWriter csv=null)
        {
            int[] len = new int[l[0].DataTxt.Length];
            for (int i=0; i < l[0].DataTxt.Length; i++)
            {
                len[i] = l.Max((e) => e.DataTxt[i].Length);
            }
            foreach (DumperDataEntry d in l.OrderBy((d)=>d.Sort))
            {
                for (int i = 0; i < d.DataTxt.Length; i++)
                {
                    w.Write(d.DataTxt[i]);
                    w.Write(GetSpacingFor(d.DataTxt[i], len[i]));
                }
                w.WriteLine();
                if (csv != null)
                    csv.WriteLine(d.DataCsv);
            }
        }

        private struct DumperDataEntry
        {
            public string[] DataTxt;
            public string DataCsv;
            public string Sort;
        }

        private static DumperDataEntry FillMech(MechDef d, int parts, int maxparts, int storage, int active)
        {
            DumperDataEntry r = new DumperDataEntry();
            r.DataTxt = new string[9];
            r.DataTxt[0] = ((storage+active)>0 ? "+" : "-") + d.Chassis.Tonnage + "t " + d.Chassis.Description.UIName + " " + d.Chassis.VariantName;
            r.DataTxt[1] = d.Chassis.StockRole + "";
            int bal = 0;
            int en = 0;
            int mis = 0;
            int sup = 0;
            MechStatisticsRules.GetHardpointCountForLocation(d, ChassisLocations.Head, ref bal, ref en, ref mis, ref sup);
            MechStatisticsRules.GetHardpointCountForLocation(d, ChassisLocations.CenterTorso, ref bal, ref en, ref mis, ref sup);
            MechStatisticsRules.GetHardpointCountForLocation(d, ChassisLocations.LeftTorso, ref bal, ref en, ref mis, ref sup);
            MechStatisticsRules.GetHardpointCountForLocation(d, ChassisLocations.RightTorso, ref bal, ref en, ref mis, ref sup);
            MechStatisticsRules.GetHardpointCountForLocation(d, ChassisLocations.LeftArm, ref bal, ref en, ref mis, ref sup);
            MechStatisticsRules.GetHardpointCountForLocation(d, ChassisLocations.RightArm, ref bal, ref en, ref mis, ref sup);
            MechStatisticsRules.GetHardpointCountForLocation(d, ChassisLocations.LeftLeg, ref bal, ref en, ref mis, ref sup);
            MechStatisticsRules.GetHardpointCountForLocation(d, ChassisLocations.RightLeg, ref bal, ref en, ref mis, ref sup);
            r.DataTxt[2] = bal + "/" + en + "/" + mis + "/" + sup;
            r.DataTxt[3] = (d.Chassis.Tonnage - d.Chassis.InitialTonnage) + "/" + d.Chassis.Heatsinks;
            if (d.Chassis.MovementCapDef == null)
            {
                d.Chassis.RefreshMovementCaps();
                if (d.Chassis.MovementCapDef==null)
                {
                    r.DataTxt[4] ="??/" + d.Chassis.MaxJumpjets;
                }
                else
                {
                    r.DataTxt[4] = d.Chassis.MovementCapDef.MaxWalkDistance + "/" + d.Chassis.MaxJumpjets;
                }
            }
            else
            {
                r.DataTxt[4] = d.Chassis.MovementCapDef.MaxWalkDistance + "/" + d.Chassis.MaxJumpjets;
            }
            r.DataTxt[5] = d.Chassis.MeleeDamage + "/" + d.Chassis.MeleeInstability + "/" + d.Chassis.DFADamage + "/" + d.Chassis.DFAInstability;
            r.DataTxt[6] = active + "/" + storage + "/" + parts + "/" + maxparts;
            r.DataTxt[7] = d.Chassis.Description.Id + "/" + d.Description.Id;
            r.DataTxt[8] = d.Chassis.YangsThoughts + "";
            r.Sort = string.Format("{0,3}_{1}", new object[] { d.Chassis.Tonnage, d.Chassis.VariantName });
            r.DataCsv = d.Chassis.Tonnage + ";" + d.Chassis.Description.UIName + ";" + d.Chassis.VariantName + ";" + d.Chassis.StockRole;
            r.DataCsv += ";" + bal + ";" + en + ";" + mis + ";" + sup;
            r.DataCsv += ";" + (d.Chassis.Tonnage - d.Chassis.InitialTonnage) + ";" + d.Chassis.Heatsinks;
            r.DataCsv += ";" + (d.Chassis.MovementCapDef==null ? -1f : d.Chassis.MovementCapDef.MaxWalkDistance) + ";" + d.Chassis.MaxJumpjets;
            r.DataCsv += ";" + d.Chassis.MeleeDamage + ";" + d.Chassis.MeleeInstability + ";" + d.Chassis.DFADamage + ";" + d.Chassis.DFAInstability;
            r.DataCsv += ";" + active + ";" + storage + ";" + parts + ";" + maxparts;
            r.DataCsv += ";" + d.Chassis.Description.Id + "/" + d.Description.Id;
            return r;
        }

        private static DumperDataEntry GetMechDesc()
        {
            DumperDataEntry r = new DumperDataEntry();
            r.DataTxt = new string[]
            {
                "Mech",
                "Role",
                "b/e/m/s",
                "useTon/heatsink",
                "walkDist/maxJJ",
                "m DMG/m INS/DFA DMG/ DFA INS",
                "active/storage/parts/partsneeded",
                "chassisID/mechID",
                "Yangs thougths",
            };
            r.Sort = "";
            r.DataCsv = "Tonnage;Mech;Variant;Role;b;e;m;s;usetonns;heatsinks;walkspeed;jumpjets;melee dmg;melee istab;dfa dmg;dfa istab;active;storage;parts;partsneeded;chassisID;mechID";
            return r;
        }

        private static DumperDataEntry FillComp(MechComponentDef d, int size=0, int off = 0)
        {
            DumperDataEntry r = new DumperDataEntry();
            r.DataTxt = new string[4 + size];
            r.DataTxt[0] = d.Description.UIName + " " + d.Description.Manufacturer + " " + d.Description.Model;
            r.DataTxt[1 + off] = d.Tonnage + "/" + d.InventorySize + "/" + d.CanExplode;
            r.DataTxt[2 + off] = d.Description.Id + "";
            r.DataTxt[3 + off] = d.BonusValueA + "";
            if (!string.IsNullOrEmpty(d.BonusValueB))
                r.DataTxt[3 + off] += "/" + d.BonusValueB;
            r.Sort = d.Description.Id + "";
            r.DataCsv = r.DataTxt[0] + ";" + d.Tonnage + ";" + d.InventorySize + ";" + d.CanExplode + ";" + d.Description.Id + ";\"" + r.DataTxt[3 + off] + "\"";
            return r;
        }

        private static DumperDataEntry GetCompDesc(int size=0, int off=0)
        {
            DumperDataEntry r = new DumperDataEntry();
            r.DataTxt = new string[4 + size];
            r.DataTxt[0] = "Upgrade";
            r.DataTxt[1 + off] = "Tonns/Size/Explodes";
            r.DataTxt[2 + off] = "ID";
            r.DataTxt[3 + off] = "Boni";
            r.Sort = "";
            r.DataCsv = "PartName;Tonns;Size;Explodes;ID;Boni";
            return r;
        }

        private static DumperDataEntry FillAmmo(AmmunitionBoxDef d)
        {
            DumperDataEntry r = FillComp(d, 1);
            r.DataTxt[4] = d.AmmoID + "/" + d.Capacity;
            r.DataCsv += ";" + d.AmmoID + ";" + d.Capacity;
            return r;
        }

        private static DumperDataEntry GetAmmoDesc()
        {
            DumperDataEntry r = GetCompDesc(1);
            r.DataTxt[0] = "AmmoBox";
            r.DataTxt[4] = "AmmoType/Capacity";
            r.DataCsv += ";AmmoType;Capacity";
            return r;
        }

        private static DumperDataEntry FillWeapon(WeaponDef d)
        {
            DumperDataEntry r = FillComp(d, 4, 4);
            r.DataTxt[1] = d.Damage + "/" + d.StructureDamage + "/" + d.Instability + "/" + d.HeatDamage + "*" + d.ShotsWhenFired;
            r.DataTxt[2] = d.MinRange + "/" + d.MediumRange + "/" + d.MaxRange;
            if (d.ComponentTags.Contains("range_extreme"))
                r.DataTxt[2] = "Extreme (" + r.DataTxt[2] + ")";
            else if (d.ComponentTags.Contains("range_very-long"))
                r.DataTxt[2] = "Very Long (" + r.DataTxt[2] + ")";
            else if (d.ComponentTags.Contains("range_long"))
                r.DataTxt[2] = "Long (" + r.DataTxt[2] + ")";
            else if (d.ComponentTags.Contains("range_standard"))
                r.DataTxt[2] = "Standard (" + r.DataTxt[2] + ")";
            else if (d.ComponentTags.Contains("range_close"))
                r.DataTxt[2] = "Close (" + r.DataTxt[2] + ")";
            r.DataTxt[3] = d.AccuracyModifier + "/" + d.CriticalChanceMultiplier + "/" + d.IndirectFireCapable + "/" + d.RefireModifier;
            r.DataTxt[4] = d.AmmoCategoryToAmmoId + "/" + d.StartingAmmoCapacity;
            r.DataCsv += ";" + d.Damage + ";" + d.StructureDamage + ";" + d.Instability + ";" + d.HeatDamage + ";" + d.ShotsWhenFired;
            r.DataCsv += ";" + d.MinRange + ";" + d.MediumRange + ";" + d.MaxRange;
            r.DataCsv += ";" + d.AccuracyModifier + ";" + d.CriticalChanceMultiplier + ";" + d.IndirectFireCapable + ";" + d.RefireModifier;
            r.DataCsv += ";" + d.AmmoCategoryToAmmoId + ";" + d.StartingAmmoCapacity;
            return r;
        }

        private static DumperDataEntry GetWeaponDesc()
        {
            DumperDataEntry r = GetCompDesc(4, 4);
            r.DataTxt[0] = "Weapon";
            r.DataTxt[1] = "Dmg/Struct/Inst/Heat*numShots";
            r.DataTxt[2] = "minRange/optRange/maxRange";
            r.DataTxt[3] = "AccAdd/CritChanceMult/IndirectFire/Refire";
            r.DataTxt[4] = "Ammo/Internal";
            r.DataCsv += ";Dmg;Struct;Inst;Heat;numShots";
            r.DataCsv += ";minRange;optRange;maxRange";
            r.DataCsv += ";AccAdd;CritChanceMult;IndirectFire;Refire";
            r.DataCsv += ";Ammo;Internal";
            return r;
        }
    }
}
