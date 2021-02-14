using BattleTech;
using Harmony;
using Org.BouncyCastle.Crypto.Digests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[assembly:AssemblyVersion("1.2.0")]

namespace BTMechDumper
{
    class BTMechDumper
    {
        public delegate int SMA_GetNumPartsForAssembly(SimGameState s, MechDef m);
        public delegate void CLoc_Localize(ref string text);
        public static SMA_GetNumPartsForAssembly Del_SMA_GetNumPartsForAssembly = null;
        public static CLoc_Localize Del_CLoc_Localize = null;

        public static void Init(string dir, string sett)
        {
            var harmony = HarmonyInstance.Create("com.github.mcb5637.BTMechDumper");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            // some reflection magic to get a delegate of BTSimpleMechAssembly.SimpleMechAssembly_Main.GetNumPartsForAssembly if that mod is loaded
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.GetName().Name.Equals("BTSimpleMechAssembly")) {
                    Type t = a.GetType("BTSimpleMechAssembly.SimpleMechAssembly_Main");
                    Del_SMA_GetNumPartsForAssembly = (SMA_GetNumPartsForAssembly) Delegate.CreateDelegate(typeof(SMA_GetNumPartsForAssembly), t.GetMethod("GetNumPartsForAssembly"));
                }
                else if (a.GetName().Name.Equals("CustomLocalization"))
                {
                    Type t = a.GetType("CustomTranslation.Text_Append");
                    Del_CLoc_Localize = (CLoc_Localize)Delegate.CreateDelegate(typeof(CLoc_Localize), t.GetMethods().Single((m)=> m.Name.Equals("Localize") && m.GetParameters().Length==1));
                }
            }
        }

        public static string TryLoc(string i)
        {
            Del_CLoc_Localize?.Invoke(ref i);
            return i;
        }

        public static void DumpData(SimGameState s)
        {
            string last = "";
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
                DumpMechs(s, w, mcsv, out last);
                w.WriteLine();
                DumpWeapons(s, w, wcsv, out last);
                w.WriteLine();
                DumpAmmo(s, w, acsv, out last);
                w.WriteLine();
                DumpUpgrades(s, w, ucsv, out last);
                w.Close();
                mcsv.Close();
                wcsv.Close();
                acsv.Close();
                ucsv.Close();
            }
            catch (Exception e)
            {
                FileLog.Log(last);
                FileLog.Log(e.ToString());
            }
        }

        public static void DumpMechs(SimGameState s, StreamWriter w, StreamWriter csv, out string last)
        {
            List<DumperDataEntry> mechs = new List<DumperDataEntry>();
            Dictionary<string, DumperDataEntry> blacklist = new Dictionary<string, DumperDataEntry>();
            mechs.Add(GetMechDesc());
            foreach (KeyValuePair<string, MechDef> kv in s.DataManager.MechDefs)
            {
                last = kv.Key;
                if (IsMechDefCustom(kv.Value) || IsMechDefCUVehicle(kv.Value))
                    continue;
                int part = s.GetItemCount(kv.Key, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
                int mechstor = s.GetItemCount(kv.Value.Chassis.Description.Id, kv.Value.GetType(), SimGameState.ItemCountType.UNDAMAGED_ONLY);
                int active = 0;
                int sma_parts = -1;
                MechDef d = kv.Value;
                foreach (KeyValuePair<int, MechDef> a in s.ActiveMechs)
                {
                    if (kv.Value.ChassisID==a.Value.ChassisID)
                    {
                        active++;
                        //d = a.Value;
                    }
                }
                if (Del_SMA_GetNumPartsForAssembly!=null)
                {
                    sma_parts = Del_SMA_GetNumPartsForAssembly(s, kv.Value);
                }
                DumperDataEntry e = FillMech(d, part, s.Constants.Story.DefaultMechPartMax, mechstor, active, sma_parts);
                if (kv.Value.MechTags.Contains("BLACKLISTED"))
                    blacklist.Add(kv.Key, e);
                else
                    mechs.Add(e);
            }
            foreach (KeyValuePair<string, ItemCollectionDef> kv in s.DataManager.ItemCollectionDefs)
            {
                last = kv.Key;
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
            last = "write";
            WriteDataEntries(mechs, w, csv);
            WriteDataEntries(blacklist.Values, null, csv);
        }

        private static void DumpWeapons(SimGameState s, StreamWriter w, StreamWriter csv, out string last)
        {
            List<DumperDataEntry> weps = new List<DumperDataEntry>();
            weps.Add(GetWeaponDesc());
            Dictionary<string, float> ammoShotsPerTon = new Dictionary<string, float>();
            foreach (KeyValuePair<string, AmmunitionBoxDef> kv in s.DataManager.AmmoBoxDefs)
            {
                last = kv.Key;
                float apt = kv.Value.Capacity / kv.Value.Tonnage;
                if (!ammoShotsPerTon.ContainsKey(kv.Value.AmmoID))
                {
                    ammoShotsPerTon.Add(kv.Value.AmmoID, apt);
                }
                else if (ammoShotsPerTon[kv.Value.AmmoID] < apt)
                {
                    ammoShotsPerTon[kv.Value.AmmoID] = apt;
                }
            }
            foreach (KeyValuePair<string, WeaponDef> kv in s.DataManager.WeaponDefs)
            {
                last = kv.Key;
                if (kv.Value.WeaponCategoryValue.IsMelee || kv.Value.WeaponSubType==WeaponSubType.AIImaginary || kv.Value.WeaponEffectID.Contains("WeaponEffect-Artillery"))
                    continue;
                weps.Add(FillWeapon(kv.Value, ammoShotsPerTon));
            }
            last = "write";
            WriteDataEntries(weps, w, csv);
        }

        private static void DumpAmmo(SimGameState s, StreamWriter w, StreamWriter csv, out string last)
        {
            List<DumperDataEntry> l = new List<DumperDataEntry>();
            l.Add(GetAmmoDesc());
            foreach (KeyValuePair<string, AmmunitionBoxDef> kv in s.DataManager.AmmoBoxDefs)
            {
                last = kv.Key;
                l.Add(FillAmmo(kv.Value));
            }
            last = "write";
            WriteDataEntries(l, w, csv);
        }

        private static void DumpUpgrades(SimGameState s, StreamWriter w, StreamWriter csv, out string last)
        {
            List<DumperDataEntry> l = new List<DumperDataEntry>();
            l.Add(GetCompDesc());
            foreach (KeyValuePair<string, UpgradeDef> kv in s.DataManager.UpgradeDefs)
            {
                last = kv.Key;
                l.Add(FillComp(kv.Value));
            }
            foreach (KeyValuePair<string, JumpJetDef> kv in s.DataManager.JumpJetDefs)
            {
                last = kv.Key;
                l.Add(FillComp(kv.Value));
            }
            foreach (KeyValuePair<string, HeatSinkDef> kv in s.DataManager.HeatSinkDefs)
            {
                last = kv.Key;
                l.Add(FillComp(kv.Value));
            }
            last = "write";
            WriteDataEntries(l, w, csv);
        }

        private static string GetSpacingFor(string s, int l)
        {
            return new string(' ', l - s.Length + 1);
        }

        private static void WriteDataEntries(IEnumerable<DumperDataEntry> l, StreamWriter w, StreamWriter csv=null)
        {
            int[] len = new int[l.First().DataTxt.Length];
            for (int i=0; i < l.First().DataTxt.Length; i++)
            {
                len[i] = l.Max((e) => e.DataTxt[i].Length);
            }
            foreach (DumperDataEntry d in l.OrderBy((d)=>d.Sort))
            {
                if (w != null)
                {
                    for (int i = 0; i < d.DataTxt.Length; i++)
                    {
                        w.Write(d.DataTxt[i]);
                        w.Write(GetSpacingFor(d.DataTxt[i], len[i]));
                    }
                    w.WriteLine(); 
                }
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

        public static bool IsMechDefCustom(MechDef d)
        {
            return d.Description.Id.Contains("mechdef_CUSTOM_");
        }

        public static bool IsMechDefCUVehicle(MechDef d)
        {
            return d.Description.Id.Contains("vehicledef_");
        }

        private static readonly ChassisLocations[] AllChassisLocs = new ChassisLocations[] { ChassisLocations.Head, ChassisLocations.CenterTorso, ChassisLocations.LeftTorso, ChassisLocations.RightTorso,
            ChassisLocations.LeftArm, ChassisLocations.RightArm, ChassisLocations.LeftLeg, ChassisLocations.RightLeg };
        private static readonly string[] ExtrasToNote = new string[] { "chassis_ferro", "chassis_endo", "chassis_dhs", "chassis_omni" };

        private static DumperDataEntry FillMech(MechDef d, int parts, int maxparts, int storage, int active, int sma_parts)
        {
            DumperDataEntry r = new DumperDataEntry();
            r.DataTxt = new string[9];
            r.DataTxt[0] = ((storage+active)>0 ? "+" : "-") + d.Chassis.Tonnage + "t " + TryLoc(d.Chassis.Description.UIName) + " " + TryLoc(d.Chassis.VariantName);
            r.DataTxt[1] = TryLoc(d.Chassis.StockRole) + "";
            int bal = 0;
            int en = 0;
            int mis = 0;
            int sup = 0;
            foreach (ChassisLocations c in AllChassisLocs)
                MechStatisticsRules.GetHardpointCountForLocation(d, c, ref bal, ref en, ref mis, ref sup);
            r.DataTxt[2] = bal + "/" + en + "/" + mis + "/" + sup;
            r.DataTxt[3] = (d.Chassis.Tonnage - d.Chassis.InitialTonnage) + "/" + d.Chassis.Heatsinks;
            float carmor = 0;
            float marmor = 0;
            foreach (ChassisLocations c in AllChassisLocs)
            {
                carmor += d.GetLocationLoadoutDef(c).AssignedArmor;
                marmor += d.GetChassisLocationDef(c).MaxArmor;
                if (d.GetChassisLocationDef(c).MaxRearArmor > 0)
                {
                    carmor += d.GetLocationLoadoutDef(c).AssignedRearArmor;
                    marmor += d.GetChassisLocationDef(c).MaxRearArmor;
                }
            }
            float div = UnityGameInstance.BattleTechGame.MechStatisticsConstants.ARMOR_PER_TENTH_TON * 10f;
            if (d.Chassis.ChassisTags.Contains("chassis_ferro"))
                if (d.Chassis.ChassisTags.Contains("chassis_clan"))
                    div = UnityGameInstance.BattleTechGame.MechStatisticsConstants.ARMOR_PER_TENTH_TON * 12f;
                else
                    div = UnityGameInstance.BattleTechGame.MechStatisticsConstants.ARMOR_PER_TENTH_TON * 11.2f;
            carmor /= div;
            marmor /= div;
            carmor = Mathf.Round(carmor * 10) / 10;
            marmor = Mathf.Round(marmor * 10) / 10;
            r.DataTxt[3] += "/" + carmor + "/" + marmor + "/" + (d.Chassis.Tonnage - d.Chassis.InitialTonnage - marmor);
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
            r.DataTxt[6] = active + "/" + storage + "/" + parts;
            if (sma_parts >=0)
                r.DataTxt[6] += "(" + sma_parts + ")";
            r.DataTxt[6] += "/" + maxparts;
            r.DataTxt[7] = d.Chassis.Description.Id + "/" + d.Description.Id;
            Dictionary<string, int> eq = new Dictionary<string, int>();
            Dictionary<string, int> feq = new Dictionary<string, int>();
            foreach (MechComponentRef c in d.Inventory)
            {
                if (c.ComponentDefType == ComponentType.Weapon)
                {
                    WeaponDef wep = c.Def as WeaponDef;
                    if (wep != null && wep.WeaponCategoryValue.IsMelee || wep.WeaponSubType == WeaponSubType.AIImaginary || wep.WeaponEffectID.Contains("WeaponEffect-Artillery"))
                        continue;
                }
                string key = c.Def.Description.Id;
                if (c.IsFixed)
                {
                    if (feq.ContainsKey(key))
                        feq[key]++;
                    else
                        feq.Add(key, 1);
                }
                else
                {
                    if (eq.ContainsKey(key))
                        eq[key]++;
                    else
                        eq.Add(key, 1);
                }
            }
            string txteq = "";
            string txtfeq = "";
            foreach (string key in eq.Keys.OrderBy((k) => k))
            {
                if (!string.IsNullOrEmpty(txteq))
                    txteq += ",";
                txteq += key + ":" + eq[key];
            }
            foreach (string key in feq.Keys.OrderBy((k) => k))
            {
                if (!string.IsNullOrEmpty(txtfeq))
                    txtfeq += ",";
                txtfeq += key + ":" + feq[key];
            }
            string txtext = "";
            foreach (string ex in ExtrasToNote)
            {
                if (d.Chassis.ChassisTags.Contains(ex))
                {
                    if (!string.IsNullOrEmpty(txtext))
                        txtext += ",";
                    txtext += ex;
                }
            }
            r.DataTxt[8] = txtext + "/" + txteq + "/" + txtfeq;
            r.Sort = string.Format("{0,3}_{1}", new object[] { d.Chassis.Tonnage, d.Chassis.VariantName });
            r.DataCsv = d.Chassis.Tonnage + ";" + d.Chassis.Description.UIName + ";" + d.Chassis.VariantName + ";" + d.Chassis.StockRole;
            r.DataCsv += ";" + bal + ";" + en + ";" + mis + ";" + sup;
            r.DataCsv += ";" + (d.Chassis.Tonnage - d.Chassis.InitialTonnage) + ";" + d.Chassis.Heatsinks + ";" + carmor + ";" + marmor + ";" + (d.Chassis.Tonnage - d.Chassis.InitialTonnage - marmor);
            r.DataCsv += ";" + (d.Chassis.MovementCapDef==null ? -1f : d.Chassis.MovementCapDef.MaxWalkDistance) + ";" + d.Chassis.MaxJumpjets;
            r.DataCsv += ";" + d.Chassis.MeleeDamage + ";" + d.Chassis.MeleeInstability + ";" + d.Chassis.DFADamage + ";" + d.Chassis.DFAInstability;
            r.DataCsv += ";" + active + ";" + storage + ";" + parts;
            if (sma_parts >= 0)
                r.DataCsv += "(" + sma_parts + ")";
            r.DataCsv += ";" + maxparts;
            r.DataCsv += ";" + d.Chassis.Description.Id + ";" + d.Description.Id;
            r.DataCsv += ";" + txtext + ";" + txteq + ";" + txtfeq;
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
                "useTon/heatsink/armorT/maxarmorT/useTonnsWithMArmor",
                "walkDist/maxJJ",
                "m DMG/m INS/DFA DMG/ DFA INS",
                "active/storage/parts/partsneeded",
                "chassisID/mechID",
                "extras/Equip/FixedEquip",
            };
            r.Sort = "";
            r.DataCsv = "Tonnage;Mech;Variant;Role;b;e;m;s;usetonns;heatsinks;carmorT;marmorT;useTonnsWithMArmor;walkspeed;jumpjets;melee dmg;melee istab;dfa dmg;dfa istab;active;storage;parts;partsneeded;chassisID;mechID;extras;equipment;fixed equipment";
            return r;
        }

        private static DumperDataEntry FillComp(MechComponentDef d, int size=0, int off = 0)
        {
            DumperDataEntry r = new DumperDataEntry();
            r.DataTxt = new string[4 + size];
            r.DataTxt[0] = TryLoc(d.Description.UIName) + " " + TryLoc(d.Description.Manufacturer) + " " + TryLoc(d.Description.Model);
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

        private static DumperDataEntry FillWeapon(WeaponDef d, Dictionary<string, float> ammoShotsPerTonn)
        {
            DumperDataEntry r = FillComp(d, 5, 5);
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
            float efftonn = d.Tonnage;
            float ammotonns = 0;
            if (!string.IsNullOrEmpty(d.AmmoCategoryToAmmoId) && ammoShotsPerTonn.ContainsKey(d.AmmoCategoryToAmmoId)) // add ammo tonnage
            {
                float shots = 10 * d.ShotsWhenFired - d.StartingAmmoCapacity;
                if (shots > 0)
                {
                    ammotonns += shots / ammoShotsPerTonn[d.AmmoCategoryToAmmoId];
                    efftonn += ammotonns;
                }
            }
            float efftonndhs = efftonn;
            efftonn += d.HeatGenerated / 3; // heat w standard heatsinks
            efftonndhs += d.HeatGenerated / 6; // heat w double heatsinks
            float effdmg = (d.Damage + d.StructureDamage + d.HeatDamage + d.Instability) * d.ShotsWhenFired;
            float accmod = 0.5f + (-d.AccuracyModifier * 0.05f);
            float rmod = GetWeaponRangeFactor(d);
            float rating = Mathf.Round(effdmg * accmod * rmod / efftonn * 100) / 100;
            float ratingdhs = Mathf.Round(effdmg * accmod * rmod / efftonndhs * 100) / 100;
            r.DataTxt[5] = efftonn + "/" + efftonndhs + "/" + rating + "/" + ratingdhs;
            r.DataCsv += ";" + d.Damage + ";" + d.StructureDamage + ";" + d.Instability + ";" + d.HeatDamage + ";" + d.ShotsWhenFired;
            r.DataCsv += ";" + d.MinRange + ";" + d.MediumRange + ";" + d.MaxRange;
            r.DataCsv += ";" + d.AccuracyModifier + ";" + d.CriticalChanceMultiplier + ";" + d.IndirectFireCapable + ";" + d.RefireModifier;
            r.DataCsv += ";" + d.AmmoCategoryToAmmoId + ";" + d.StartingAmmoCapacity;
            r.DataCsv += ";" + effdmg + ";" + accmod + ";" + rmod + ";" + ammotonns + ";" + efftonn + ";" + efftonndhs + ";" + rating + ";" + ratingdhs;
            return r;
        }

        private static DumperDataEntry GetWeaponDesc()
        {
            DumperDataEntry r = GetCompDesc(5, 5);
            r.DataTxt[0] = "Weapon";
            r.DataTxt[1] = "Dmg/Struct/Inst/Heat*numShots";
            r.DataTxt[2] = "minRange/optRange/maxRange";
            r.DataTxt[3] = "AccAdd/CritChanceMult/IndirectFire/Refire";
            r.DataTxt[4] = "Ammo/Internal";
            r.DataTxt[5] = "effTonn/effTonnDHS/rating/ratingDHS";
            r.DataCsv += ";Dmg;Struct;Inst;Heat;numShots";
            r.DataCsv += ";minRange;optRange;maxRange";
            r.DataCsv += ";AccAdd;CritChanceMult;IndirectFire;Refire";
            r.DataCsv += ";Ammo;Internal";
            r.DataCsv += ";effectiveDamage;accuracyFactor;rangeFactor;ammoTonnage10;effectiveTonnage;effectiveTonnageDHS;rating;ratingDHS";
            return r;
        }

        private static float GetWeaponRangeFactor(WeaponDef d)
        {
            float one = 375; // l laser, 1
            float s = 90f; // s laser, 0.75
            float r = (d.MediumRange - d.MinRange) + (d.MaxRange - d.MediumRange) / 2;
            // rating = del * range + b;
            float del = (1 - 0.75f) / (one - s);
            float b = 1 - (del * one);
            return del * r + b;
        }
    }
}
